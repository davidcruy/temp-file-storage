using Microsoft.Extensions.DependencyInjection;

namespace TempFileStorage;

public static class TempFileStorageProviderConfigurationExtensions
{
    public static TempFileStorageOptions MemoryStorage(this TempFileStorageOptions configuration)
    {
        configuration.ConfigureAction = services =>
        {
            services.AddSingleton<ITempFileStorage, TempFileMemoryStorage>();
        };

        return configuration;
    }
}

public class TempFileMemoryStorage : ITempFileStorage
{
    private readonly IDictionary<string, (TempFile FileInfo, byte[] Content)> _files = new Dictionary<string, (TempFile, byte[])>();

    public Task<TempFile> StoreFile(string filename, byte[] content, bool isUpload = false)
    {
        return StoreFile(filename, new MemoryStream(content));
    }

    public Task<TempFile> StoreFile(string filename, Stream contentStream, bool isUpload = false) => StoreFile(filename, contentStream, TimeSpan.FromMinutes(30), isUpload);

    public Task<byte[]> Download(string key)
    {
        return Task.FromResult(_files[key].Content);
    }

    public async Task<TempFile> StoreFile(string filename, Stream contentStream, TimeSpan timeout, bool isUpload = false)
    {
        var memStream = new MemoryStream();
        await contentStream.CopyToAsync(memStream);

        var content = memStream.ToArray();
        var fileSize = content.Length;

        var file = new TempFile
        {
            Filename = filename,
            FileSize = fileSize,
            IsUpload = isUpload,
            CacheTimeout = DateTime.Now.Add(timeout)
        };

        _files.Add(file.Key, (file, content));

        return file;
    }

    public Task<bool> ContainsKey(string key, bool filterUpload = false)
    {
        return filterUpload
            ? Task.FromResult(_files.Values.Any(f => f.FileInfo.Key == key && f.FileInfo.IsUpload == false))
            : Task.FromResult(_files.ContainsKey(key));
    }

    public Task<TempFile> GetFileInfo(string key, bool filterUpload = false)
    {
        var fileInfo = _files.TryGetValue(key, out var storedFile)
            ? storedFile.FileInfo
            : null;

        if (filterUpload && fileInfo is { IsUpload: true })
        {
            return Task.FromResult<TempFile>(null);
        }

        return Task.FromResult(fileInfo);
    }

    public Task CleanupStorage(CancellationToken cancellationToken)
    {
        // Do nothing
        return Task.CompletedTask;
    }
}