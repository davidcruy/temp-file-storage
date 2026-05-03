using System.Net;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TempFileStorage.Middleware;

internal class TempFileDownloadMiddleware(RequestDelegate next, ILogger<TempFileDownloadMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITempFileStorage storage)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (!context.Request.Query.TryGetValue("key", out var fileKeys))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("TempFileStorage download key is required.");
            return;
        }

        var key = fileKeys[0];

        if (!await storage.ContainsKey(key, filterUpload: true))
        {
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return;
        }

        logger.LogDebug("Download file by key {Key}", key);
        var fileInfo = await storage.GetFileInfo(key);

        var contentDisposition = new ContentDispositionHeaderValue("attachment");
        contentDisposition.SetHttpFileName(fileInfo.Filename);

        await using (var contentStream = await storage.GetContentStream(key))
        {
            context.Response.ContentType = "application/octet-stream";
            context.Response.Headers.Append(HeaderNames.ContentDisposition, contentDisposition.ToString());
            context.Response.ContentLength = fileInfo.FileSize;

            await contentStream.CopyToAsync(context.Response.Body, 81920, context.RequestAborted);
        }

        HandleFileDeletionAfterDownload(context, storage, fileInfo, key);
    }

    private void HandleFileDeletionAfterDownload(HttpContext context, ITempFileStorage storage, TempFile fileInfo, string key)
    {
        if (!fileInfo.DeleteOnDownload)
            return;

        context.Response.OnCompleted(() =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogDebug("Deleting file after download {Key}", key);
                    await storage.Remove(key);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unknown exception during delete after download {Key}", key);
                }
            });

            return Task.CompletedTask;
        });
    }
}