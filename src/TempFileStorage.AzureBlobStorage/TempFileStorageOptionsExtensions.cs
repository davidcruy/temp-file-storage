using Azure.Core;
using Azure.Storage.Blobs;
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
        return configuration.AzureBlobStorage(new BlobContainerClient(connectionString, containerName));
    }

    /// <summary>
    /// Temporary files will be held in an Azure blob storage container (default is "temp-file-storage"),
    /// authenticating with a token credential (e.g. DefaultAzureCredential / managed identity).
    /// </summary>
    public static TempFileStorageOptions AzureBlobStorage(this TempFileStorageOptions configuration, Uri serviceUri, TokenCredential credential, string containerName = "temp-file-storage")
    {
        var containerClient = new BlobServiceClient(serviceUri, credential).GetBlobContainerClient(containerName);

        return configuration.AzureBlobStorage(containerClient);
    }

    /// <summary>
    /// Temporary files will be held in the Azure blob storage container of the given pre-built client, allowing any authentication mode.
    /// </summary>
    public static TempFileStorageOptions AzureBlobStorage(this TempFileStorageOptions configuration, BlobContainerClient containerClient)
    {
        configuration.ConfigureAction = services =>
        {
            services.AddScoped<ITempFileStorage>(_ => new TempFileAzureBlobStorage(containerClient, configuration));
        };

        return configuration;
    }
}