using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace TempFileStorage.SqlServer;

public class TempFileSqlStorage(string connectionString) : ITempFileStorage
{
    public Task<TempFile> StoreFile(string filename, byte[] content, bool isUpload = false)
    {
        return StoreFile(filename, new MemoryStream(content), isUpload);
    }

    public Task<TempFile> StoreFile(string filename, Stream contentStream, bool isUpload = false) => StoreFile(filename, contentStream, TimeSpan.FromMinutes(30), isUpload);

    public async Task<TempFile> StoreFile(string filename, Stream contentStream, TimeSpan timeout, bool isUpload = false)
    {
        var tempFile = new TempFile
        {
            Filename = filename,
            CacheTimeout = DateTime.UtcNow.Add(timeout)
        };

        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync();
        await using (var cmd = new SqlCommand("INSERT INTO [TempFileStorage] ([Key], Filename, FileSize, IsUpload, CacheTimeout, Content) VALUES (@key, @filename, @fileSize, @isUpload, @cacheTimeout, @content)", connection))
        {
            cmd.CommandTimeout = 600;

            cmd.Parameters.Add("@key", SqlDbType.NVarChar).Value = tempFile.Key;
            cmd.Parameters.Add("@filename", SqlDbType.NVarChar).Value = filename;
            cmd.Parameters.Add("@isUpload", SqlDbType.Bit).Value = isUpload;
            cmd.Parameters.Add("@fileSize", SqlDbType.BigInt).Value = contentStream.Length;
            cmd.Parameters.Add("@cacheTimeout", SqlDbType.DateTime).Value = tempFile.CacheTimeout;

            // Add a parameter which uses the FileStream we just opened
            // Size is set to -1 to indicate "MAX"
            cmd.Parameters.Add("@content", SqlDbType.Binary, -1).Value = contentStream;

            // Send the data to the server asynchronously  
            await cmd.ExecuteNonQueryAsync();

            tempFile.FileSize = contentStream.Length;
        }

        await CleanupStorage(connection);

        return tempFile;
    }

    public async Task<bool> ContainsKey(string key, bool filterUpload)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = filterUpload
            ? "SELECT COUNT(*) FROM [TempFileStorage] WHERE [Key] = @key AND [CacheTimeout] > @timeout AND [IsUpload] = 0"
            : "SELECT COUNT(*) FROM [TempFileStorage] WHERE [Key] = @key AND [CacheTimeout] > @timeout";
        await using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("key", key);
        cmd.Parameters.AddWithValue("timeout", DateTime.UtcNow);

        var count = (int?) await cmd.ExecuteScalarAsync();

        await CleanupStorage(connection);

        return count is 1;
    }

    public async Task<TempFile> GetFileInfo(string key, bool filterUpload)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = filterUpload
            ? "SELECT [Filename], [FileSize], [IsUpload], [CacheTimeout] FROM [TempFileStorage] WHERE [Key] = @key AND [CacheTimeout] > @timeout AND [IsUpload] = 0"
            : "SELECT [Filename], [FileSize], [IsUpload], [CacheTimeout] FROM [TempFileStorage] WHERE [Key] = @key AND [CacheTimeout] > @timeout";
        await using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("key", key);
        cmd.Parameters.AddWithValue("timeout", DateTime.UtcNow);

        var reader = await cmd.ExecuteReaderAsync();

        TempFile tempFile = null;

        while (await reader.ReadAsync())
        {
            var filename = reader.GetString(0);
            var fileSize = reader.GetInt64(1);
            var isUpload = reader.GetBoolean(2);
            var cacheTimeout = reader.GetDateTime(3);

            tempFile = new TempFile(key)
            {
                CacheTimeout = cacheTimeout,
                Filename = filename,
                FileSize = fileSize,
                IsUpload = isUpload
            };
        }

        await reader.CloseAsync();
        await CleanupStorage(connection);

        return tempFile;
    }

    public async Task<byte[]> Download(string key)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT [Content] FROM [TempFileStorage] WHERE [Key] = @key", connection);
        command.CommandTimeout = 600;

        command.Parameters.AddWithValue("key", key);

        byte[] content = null;

        // The reader needs to be executed with the SequentialAccess behavior to enable network streaming  
        // Otherwise ReadAsync will buffer the entire BLOB into memory which can cause scalability issues or even OutOfMemoryExceptions  
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        if (await reader.ReadAsync())
        {
            content = (byte[])reader["Content"];
        }

        await reader.CloseAsync();
        await CleanupStorage(connection);

        return content;
    }

    private static async Task CleanupStorage(SqlConnection connection)
    {
        var timer = new Stopwatch();
        timer.Start();

        int deletedCount;
        bool running;

        do
        {
            try
            {
                var command = new SqlCommand("DELETE TOP(1) FROM [TempFileStorage] WHERE [CacheTimeout] < @timeout", connection);
                command.Parameters.AddWithValue("timeout", DateTime.UtcNow);
                command.CommandTimeout = 10; // Give a max timeout of 10 seconds for a single delete
                deletedCount = (int?) await command.ExecuteScalarAsync() ?? 0;
            }
            catch (TimeoutException)
            {
                throw new TempFileStorageException("Unable to cleanup temp SQL storage, command timeout exceeded");
            }

            // Stop the cleanup task if we are running longer than 5 seconds
            running = timer.ElapsedMilliseconds < 5000;

        } while (deletedCount > 0 && running);
    }
}