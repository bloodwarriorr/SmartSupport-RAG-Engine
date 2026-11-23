using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using SmartSupport.Application.Abstractions.AI;
using SmartSupport.Application.Abstractions.Persistence;
using SmartSupport.Application.Common.Interfaces;
using SmartSupport.Infrastructure.AI;
using SmartSupport.Infrastructure.Documents;
using SmartSupport.Infrastructure.Persistence;
using SmartSupport.Infrastructure.Persistence.Repositories;
using SmartSupport.Infrastructure.VectorStore;

namespace SmartSupport.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the data access layer and the external-service adapters.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddVectorStore(services, configuration);
        AddAi(services, configuration);

        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddSingleton<ITextChunker, SemanticKernelTextChunker>();

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Missing connection string 'ConnectionStrings:{DatabaseOptions.ConnectionStringName}'.");

        // Timestamps are stored as "timestamp with time zone" while entities use DateTime.UtcNow.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3))
            // The model snapshot is maintained by hand (EF tooling can't run on the dev machine);
            // don't block startup on cosmetic snapshot differences.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
    }

    private static void AddVectorStore(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection(QdrantOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
            var host = new Uri(options.Host).Host;
            return new QdrantClient(host: host, apiKey: options.ApiKey, https: true);
        });
        services.AddSingleton<IVectorStore, QdrantVectorStore>();
    }

    private static void AddAi(IServiceCollection services, IConfiguration configuration)
    {
        // Embeddings: Gemini through a typed HttpClient (pooled handlers, no socket exhaustion).
        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IEmbeddingGenerator, GeminiEmbeddingGenerator>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("x-goog-api-key", options.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Answers: Groq's OpenAI-compatible endpoint through Semantic Kernel.
        services.AddOptions<GroqOptions>()
            .Bind(configuration.GetSection(GroqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var groq = configuration.GetSection(GroqOptions.SectionName).Get<GroqOptions>() ?? new GroqOptions();
        services.AddOpenAIChatCompletion(
            modelId: groq.Model,
            apiKey: groq.ApiKey,
            endpoint: new Uri(groq.Endpoint));

        services.AddScoped<IChatModel, GroqChatModel>();
    }
}
