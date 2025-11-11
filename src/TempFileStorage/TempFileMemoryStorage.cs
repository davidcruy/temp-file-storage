using Microsoft.Extensions.DependencyInjection;

namespace TempFileStorage;

public static class TempFileStorageProviderConfigurationExtensions
{
    public static TempFileStorageOptions MemoryStorage(this TempFileStorageOptions configuration)
    {
        configuration.ConfigureAction = services => { services.AddSingleton<ITempFileStorage, TempFileMemoryStorage>(); };

        return configuration;
    }
}

public class TempFileMemoryStorage(TempFileStorageOptions options) : TempFileStorage(options)
{
    private readonly IDictionary<string, (TempFile FileInfo, byte[] Content)> _files = new Dictionary<string, (TempFile, byte[])>();

    public override Task<byte[]> GetContent(string key, CancellationToken token = default)
    {
        return Task.FromResult(_files[key].Content);
    }

    public override Task<Stream> GetContentStream(string key)
    {
        var content = _files[key].Content;
        var stream = new MemoryStream(content);

        return Task.FromResult<Stream>(stream);
    }

    public override Task<bool> Remove(string key)
    {
        var removed = _files.Remove(key);

        return Task.FromResult(removed);
    }

    public override async Task<TempFile> StoreFile(string filename, Stream contentStream, TimeSpan timeout, bool isUpload = false, bool deleteOnDownload = true, CancellationToken token = default)
    {
        var memStream = new MemoryStream();
        await contentStream.CopyToAsync(memStream, token);

        var content = memStream.ToArray();
        var fileSize = content.Length;

        var file = new TempFile
        {
            Filename = filename,
            FileSize = fileSize,
            IsUpload = isUpload,
            DeleteOnDownload = deleteOnDownload,
            CacheTimeout = DateTime.Now.Add(timeout)
        };

        _files.Add(file.Key, (file, content));

        return file;
    }

    public override Task<TempFile> GetFileInfo(string key, bool filterUpload = false)
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

    public override Task CleanupStorage(CancellationToken token = default)
    {
        var toRemove = _files.Where(x => x.Value.FileInfo.CacheTimeout < DateTime.UtcNow).ToList();

        foreach (var remove in toRemove)
        {
            _files.Remove(remove);
        }

        return Task.CompletedTask;
    }
}