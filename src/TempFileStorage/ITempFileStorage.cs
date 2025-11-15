namespace TempFileStorage;

public interface ITempFileStorage
{
    TempFileStorageOptions Options { get; }

    Task<TempFile> StoreFile(string filename, byte[] content, bool isUpload = false, bool deleteOnDownload = true, CancellationToken token = default);
    Task<TempFile> StoreFile(string filename, Stream contentStream, bool isUpload = false, bool deleteOnDownload = true, CancellationToken token = default);
    Task<TempFile> StoreFile(string filename, Stream contentStream, TimeSpan timeout, bool isUpload = false, bool deleteOnDownload = true, CancellationToken token = default);

    Task<bool> ContainsKey(string key, bool filterUpload = false);
    Task<TempFile> GetFileInfo(string key, bool filterUpload = false);
    Task<byte[]> GetContent(string key, CancellationToken token = default);
    Task<Stream> GetContentStream(string key);
    Task<bool> Remove(string key);

    Task CleanupStorage(CancellationToken token = default);
}