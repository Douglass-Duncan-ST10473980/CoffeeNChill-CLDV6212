using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ======================================================================================
// PROGRAM - Application Entry Point & Dependency Injection
// ======================================================================================
//
// This file serves as the main entry point for the Isolated Process Azure Functions app.
// It configures the host builder, Dependency Injection (DI), and monitoring tools.
//
// Key configurations applied here:
//
// 1. Functions Web Application Middleware:
//    - Uses builder.ConfigureFunctionsWebApplication() to enable full integration with 
//      ASP.NET Core middleware and routing features (applies to .NET 8 Isolated model).
//
// 2. Dependency Injection (DI) Setup:
//    - Registers MenuStorageService as a Singleton service.
//    - Dynamically fetches the "AzureWebJobsStorage" connection string from environment 
//      variables (defaults to Azure Storage Emulator if not found in production).
//    - Injects an ILogger into the MenuStorageService during instantiation for 
//      proper logging of database operations.
//
// 3. Application Insights:
//    - Uses the official Microsoft.ApplicationInsights.WorkerService packages
//      which are compatible with .NET 8 Isolated Functions.
//
// ======================================================================================

var builder = FunctionsApplication.CreateBuilder(args);

// Dependency Injection Configuration

// Register MenuStorageService with logger support
builder.Services.AddSingleton<MenuStorageService>(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                           ?? "UseDevelopmentStorage=true";

    // Get logger for the service
    var logger = sp.GetService<ILogger<MenuStorageService>>();

    // Create service instance with logger
    return new MenuStorageService(connectionString, logger);
});

// Application Insights - using the official isolated worker packages
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Configure the web application MUST happen BEFORE Build() in .NET 8 Isolated functions
builder.ConfigureFunctionsWebApplication();

builder.Build().Run();
