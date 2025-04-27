using Microsoft.AspNetCore.Builder;

namespace TempFileStorage;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseTempFiles(this IApplicationBuilder builder, Action<ITempFileStorageBuilder> extraOptions = null)
    {
        var customBuilder = new TempFileStorageBuilder(builder, new TempFileStorageOptions());
        extraOptions?.Invoke(customBuilder);

        return builder
            .Map(customBuilder.Options.DownloadFilePattern, applicationBuilder =>
            {
                applicationBuilder.UseMiddleware<TempFileDownloadMiddleware>();
            })
            .Map(customBuilder.Options.UploadFilePattern, applicationBuilder =>
            {
                applicationBuilder.UseMiddleware<TempFileUploadMiddleware>();
            });
    }
}