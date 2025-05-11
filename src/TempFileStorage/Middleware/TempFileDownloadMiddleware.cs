using Microsoft.AspNetCore.Http;

namespace TempFileStorage.Middleware;

internal class TempFileDownloadMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITempFileStorage storage)
    {
        if (context.Request.Method.Equals("GET"))
        {
            if (!context.Request.Query.TryGetValue("key", out var fileKeys))
            {
                throw new ArgumentException("TempFileStorage download key is required.");
            }
            if (!await storage.ContainsKey(fileKeys[0], filterUpload: true))
            {
                throw new InvalidOperationException("TempFileStorage download key is invalid.");
            }

            var fileInfo = await storage.GetFileInfo(fileKeys[0]);
            var content = await storage.Download(fileKeys[0]);

            context.Response.ContentType = "application/octet-stream";
            context.Response.Headers.Append("content-disposition", new[] { $"attachment;filename=\"{fileInfo.Filename}\"" });
            context.Response.ContentLength = content.Length;

            await context.Response.Body.WriteAsync(content);
            return;
        }

        await next(context);
    }
}