using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("TempFileStorage.SqlServer")]
[assembly: InternalsVisibleTo("TempFileStorage.AzureBlobStorage")]

namespace TempFileStorage;

public class TempFileStorageOptions
{
    /// <summary>
    /// Gets or sets the interval (in minutes) in which the temp file storage will remove all the unused files 
    /// </summary>
    public int CleanupInterval { get; set; } = 15;

    /// <summary>
    /// Gets or sets the timeout (in minutes) for how long a file remains in the storage
    /// </summary>
    public int DefaultTimeout { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum file size (in bytes) that can be uploaded by the client
    /// </summary>
    public long MaxFileSize { get; set; } = 1024 * 1024 * 50;

    internal Action<IServiceCollection> ConfigureAction { get; set; }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTempFiles(this IServiceCollection services, Action<TempFileStorageOptions> optionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsAction);

        var options = new TempFileStorageOptions();
        optionsAction.Invoke(options);

        // Configures TempFileMemoryStorage or another provider
        options.ConfigureAction(services);

        // Add options to container
        services.AddSingleton(options);

        services.AddHostedService(scope => new BackgroundCleanupHostedService(
            TimeSpan.FromMinutes(options.CleanupInterval),
            scope.GetService<IServiceScopeFactory>(),
            scope.GetService<ILogger<BackgroundCleanupHostedService>>()
        ));

        return services;
    }
}