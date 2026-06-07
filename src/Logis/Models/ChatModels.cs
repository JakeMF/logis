namespace Logis.Models;

public record LogisRequest(
    List<LogisMessage> Messages,
    double Temperature,
    int MaxTokens
);

public record LogisMessage(
    string Role,
    string Content
);

public record LogisResponse(
    List<LogisChoice> Choices,
    LogisUsage Usage
);

public record LogisChoice(
    LogisMessage Message,
    string FinishReason
);

public record LogisUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens
);
