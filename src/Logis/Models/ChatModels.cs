namespace Logis.Models;

public record ChatRequest(
    List<ChatMessage> Messages,
    double Temperature,
    int MaxTokens
);

public record ChatMessage(
    string Role,
    string Content
);

public record ChatResponse(
    List<Choice> Choices,
    Usage Usage
);

public record Choice(
    ChatMessage Message,
    string FinishReason
);

public record Usage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens
);
