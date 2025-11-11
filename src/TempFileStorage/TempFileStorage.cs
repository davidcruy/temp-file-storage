namespace TempFileStorage;

public abstract class TempFileStorage(TempFileStorageOptions options) : ITempFileStorage
{
    public TempFileStorageOptions Options { get; } = options;

    public virtual Task<TempFile> StoreFile(string filename, byte[] content, bool isUpload = false, bool deleteOnDownload = true, CancellationToken token = default)
    {
        return StoreFile(filename, new MemoryStream(content), isUpload, deleteOnDownload, token);
    }

    public virtual Task<TempFile> StoreFile(string filename, Stream contentStream, bool isUpload = false, bool deleteOnDownload = true, CancellationToken token = default)
    {
        return StoreFile(filename, contentStream, TimeSpan.FromMinutes(Options.DefaultTimeout), isUpload, deleteOnDownload, token);
    }

    public abstract Task<TempFile> StoreFile(string filename, Stream contentStream, TimeSpan timeout, bool isUpload = false, bool deleteOnDownload = true, CancellationToken token = default);

    public virtual async Task<bool> ContainsKey(string key, bool filterUpload = false)
    {
        var fileInfo = await GetFileInfo(key, filterUpload);
        return fileInfo != null;
    }

    public abstract Task<TempFile> GetFileInfo(string key, bool filterUpload = false);

    public abstract Task<byte[]> GetContent(string key, CancellationToken token = default);

    public abstract Task<Stream> GetContentStream(string key);

    public abstract Task<bool> Remove(string key);

    public abstract Task CleanupStorage(CancellationToken token = default);
}