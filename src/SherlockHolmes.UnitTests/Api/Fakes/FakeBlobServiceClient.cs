using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SherlockHolmes.UnitTests.Api.Fakes;

/// <summary>
/// In-memory stand-in for the Azure Blob service with a single container. Missing blobs
/// raise a 404 RequestFailedException, mirroring the real SDK.
/// </summary>
internal sealed class FakeBlobServiceClient : BlobServiceClient
{
    private readonly FakeBlobContainerClient _container;

    public FakeBlobServiceClient(IReadOnlyDictionary<string, string> blobs)
    {
        _container = new FakeBlobContainerClient(blobs);
    }

    public string? RequestedContainerName { get; private set; }

    public override BlobContainerClient GetBlobContainerClient(string blobContainerName)
    {
        RequestedContainerName = blobContainerName;
        return _container;
    }
}

internal sealed class FakeBlobContainerClient : BlobContainerClient
{
    private readonly IReadOnlyDictionary<string, string> _blobs;

    public FakeBlobContainerClient(IReadOnlyDictionary<string, string> blobs)
    {
        _blobs = blobs;
    }

    public override BlobClient GetBlobClient(string blobName)
        => new FakeBlobClient(blobName, _blobs);
}

internal sealed class FakeBlobClient : BlobClient
{
    private readonly string _blobName;
    private readonly IReadOnlyDictionary<string, string> _blobs;

    public FakeBlobClient(string blobName, IReadOnlyDictionary<string, string> blobs)
    {
        _blobName = blobName;
        _blobs = blobs;
    }

    public override Task<Response<BlobDownloadResult>> DownloadContentAsync(CancellationToken cancellationToken = default)
    {
        if (!_blobs.TryGetValue(_blobName, out var text))
        {
            throw new RequestFailedException(404, "The specified blob does not exist.", "BlobNotFound", null);
        }

        var result = BlobsModelFactory.BlobDownloadResult(content: BinaryData.FromString(text));
        return Task.FromResult(Response.FromValue(result, null!));
    }
}
