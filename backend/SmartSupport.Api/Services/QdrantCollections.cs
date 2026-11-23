namespace SmartSupport.Api.Services;

public static class QdrantCollections
{
    // Gemini vectors (768 dims). The old "knowledge_base" collection holds 384-dim HuggingFace vectors.
    public const string KnowledgeBase = "knowledge_base_gemini";
}
