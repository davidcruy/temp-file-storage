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

Add the following service to the container:

```
services.AddSingleton<ITempFileStorage, TempFileMemoryStorage>();
```

Register the Middleware in your Startup.cs to activate the request-middleware:

```
app.UseTempFiles(builder =>
{
    builder.Options.UploadFilePath = "/upload-file";
    builder.Options.DownloadFilePath = "/download-file";
});
```
