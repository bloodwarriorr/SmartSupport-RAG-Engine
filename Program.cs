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


builder.Services.AddOpenAITextEmbeddingGeneration(modelId, openAiKey);


builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<ISearchService, SearchService>();
var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();