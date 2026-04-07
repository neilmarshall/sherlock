namespace SherlockHolmes.Api.Models;

public record StoryMetadataResponse(
    string Id,
    string Title,
    string Collection,
    int YearPublished,
    int WordCount);
