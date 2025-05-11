using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("TempFileStorage.SqlServer")]

namespace TempFileStorage;

public class TempFileStorageOptions
{
    /// <summary>
    /// Gets or sets the interval (in minutes) in which the temp file storage will remove all the unused files 
    /// </summary>
    public int CleanupInterval { get; set; } = 15;

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

        services.AddHostedService<BackgroundCleanupHostedService>(scope =>
            new BackgroundCleanupHostedService(
                TimeSpan.FromMinutes(options.CleanupInterval),
                scope.GetService<IServiceScopeFactory>(),
                scope.GetService<ILogger<BackgroundCleanupHostedService>>()
            )
        );

        return services;
    }
}