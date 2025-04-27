using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace TempFileStorage;

public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Enable TempFileStorage middleware to download files.
    /// Default patterns:
    /// - /download-file
    /// </summary>
    public static IEndpointConventionBuilder MapTempFileDownload(this IEndpointRouteBuilder endpoints, string downloadPattern = "/download-file")
    {
        var app = endpoints.CreateApplicationBuilder();

        var downloadPipeline = app
            .UseMiddleware<TempFileDownloadMiddleware>()
            .Build();

        return endpoints.Map(downloadPattern, downloadPipeline);
    }

    /// <summary>
    /// Enable TempFileStorage middleware to upload files.
    /// Default patterns:
    /// - /upload-file
    /// </summary>
    public static IEndpointConventionBuilder MapTempFileUpload(this IEndpointRouteBuilder endpoints, string uploadPattern = "/upload-file")
    {
        var app = endpoints.CreateApplicationBuilder();

        var uploadPipeline = app
            .UseMiddleware<TempFileUploadMiddleware>()
            .Build();

        return endpoints.Map(uploadPattern, uploadPipeline);
    }
}