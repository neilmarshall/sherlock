using Azure;
using Azure.Data.Tables;

namespace SherlockHolmes.Api.Models;

public class StoryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "story";
    public string RowKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Collection { get; set; } = "";
    public int YearPublished { get; set; }
    public int WordCount { get; set; }
    public string BlobName { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
