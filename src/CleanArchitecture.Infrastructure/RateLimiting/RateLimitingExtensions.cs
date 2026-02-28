using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using RedisRateLimiting;
using StackExchange.Redis;

namespace CleanArchitecture.Infrastructure.RateLimiting;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(rateLimitOptions =>
        {
            #region Policies

            //Global Policy
            rateLimitOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var key = GetUserIdentifier(httpContext);
                return CreateRedisRateLimiter(
                    httpContext, 
                    $"glb:{key}", 
                    options.Global.PermitLimit, 
                    options.Global.WindowInSeconds);
            });

            //Login Policy
            rateLimitOptions.AddPolicy(RateLimitPolicies.Login, httpContext =>
            {
                var key = $"login:{httpContext.Connection.RemoteIpAddress}";
                return CreateRedisRateLimiter(
                    httpContext,
                    key,
                    options.Login.PermitLimit,
                    options.Login.WindowInSeconds,
                    RateLimitAlgorithm.SlidingWindow);
            });

            //Registration Policy
            rateLimitOptions.AddPolicy(RateLimitPolicies.Registration, httpContext =>
            {
                var key = $"registration:{httpContext.Connection.RemoteIpAddress}";
                return CreateRedisRateLimiter(
                    httpContext,
                    key,
                    options.Registration.PermitLimit,
                    options.Registration.WindowInSeconds);
            });

            #endregion

            #region OnRejected Handler

            rateLimitOptions.OnRejected = async (context, token) =>
            {
                var detailMessage = "API rate limit exceeded. Please try again later.";
                
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    detailMessage = $"API rate limit exceeded. Please try again in {(int)retryAfter.TotalSeconds} seconds.";
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                    Detail = detailMessage,
                    Instance = context.HttpContext.Request.Path
                };

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, token);
            };

            #endregion
        });

        return services;
    }

    #region Helper Methods

    private static string GetUserIdentifier(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                   ?? throw new InvalidOperationException("Authenticated user does not have a 'sub' claim.");
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static RateLimitPartition<string> CreateRedisRateLimiter(
        HttpContext httpContext, 
        string partitionKey, 
        int permitLimit, 
        int windowSeconds,
        RateLimitAlgorithm algorithm = RateLimitAlgorithm.FixedWindow)
    {
        var window = TimeSpan.FromSeconds(windowSeconds);

        try
        {
            var redis = httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>();

            if (!redis.IsConnected)
                throw new InvalidOperationException("Redis is not connected.");

            return algorithm switch
            {
                RateLimitAlgorithm.SlidingWindow => RedisRateLimitPartition.GetSlidingWindowRateLimiter(partitionKey, _ => new RedisSlidingWindowRateLimiterOptions
                {
                    ConnectionMultiplexerFactory = () => redis,
                    PermitLimit = permitLimit,
                    Window = window,
                }),
                _ => RedisRateLimitPartition.GetFixedWindowRateLimiter(partitionKey, _ => new RedisFixedWindowRateLimiterOptions
                {
                    ConnectionMultiplexerFactory = () => redis,
                    PermitLimit = permitLimit,
                    Window = window
                })
            };
        }
        catch (Exception ex)
        {
            var logger = httpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("RateLimitingFallback");

            logger.LogWarning(ex, "Redis unavailable for key {Key}, using in-memory fallback.", partitionKey);

            return algorithm switch
            {
                RateLimitAlgorithm.SlidingWindow => RateLimitPartition.GetSlidingWindowLimiter($"local:{partitionKey}", _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    SegmentsPerWindow = 5
                }),
                _ => RateLimitPartition.GetFixedWindowLimiter($"local:{partitionKey}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window
                })
            };
        }
    }

    #endregion
}