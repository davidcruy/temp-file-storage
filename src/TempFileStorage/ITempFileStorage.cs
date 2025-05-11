namespace TempFileStorage;

public interface ITempFileStorage
{
    Task<TempFile> StoreFile(string filename, byte[] content, bool isUpload = false);
    Task<TempFile> StoreFile(string filename, Stream contentStream, bool isUpload = false);
    Task<TempFile> StoreFile(string filename, Stream contentStream, TimeSpan timeout, bool isUpload = false);

    Task<bool> ContainsKey(string key, bool filterUpload = false);
    Task<TempFile> GetFileInfo(string key, bool filterUpload = false);
    Task<byte[]> Download(string key);

    Task CleanupStorage(CancellationToken cancellationToken);
}