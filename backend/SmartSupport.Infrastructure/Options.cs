using System.ComponentModel.DataAnnotations;

namespace SmartSupport.Infrastructure;

// Strongly typed settings, validated when the app starts so a missing key fails the deploy
// immediately instead of on the first request. Section names match the existing environment
// variables (e.g. Gemini__ApiKey), so no Railway variables need to change.

public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "SupabaseConnection";
}

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    /// <summary>Cluster URL, e.g. https://xxxx.cloud.qdrant.io (the gRPC port is used automatically).</summary>
    [Required, Url] public string Host { get; set; } = string.Empty;
    [Required] public string ApiKey { get; set; } = string.Empty;
    [Required] public string CollectionName { get; set; } = "knowledge_base_gemini";
}

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    [Required] public string ApiKey { get; set; } = string.Empty;
    [Required] public string Model { get; set; } = "gemini-embedding-001";
    [Range(128, 3072)] public int Dimensions { get; set; } = 768;
    [Range(1, 100)] public int MaxBatchSize { get; set; } = 100;
    [Required, Url] public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
}

public sealed class GroqOptions
{
    public const string SectionName = "Groq";

    [Required] public string ApiKey { get; set; } = string.Empty;
    [Required] public string Model { get; set; } = "openai/gpt-oss-120b";
    [Required, Url] public string Endpoint { get; set; } = "https://api.groq.com/openai/v1";
}
