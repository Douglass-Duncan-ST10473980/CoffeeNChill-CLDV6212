//Author: Tahir Ismail , Douglass Duncan, Neha
using Azure.Monitor.OpenTelemetry.Exporter;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSingleton<MenuStorageService>(serviceProvider =>
{
    string connectionString =
        Environment.GetEnvironmentVariable("AzureWebJobsStorage")
        ?? "UseDevelopmentStorage=true";

    var logger =
        serviceProvider.GetRequiredService<ILogger<MenuStorageService>>();

    return new MenuStorageService(connectionString, logger);
});

if (!string.IsNullOrEmpty(
        Environment.GetEnvironmentVariable(
            "APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();