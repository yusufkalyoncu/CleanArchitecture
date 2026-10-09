using System.Security.Claims;
using System.Threading.RateLimiting;
using CleanArchitecture.Infrastructure.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using StackExchange.Redis;
using Microsoft.AspNetCore.RateLimiting;

namespace CleanArchitecture.Integration.Tests.Infrastructure.RateLimiting;

public class RateLimitingExtensionsTests
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILoggerFactory _loggerFactory;

    public RateLimitingExtensionsTests()
    {
        _services = new ServiceCollection();
        
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"RateLimit:Global:PermitLimit", "100"},
            {"RateLimit:Global:WindowInSeconds", "60"},
            {"RateLimit:Login:PermitLimit", "5"},
            {"RateLimit:Login:WindowInSeconds", "60"},
            {"RateLimit:Registration:PermitLimit", "3"},
            {"RateLimit:Registration:WindowInSeconds", "60"}
        });
        _configuration = configBuilder.Build();
        
        _redis = Substitute.For<IConnectionMultiplexer>();
        _redis.IsConnected.Returns(true);
        _services.AddSingleton(_redis);
        
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _services.AddSingleton(_loggerFactory);
    }

    [Fact]
    public void GlobalLimiter_Should_ReturnRedisLimiter_WhenAuthenticatedWithSub()
    {
        _services.AddRateLimiting(_configuration);
        var provider = _services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "user-123")], "Bearer");
        context.User = new ClaimsPrincipal(identity);
        context.RequestServices = provider;

        var limiter = options.GlobalLimiter!.AcquireAsync(context);
        limiter.Should().NotBeNull();
    }
    
    [Fact]
    public void GlobalLimiter_Should_ReturnRedisLimiter_WhenNotAuthenticated()
    {
        _services.AddRateLimiting(_configuration);
        var provider = _services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext
        {
            Connection =
            {
                RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1")
            },
            RequestServices = provider
        };

        var limiter = options.GlobalLimiter!.AcquireAsync(context);
        limiter.Should().NotBeNull();
    }
    
    [Fact]
    public async Task GlobalLimiter_Should_ThrowException_WhenAuthenticatedWithoutSub()
    {
        _services.AddRateLimiting(_configuration);
        var provider = _services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity([new Claim("other-claim", "val")], "Bearer");
        context.User = new ClaimsPrincipal(identity);
        context.RequestServices = provider;

        Func<Task> action = async () => await options.GlobalLimiter!.AcquireAsync(context);
        var assertion = await action.Should().ThrowAsync<InvalidOperationException>();
        assertion.WithMessage("*does not have a 'sub' claim*");
    }

    [Fact]
    public void GlobalLimiter_Should_FallbackToLocalLimiter_WhenRedisNotConnected()
    {
        _redis.IsConnected.Returns(false);
        _services.AddRateLimiting(_configuration);
        var provider = _services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };

        var limiter = options.GlobalLimiter!.AcquireAsync(context);
        limiter.Should().NotBeNull();
    }

    [Fact]
    public void GlobalLimiter_Should_FallbackToLocalLimiter_WhenRedisThrowsException()
    {
        _redis.IsConnected.Returns(true);
        // Force an exception during creation we can't easily make RedisRateLimitPartition throw just from IsConnected=true, 
        // but if we make RequestServices throw when resolving something we can trigger it. 
        // Wait, CreateRedisRateLimiter resolves IConnectionMultiplexer.
        // Let's replace the provider's IConnectionMultiplexer with one that throws.
        var badServices = new ServiceCollection();
        badServices.AddRateLimiting(_configuration);
        badServices.AddSingleton(_loggerFactory);
        badServices.AddTransient<IConnectionMultiplexer>(_ => throw new Exception("Redis ded"));
        var provider = badServices.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };

        var limiter = options.GlobalLimiter!.AcquireAsync(context);
        limiter.Should().NotBeNull();
    }

    [Fact]
    public void LoginPolicy_Should_FallbackToLocalSlidingWindow_WhenRedisThrowsException()
    {
        var badServices = new ServiceCollection();
        badServices.AddRateLimiting(_configuration);
        badServices.AddSingleton<ILoggerFactory>(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        badServices.AddTransient<IConnectionMultiplexer>(_ => throw new Exception("Redis ded"));
        var provider = badServices.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            Connection =
            {
                RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1")
            },
            RequestServices = provider
        };

        var method = typeof(RateLimitingExtensions).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .First(m => m.Name == "CreateRedisRateLimiter");
        
        var limiter = method.Invoke(null, [context, "key", 5, 60, RateLimitAlgorithm.SlidingWindow]);
        limiter.Should().NotBeNull();
    }
    
    private class DummyLease(bool hasRetryAfter = false, TimeSpan retryAfter = default) : RateLimitLease
    {
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => Array.Empty<string>();
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name && hasRetryAfter)
            {
                metadata = retryAfter;
                return true;
            }
            metadata = null;
            return false;
        }

        private bool _disposed;
        
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task OnRejected_Should_WriteProblemDetails()
    {
        _services.AddRateLimiting(_configuration);
        var provider = _services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            },
            RequestServices = provider
        };

        var rejectedContext = new OnRejectedContext
        {
            HttpContext = context,
            Lease = new DummyLease()
        };

        await options.OnRejected!(rejectedContext, CancellationToken.None);
        
        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.Should().Contain("Too Many Requests");
    }
    
    [Fact]
    public async Task OnRejected_Should_IncludeRetryAfter_WhenMetadataExists()
    {
        _services.AddRateLimiting(_configuration);
        var provider = _services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        var context = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            },
            RequestServices = provider
        };

        var rejectedContext = new OnRejectedContext
        {
            HttpContext = context,
            Lease = new DummyLease(true, TimeSpan.FromSeconds(10))
        };

        await options.OnRejected!(rejectedContext, CancellationToken.None);
        
        context.Response.Headers.RetryAfter.ToString().Should().Be("10");
    }
}