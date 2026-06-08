namespace Logis.Models;

/// <summary>
/// Represents a structured request sent to an LLM provider.
/// </summary>
public record LogisRequest(
    List<LogisMessage> Messages,
    double Temperature,
    int MaxTokens
);

/// <summary>
/// Represents a single message in a chat conversation.
/// </summary>
public record LogisMessage(
    string Role,
    string Content
);

/// <summary>
/// Represents the raw response structure expected from provider-neutral mapping.
/// </summary>
public record LogisResponse(
    List<LogisChoice> Choices,
    LogisUsage Usage
);

/// <summary>
/// Represents a single completion choice returned by the model.
/// </summary>
public record LogisChoice(
    LogisMessage Message,
    string FinishReason
);

/// <summary>
/// Captures token consumption metadata for auditing and billing visibility.
/// </summary>
public record LogisUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens
);
