using SmartSupport.Api.Extensions;
using SmartSupport.Application;
using SmartSupport.Infrastructure;
using SmartSupport.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Don't advertise the server software in responses.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services
    .AddApplication(builder.Configuration)      // BLL: use cases and business rules
    .AddInfrastructure(builder.Configuration)   // DAL + Qdrant, Gemini, Groq, PDF adapters
    .AddApi(builder.Configuration);             // HTTP: auth, CORS, rate limits, errors, controllers

var app = builder.Build();

// Fail fast: if the database can't be migrated, the deploy should fail instead of serving errors.
await app.Services.MigrateDatabaseAsync();

app.UseApiPipeline();
app.Run();
