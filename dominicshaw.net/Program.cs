using Azure.Core;
using Azure.Identity;
using DominicShawNet.Contact;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();
builder.Services.Configure<ContactEmailOptions>(
    builder.Configuration.GetSection(ContactEmailOptions.SectionName));
builder.Services.Configure<ContactRateLimitOptions>(
    builder.Configuration.GetSection(ContactRateLimitOptions.SectionName));
builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IContactEmailSender, AzureCommunicationServicesContactEmailSender>();
builder.Services.AddSingleton<IContactRateLimitStore, AzureTableContactRateLimitStore>();
builder.Services.AddSingleton<IContactRateLimiter, FixedWindowContactRateLimiter>();

builder.Build().Run();
