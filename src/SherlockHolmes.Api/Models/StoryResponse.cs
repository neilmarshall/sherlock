namespace SherlockHolmes.Api.Models;

public record StoryResponse(
    string Id,
    string Title,
    string Collection,
    int YearPublished,
    int WordCount,
    string Body);
