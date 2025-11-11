using Microsoft.Extensions.DependencyInjection;
using TempFileStorage.AzureBlobStorage;

namespace TempFileStorage;

public static class TempFileStorageOptionsExtensions
{
    /// <summary>
    /// Temporary files will be held Azure blob storage container (default is "temp-file-storage")
    /// </summary>
    public static TempFileStorageOptions AzureBlobStorage(this TempFileStorageOptions configuration, string connectionString, string containerName = "temp-file-storage")
    {
        configuration.ConfigureAction = services =>
        {
            services.AddScoped<ITempFileStorage>(_ => new TempFileAzureBlobStorage(connectionString, containerName, configuration));
        };

        return configuration;
    }
}