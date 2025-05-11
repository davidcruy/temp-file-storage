using Microsoft.Extensions.DependencyInjection;
using TempFileStorage.SqlServer;

namespace TempFileStorage;

public static class TempFileStorageOptionsExtensions
{
    public static TempFileStorageOptions SqlServer(this TempFileStorageOptions configuration, string connectionString)
    {
        configuration.ConfigureAction = services =>
        {
            services.AddScoped<ITempFileStorage>(_ => new TempFileSqlStorage(connectionString));
        };

        return configuration;
    }
}