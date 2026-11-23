namespace SmartSupport.Application.Chat;

/// <summary>Tuning for retrieval and answering. Bound from the "Rag" configuration section; all values have defaults.</summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    /// <summary>How many document chunks to retrieve per question.</summary>
    public int SearchLimit { get; set; } = 4;

    /// <summary>How many earlier messages of a chat the model sees (5 questions + 5 answers).</summary>
    public int MemoryMessageCount { get; set; } = 10;

    /// <summary>Earlier messages are cut to this length before being sent to the model.</summary>
    public int MemoryMessageMaxLength { get; set; } = 2000;

    /// <summary>Sources are only shown when the best match scores at least this.</summary>
    public float MinSourceScore { get; set; } = 0.5f;

    /// <summary>Other sources are shown only if they score within this distance of the best match.</summary>
    public float MaxSourceGapFromBest { get; set; } = 0.08f;

    /// <summary>Length of the passage shown under a source chip.</summary>
    public int SnippetLength { get; set; } = 280;

    /// <summary>Longest question a user may send.</summary>
    public int MaxQuestionLength { get; set; } = 2000;
}
