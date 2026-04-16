using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;
using SmartSupport.Api.Data;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Services;
var builder = WebApplication.CreateBuilder(args);


var connectionString = builder.Configuration.GetConnectionString("SupabaseConnection");


var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var qdrantClient = builder.Configuration["Qdrant:Host"];
var groqApiKey = builder.Configuration["Groq:ApiKey"] ?? "PLACEHOLDER";
var groqModel = builder.Configuration["Groq:Model"] ?? "llama3-8b-8192";


var huggingFaceApiKey = builder.Configuration["HuggingFace:ApiKey"];


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LivePolicy", policy =>
    {
        policy.WithOrigins("https://smart-support-rag-engine.vercel.app/") 
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); 
    });
});
builder.Services.AddSingleton<QdrantClient>(sp =>
{
    var rawUrl = builder.Configuration["Qdrant:Host"]; 
    var apiKey = builder.Configuration["Qdrant:ApiKey"];

    var uri = new Uri(rawUrl);
    var host = uri.Host;

    return new QdrantClient(host: host, apiKey: apiKey, https: true);
});
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2Support", true);

builder.Services.AddOpenAIChatCompletion(
    modelId: groqModel,
    apiKey: groqApiKey,
    endpoint: new Uri("https://api.groq.com/openai/v1") 
);
if (string.IsNullOrWhiteSpace(huggingFaceApiKey))
{
    throw new InvalidOperationException("HuggingFace API key missing in configuration (HuggingFace:ApiKey). Please set a valid API key in appsettings or environment variables.");
}

#pragma warning disable SKEXP0010
builder.Services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
    new HuggingFaceEmbeddingService("sentence-transformers/all-MiniLM-L6-v2", huggingFaceApiKey));
#pragma warning restore SKEXP0070
//builder.Services.AddHttpClient<IOllamaChatService, OllamaChatService>();
//builder.Services.AddKernel()
//    .AddOllamaTextEmbeddingGeneration(
//        modelId: "nomic-embed-text",
//        endpoint: new Uri("http://localhost:11434"))
//    .AddOllamaChatCompletion(
//        modelId: "llama3",
//        endpoint: new Uri("http://localhost:11434"));




builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://accounts.google.com";
        options.Audience = googleClientId;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            
            ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
            ValidateAudience = true,
            ValidAudience = googleClientId,
            ValidateLifetime = true,
            
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("--- Auth Failed ---");
                Console.WriteLine($"Exception: {context.Exception.Message}");
                // אם הטוקן הגיע אבל הוא לא תקין, זה ידפיס למה
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("--- Auth Success! ---");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseCors("LivePolicy");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();