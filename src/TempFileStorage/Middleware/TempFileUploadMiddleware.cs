using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace TempFileStorage.Middleware;

internal class TempFileUploadMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITempFileStorage storage)
    {
        if (context.Request.Method.Equals("POST"))
        {
            // based on:
            // https://github.com/aspnet/Entropy/blob/rel/1.1.0/samples/Mvc.FileUpload/Controllers/FileController.cs#L47
            var multipartBoundary = context.Request.GetMultipartBoundary();
            if (string.IsNullOrEmpty(multipartBoundary))
            {
                throw new Exception($"Expected a multipart request, but got '{context.Request.ContentType}'.");
            }

            // Used to accumulate all the form url encoded key value pairs in the request.
            //var formAccumulator = new KeyValueAccumulator();
            //string targetFilePath = null;
            var files = new List<FileInfo>();
            var reader = new MultipartReader(multipartBoundary, context.Request.Body);

            var section = await reader.ReadNextSectionAsync();
            while (section != null)
            {
                // This will reparse the content disposition header
                // Create a FileMultipartSection using its constructor to pass
                // in a cached disposition header
                var fileSection = section.AsFileSection();
                if (fileSection != null)
                {
                    var fileName = fileSection.FileName;
                    var tempFile = await storage.StoreFile(fileName, fileSection.FileStream, isUpload: true);

                    files.Add(new FileInfo
                    {
                        FileName = fileName,
                        FileSize = tempFile.FileSize,
                        Key = tempFile.Key
                    });
                }

                // Drains any remaining section body that has not been consumed and
                // reads the headers for the next section.
                section = await reader.ReadNextSectionAsync();
            }

            // Transform keys to JSON array
            var responseJson = JsonSerializer.Serialize(files, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(responseJson);
            return;
        }

        await next(context);
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