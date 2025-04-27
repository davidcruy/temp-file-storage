namespace TempFileStorage;

public class TempFileStorageOptions
{
    /// <summary>
    /// Gets or sets the path for downloading a file. (default: "/download-file")
    /// </summary>
    public string DownloadFilePattern { get; set; } = "/download-file";

    /// <summary>
    /// Gets or sets the path for file upload. (default: "/upload-file")
    /// </summary>
    public string UploadFilePattern { get; set; } = "/upload-file";
}