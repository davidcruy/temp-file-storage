using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace TempFileStorage.SqlServer;

internal class TempFileSqlStorage(string connectionString, TempFileStorageOptions options) : TempFileStorage(options)
{
    public override async Task<TempFile> StoreFile(string filename, Stream contentStream, TimeSpan timeout, bool isUpload = false, bool deleteOnDownload = true, CancellationToken token = default)
    {
        var tempFile = new TempFile
        {
            Filename = filename,
            CacheTimeout = DateTime.UtcNow.Add(timeout)
        };

        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(token);
        const string sql = """
                           INSERT INTO [TempFileStorage]  
                               ([Key], Filename, FileSize, IsUpload, DeleteOnDownload, CacheTimeout, Content)
                           OUTPUT 
                               inserted.FileSize
                           VALUES 
                               (@key, @filename, DATALENGTH(@content), @isUpload, @deleteOnDownload, @cacheTimeout, @content)
                           """;

        await using (var cmd = new SqlCommand(sql, connection))
        {
            cmd.CommandTimeout = 600;

            cmd.Parameters.Add("@key", SqlDbType.NVarChar).Value = tempFile.Key;
            cmd.Parameters.Add("@filename", SqlDbType.NVarChar).Value = filename;
            cmd.Parameters.Add("@isUpload", SqlDbType.Bit).Value = isUpload;
            cmd.Parameters.Add("@deleteOnDownload", SqlDbType.Bit).Value = deleteOnDownload;
            cmd.Parameters.Add("@cacheTimeout", SqlDbType.DateTime).Value = tempFile.CacheTimeout;

            // Add a parameter which uses the FileStream we just opened
            // Size is set to -1 to indicate "MAX"
            cmd.Parameters.Add("@content", SqlDbType.Binary, -1).Value = contentStream;

            // Send the data to the server asynchronously  
            var result = await cmd.ExecuteScalarAsync(token);

            // SET the file size from the database response.
            if (result != null && result != DBNull.Value)
            {
                tempFile.FileSize = (long)result;
            }
            else
            {
                throw new TempFileStorageException("Could not retrieve file size from database after insert.");
            }
        }

        await connection.CloseAsync();

        return tempFile;
    }

    public override async Task<TempFile> GetFileInfo(string key, bool filterUpload = false)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = filterUpload
            ? "SELECT [Filename], [FileSize], [IsUpload], [DeleteOnDownload], [CacheTimeout] FROM [TempFileStorage] WHERE [Key] = @key AND [CacheTimeout] > @timeout AND [IsUpload] = 0"
            : "SELECT [Filename], [FileSize], [IsUpload], [DeleteOnDownload], [CacheTimeout] FROM [TempFileStorage] WHERE [Key] = @key AND [CacheTimeout] > @timeout";
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
            var deleteOnDownload = reader.GetBoolean(3);
            var cacheTimeout = reader.GetDateTime(4);

            tempFile = new TempFile(key)
            {
                CacheTimeout = cacheTimeout,
                Filename = filename,
                FileSize = fileSize,
                IsUpload = isUpload,
                DeleteOnDownload = deleteOnDownload
            };
        }

        await reader.CloseAsync();
        await connection.CloseAsync();

        return tempFile;
    }

    public override async Task<byte[]> GetContent(string key)
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
        await connection.CloseAsync();

        return content;
    }

    public override async Task<Stream> GetContentStream(string key)
    {
        SqlConnection connection = null;
        SqlDataReader reader = null;

        try
        {
            connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var command = new SqlCommand("SELECT [Content] FROM [TempFileStorage] WHERE [Key] = @key", connection);
            command.CommandTimeout = 600;
            command.Parameters.AddWithValue("key", key);

            // 1. SequentialAccess: Streams the data, doesn't buffer it.
            // 2. CloseConnection: Ties the connection's lifetime to the reader's.
            //    When the stream we return is closed, it closes the reader,
            //    which in turn closes the connection.
            reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection);

            if (await reader.ReadAsync())
            {
                return reader.GetStream(reader.GetOrdinal("Content")); // 0 is the index of [Content]
            }

            await reader.CloseAsync();

            throw new TempFileStorageException($"No content found for key {key}");
        }
        catch (Exception)
        {
            if (reader != null)
                await reader.CloseAsync();
            else if (connection != null)
                await connection.CloseAsync();

            throw;
        }
    }

    public override async Task<bool> Remove(string key)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        int deletedCount;

        try
        {
            var command = new SqlCommand("DELETE TOP(1) FROM [TempFileStorage] WHERE [Key] = @key", connection);
            command.Parameters.AddWithValue("key", key);
            command.CommandTimeout = 30; // Give a max timeout of 30 seconds for a single delete
            deletedCount = await command.ExecuteNonQueryAsync();
        }
        catch (TimeoutException)
        {
            throw new TempFileStorageException("Unable to remove file from storage, command timeout exceeded");
        }

        return deletedCount > 0;
    }

    public override async Task CleanupStorage(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var timer = Stopwatch.StartNew();

        int deletedCount;
        bool running;

        do
        {
            try
            {
                var command = new SqlCommand("DELETE TOP(1) FROM [TempFileStorage] WHERE [CacheTimeout] < @timeout", connection);
                command.Parameters.AddWithValue("timeout", DateTime.UtcNow);
                command.CommandTimeout = 30; // Give a max timeout of 30 seconds for a single delete
                deletedCount = await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new TempFileStorageException("Unable to cleanup temp SQL storage, command timeout exceeded");
            }

            // Stop the cleanup task if we are running longer than 1 minute
            running = timer.Elapsed < TimeSpan.FromMinutes(1);
        } while (deletedCount > 0 && running);
    }
}