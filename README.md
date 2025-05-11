TempFileStorage
===============

Easy .NET standard library for handling file-uploads

Just use ITempFileStorage to store your file during uploads, this will return a key for later use, for when you want to save your form.

Core package comes with In-Memory storage that is usefull for testing or non-multi server setups.

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
        // Temporary files will be held in memory
        options.MemoryStorage();

        // The interval (in minutes) that will perform a cleanup of all temporary files (default is 15)
        options.CleanupInterval = 15;
    });
```

Register the Middleware in your Program.cs to activate the request-middleware:

```C#
// Map the download-middleware with a specific pattern (default is "/download-file")
app.MapTempFileDownload("/download-file");

// Map the upload-middleware with a specific pattern (default is "/upload-file")
app.MapTempFileUpload("/upload-file");
```

### SqlServer

You need to persist your temp file storage in a database if you want to use this in production.

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
