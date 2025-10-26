using CleanArchitecture.Application;
using CleanArchitecture.Infrastructure;
using CleanArchitecture.WebApi.Extensions;
using CleanArchitecture.WebApi.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddDocs();
builder.Services.AddLocalization();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddlewares();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseDocs();
app.ApplyMigrations();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();