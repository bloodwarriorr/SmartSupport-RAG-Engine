using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using SmartSupport.Api.Data;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Services;

var builder = WebApplication.CreateBuilder(args);


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var openAiKey = builder.Configuration["OpenAI:ApiKey"];
var modelId = builder.Configuration["OpenAI:ModelId"] ?? "text-embedding-3-small";


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2Support", true);
builder.Services.AddHttpClient<IOllamaChatService, OllamaChatService>();
builder.Services.AddKernel()
    .AddOllamaTextEmbeddingGeneration(
        modelId: "nomic-embed-text",
        endpoint: new Uri("http://localhost:11434"))
    .AddOllamaChatCompletion(
        modelId: "llama3",
        endpoint: new Uri("http://localhost:11434"));

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.Run();