namespace SherlockHolmes.Api.Models;

public record ChatRequest(IReadOnlyList<ChatTurn> History, string Message);
