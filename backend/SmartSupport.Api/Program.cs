using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using SmartSupport.Api.Data;
using SmartSupport.Api.Interfaces;
using SmartSupport.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var openAiKey = builder.Configuration["OpenAI:ApiKey"];
var modelId = builder.Configuration["OpenAI:ModelId"] ?? "text-embedding-3-small";
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];

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
app.UseCors();
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