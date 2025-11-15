TempFileStorage
===============

Easy .NET library for handling file asynchronous uploads and downloads.

Let the client upload a file via ´/upload-file´, send the file-key when you are submitting the form, and retrieve the file from ´ITempFileStorage´ in your controller.
Or, generate a file in your backend, store the file in ´ITempFileStorage´ and send the key to the client. They can fetch it trough ´/download-file´.

Core package comes with In-Memory storage that is useful for testing. You can extend with SqlServer storage or Azure Blob storage.

### Installing TempFileStorage

You should install [TempFileStorage with NuGet](https://www.nuget.org/packages/TempFileStorage):

    Install-Package TempFileStorage

Or via the .NET Core command line interface:

    dotnet add package TempFileStorage

### Usage

Add temp files to your project:

```C#
builder.Services
    .AddTempFiles(options =>
    {
        // Temporary files will be held in memory, this should only be used for testing
        options.MemoryStorage();

        // The interval (in minutes) that will perform a cleanup of all temporary files (default is 15)
        options.CleanupInterval = 15;

        // The maximum file size (in bytes) that can be uploaded by the client (default is 50MB)
        options.MaxFileSize = 1024 * 1024 * 50;

        // The timeout (in minutes) for how long a file remains in the storage (default is 30)
        options.DefaultTimeout = 30;
    });
```

Register the Middleware in your Program.cs to activate the request-middleware:

```C#
// Map the download-middleware with a specific pattern (default is "/download-file")
app.MapTempFileDownload("/download-file");

// Map the upload-middleware with a specific pattern (default is "/upload-file")
app.MapTempFileUpload("/upload-file");
```

### Azure.Storage.Blobs

You need to persist your temp file storage in storage blobs if you want to use this in production.

Install the package [TempFileStorage.AzureBlobStorage with NuGet](https://www.nuget.org/packages/TempFileStorage.AzureBlobStorage):

Swap the `MemoryStorage` with `AzureBlobStorage`

```C#
builder.Services
    .AddTempFiles(options =>
    {
        // Temporary files will be held Azure blob storage container (default is "temp-file-storage")
        options.AzureBlobStorage(builder.Configuration.GetConnectionString("StorageAccount"), containerName: "temp-file-storage");
    });
```

### SqlServer

Or, when you're not working in Azure, you can opt for SqlServer storage.

Install the package [TempFileStorage.SqlServer with NuGet](https://www.nuget.org/packages/TempFileStorage.SqlServer):

Run the SQL-script `install.sql` on your DB-server.

Swap the `MemoryStorage` with `SqlServer`

```C#
builder.Services
    .AddTempFiles(options =>
    {
        // Temporary files will be held in database
        options.SqlServer(builder.Configuration.GetConnectionString("Database"));
    });
```
