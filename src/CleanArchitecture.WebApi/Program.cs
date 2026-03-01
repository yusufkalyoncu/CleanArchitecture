using System.Reflection;
using CleanArchitecture.Application;
using CleanArchitecture.Infrastructure;
using CleanArchitecture.WebApi.Extensions;
using CleanArchitecture.WebApi.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.AddSerilog();

//builder.Services.AddControllers(); // Enable this only if you are using Controllers
builder.Services.AddDocs();
builder.Services.AddLocalization();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

var app = builder.Build();

app.UseMiddlewares();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseDocs();
app.ApplyMigrations();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapEndpoints();
// app.MapControllers(); // Enable this only if you are using Controllers

await app.RunAsync();