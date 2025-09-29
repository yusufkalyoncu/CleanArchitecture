using CleanArchitecture.Application;
using CleanArchitecture.Infrastructure;
using CleanArchitecture.WebApi.Extensions;
using CleanArchitecture.WebApi.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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
app.MapControllers();
app.UseHttpsRedirection();
app.UseDocs();

await app.RunAsync();