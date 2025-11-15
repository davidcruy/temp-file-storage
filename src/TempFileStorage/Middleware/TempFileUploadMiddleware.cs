using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace TempFileStorage.Middleware;

internal class TempFileUploadMiddleware(RequestDelegate next, ILogger<TempFileUploadMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITempFileStorage storage)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            await next(context);
            return;
        }

        var multipartBoundary = context.Request.GetMultipartBoundary();
        if (string.IsNullOrEmpty(multipartBoundary))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync($"Expected a multipart request, but got '{context.Request.ContentType}'.");
            return;
        }

        // Used to accumulate all the form url encoded key value pairs in the request
        var files = new List<FileInfo>();
        var reader = new MultipartReader(multipartBoundary, context.Request.Body)
        {
            BodyLengthLimit = storage.Options.MaxFileSize
        };
        var cancellation = context.RequestAborted;

        var section = await reader.ReadNextSectionAsync(cancellation);
        while (section != null)
        {
            // This will reparse the content disposition header
            // Create a FileMultipartSection using its constructor to pass
            // in a cached disposition header
            var fileSection = section.AsFileSection();
            if (fileSection != null)
            {
                var fileName = fileSection.FileName;

                try
                {
                    var tempFile = await storage.StoreFile(fileName, fileSection.FileStream, isUpload: true, deleteOnDownload: false, token: cancellation);

                    files.Add(new FileInfo
                    {
                        FileName = fileName,
                        FileSize = tempFile.FileSize,
                        Key = tempFile.Key
                    });
                }
                catch (InvalidDataException ex) when (ex.Message.Contains("Multipart body length limit"))
                {
                    // This is the exception thrown by the reader
                    logger.LogWarning(ex, "File upload exceeded size limit of {MaxFileSize} bytes", storage.Options.MaxFileSize);

                    context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
                    await context.Response.WriteAsync($"File size exceeds maximum of {storage.Options.MaxFileSize} bytes.", cancellationToken: cancellation);
                    return;
                }
            }

            // Drains any remaining section body that has not been consumed and
            // reads the headers for the next section.
            section = await reader.ReadNextSectionAsync(cancellation);
        }

        context.Response.StatusCode = StatusCodes.Status201Created;
        context.Response.ContentType = "application/json";

        // Transform keys to JSON array
        var responseJson = JsonSerializer.Serialize(files, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(responseJson, cancellation);
    }

    /// <summary>
    /// Used for JSON-serialization
    /// </summary>
    private class FileInfo
    {
        public string Key { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
    }
}