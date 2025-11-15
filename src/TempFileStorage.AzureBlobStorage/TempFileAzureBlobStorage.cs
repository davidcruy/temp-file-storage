using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace TempFileStorage.AzureBlobStorage;

internal class TempFileAzureBlobStorage : TempFileStorage
{
    private readonly BlobContainerClient _containerClient;
    private readonly Lazy<Task> _ensureContainerExists;

    // Metadata keys
    private const string MetaKeyFilename = "tfs_filename";
    private const string MetaKeyCacheTimeout = "tfs_cacheTimeout";
    private const string MetaKeyIsUpload = "tfs_isUpload";
    private const string MetaKeyDeleteOnDownload = "tfs_deleteOnDownload";

    private async Task EnsureContainerAsync() => await _ensureContainerExists.Value;

    /// <summary>
    /// Creates a new instance using a connection string.
    /// </summary>
    public TempFileAzureBlobStorage(string connectionString, string containerName, TempFileStorageOptions options) : base(options)
    {
        _containerClient = new BlobContainerClient(connectionString, containerName);
        _ensureContainerExists = new Lazy<Task>(() => _containerClient.CreateIfNotExistsAsync());
    }

    public override async Task<TempFile> StoreFile(string filename, Stream contentStream, TimeSpan timeout, bool isUpload = false, bool deleteOnDownload = true, CancellationToken token = default)
    {
        await EnsureContainerAsync();

        var tempFile = new TempFile
        {
            Filename = filename,
            CacheTimeout = DateTime.UtcNow.Add(timeout),
            IsUpload = isUpload,
            DeleteOnDownload = deleteOnDownload
        };

        // Create metadata for the blob
        var metadata = new Dictionary<string, string>
        {
            [MetaKeyFilename] = filename,
            [MetaKeyIsUpload] = isUpload.ToString(),
            [MetaKeyDeleteOnDownload] = deleteOnDownload.ToString(),
            // Use "o" round-trip format for reliable DateTime parsing
            [MetaKeyCacheTimeout] = tempFile.CacheTimeout.ToString("o")
        };

        var options = new BlobUploadOptions
        {
            Metadata = metadata
        };

        var blobClient = _containerClient.GetBlobClient(tempFile.Key);
        await blobClient.UploadAsync(contentStream, options, token);
        var properties = await blobClient.GetPropertiesAsync(new BlobRequestConditions(), token);

        tempFile.FileSize = properties.Value.ContentLength;

        return tempFile;
    }

    public override async Task<bool> ContainsKey(string key, bool filterUpload = false)
    {
        // This is more efficient than GetFileInfo, as it avoids a separate ExistsAsync call.
        var fileInfo = await GetFileInfo(key, filterUpload);

        return fileInfo != null;
    }

    public override async Task<TempFile> GetFileInfo(string key, bool filterUpload = false)
    {
        await EnsureContainerAsync();

        var blobClient = _containerClient.GetBlobClient(key);

        Response<BlobProperties> properties;
        try
        {
            properties = await blobClient.GetPropertiesAsync();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null; // Blob not found
        }

        var metadata = properties.Value.Metadata;

        // Check for required metadata
        if (!metadata.TryGetValue(MetaKeyCacheTimeout, out var timeoutString) ||
            !metadata.TryGetValue(MetaKeyIsUpload, out var isUploadString) ||
            !metadata.TryGetValue(MetaKeyDeleteOnDownload, out var deleteOnDownloadString) ||
            !metadata.TryGetValue(MetaKeyFilename, out var filename))
        {
            // This is a blob that wasn't created by this service, or is corrupt.
            return null;
        }

        // Check if expired
        if (!DateTime.TryParse(timeoutString, out var cacheTimeout) || cacheTimeout < DateTime.UtcNow)
            return null;

        // Check upload filter
        if (!bool.TryParse(isUploadString, out var isUpload) || (filterUpload && isUpload))
            return null; // Filtered out

        if (!bool.TryParse(deleteOnDownloadString, out var deleteOnDownload))
            return null;

        return new TempFile(key)
        {
            Filename = filename,
            FileSize = properties.Value.ContentLength,
            IsUpload = isUpload,
            DeleteOnDownload = deleteOnDownload,
            CacheTimeout = cacheTimeout
        };
    }

    public override async Task<byte[]> GetContent(string key, CancellationToken token = default)
    {
        await EnsureContainerAsync();

        var blobClient = _containerClient.GetBlobClient(key);

        try
        {
            // DownloadAsync returns the content in a stream
            BlobDownloadInfo download = await blobClient.DownloadAsync(token);

            await using var ms = new MemoryStream();
            await download.Content.CopyToAsync(ms, token);
            return ms.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null; // Not found
        }
    }

    public override async Task<Stream> GetContentStream(string key)
    {
        await EnsureContainerAsync();

        var blobClient = _containerClient.GetBlobClient(key);

        try
        {
            // DownloadAsync returns the content in a stream
            BlobDownloadInfo download = await blobClient.DownloadAsync();

            return download.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null; // Not found
        }
    }

    public override async Task<bool> Remove(string key)
    {
        await EnsureContainerAsync();
        var blobClient = _containerClient.GetBlobClient(key);
        var response = await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

        return response.Value;
    }

    /// <summary>
    /// Deletes expired blobs from the container
    /// </summary>
    public override async Task CleanupStorage(CancellationToken token = default)
    {
        await EnsureContainerAsync();

        // Get all blobs with their metadata
        await foreach (var blobItem in _containerClient.GetBlobsAsync(BlobTraits.Metadata, cancellationToken: token))
        {
            // Check if blob has timeout metadata
            if (!blobItem.Metadata.TryGetValue(MetaKeyCacheTimeout, out var timeoutString) || !DateTime.TryParse(timeoutString, out var cacheTimeout))
                continue;

            // If expired, delete it
            if (cacheTimeout >= DateTime.UtcNow)
                continue;

            var blobClient = _containerClient.GetBlobClient(blobItem.Name);
            await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: token);
        }
    }
}