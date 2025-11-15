IF OBJECT_ID(N'dbo.TempFileStorage', N'U') IS NULL
BEGIN
    CREATE TABLE [TempFileStorage] (
       [Key] nvarchar(10) NOT NULL,
       [Filename] nvarchar(max) NOT NULL,
       [Filesize] bigint NOT NULL,
       [CacheTimeout] datetime2 NOT NULL,
       [Content] varbinary(max) NOT NULL,
    
       CONSTRAINT [PK_Key] PRIMARY KEY CLUSTERED ([Key] ASC) 
    );
END;

IF NOT EXISTS (SELECT 1 FROM SYS.COLUMNS WHERE OBJECT_ID = OBJECT_ID(N'[dbo].[TempFileStorage]') AND name = 'IsUpload')
BEGIN
    ALTER TABLE [TempFileStorage]
    ADD [IsUpload] BIT NOT NULL DEFAULT 0
END;

IF NOT EXISTS (SELECT 1 FROM SYS.COLUMNS WHERE OBJECT_ID = OBJECT_ID(N'[dbo].[TempFileStorage]') AND name = 'DeleteOnDownload')
BEGIN
    ALTER TABLE [TempFileStorage]
        ADD [DeleteOnDownload] BIT NOT NULL DEFAULT 0
END;