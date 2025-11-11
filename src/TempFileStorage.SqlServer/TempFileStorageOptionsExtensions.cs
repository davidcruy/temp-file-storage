using Microsoft.Extensions.DependencyInjection;
using TempFileStorage.SqlServer;

namespace TempFileStorage;

public static class TempFileStorageOptionsExtensions
{
    /// <summary>
    /// Temporary files will be held in a database
    /// </summary>
    public static TempFileStorageOptions SqlServer(this TempFileStorageOptions configuration, string connectionString)
    {
        configuration.ConfigureAction = services =>
        {
            services.AddScoped<ITempFileStorage>(_ => new TempFileSqlStorage(connectionString, configuration));
        };

        return configuration;
    }
}