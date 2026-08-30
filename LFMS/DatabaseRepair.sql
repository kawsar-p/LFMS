-- LFMS database repair / upgrade script
-- Safe to run on the existing SQL Server database.
-- It only creates/adds objects when they are missing.

IF OBJECT_ID(N'[AspNetUsers]', N'U') IS NOT NULL
   AND COL_LENGTH(N'AspNetUsers', N'IsActive') IS NULL
BEGIN
    ALTER TABLE [AspNetUsers] ADD [IsActive] bit NOT NULL
        CONSTRAINT [DF_AspNetUsers_IsActive] DEFAULT (1);
END

IF OBJECT_ID(N'[AspNetUsers]', N'U') IS NOT NULL
   AND COL_LENGTH(N'AspNetUsers', N'ProfileImagePath') IS NULL
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ProfileImagePath] nvarchar(300) NULL;
END

IF OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Posts', N'ReferenceCode') IS NULL
BEGIN
    ALTER TABLE [Posts] ADD [ReferenceCode] nvarchar(30) NULL;
END

IF OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Posts', N'Status') IS NULL
BEGIN
    ALTER TABLE [Posts] ADD [Status] nvarchar(20) NULL;
END

IF OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
BEGIN
    UPDATE [Posts]
    SET [ReferenceCode] = CONCAT('LF-', RIGHT('000000' + CAST([Id] AS varchar(6)), 6))
    WHERE [ReferenceCode] IS NULL OR LTRIM(RTRIM([ReferenceCode])) = '';

    UPDATE [Posts]
    SET [Status] = N'Available'
    WHERE [Status] IS NULL OR LTRIM(RTRIM([Status])) = N'';
END

IF OBJECT_ID(N'[CollectionConfirmations]', N'U') IS NULL
   AND OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[AspNetUsers]', N'U') IS NOT NULL
BEGIN
    CREATE TABLE [CollectionConfirmations]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PostId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [ConfirmationType] nvarchar(40) NOT NULL,
        [IdentificationDetails] nvarchar(2000) NOT NULL,
        [HandoverDetails] nvarchar(2000) NOT NULL,
        [HandoverDate] datetime2 NOT NULL,
        [Status] nvarchar(100) NOT NULL CONSTRAINT [DF_CollectionConfirmations_Status] DEFAULT (N'Submitted'),
        [ConfirmedAt] datetime2 NOT NULL CONSTRAINT [DF_CollectionConfirmations_ConfirmedAt] DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_CollectionConfirmations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CollectionConfirmations_Posts] FOREIGN KEY ([PostId]) REFERENCES [Posts]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollectionConfirmations_AspNetUsers] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_CollectionConfirmations_PostId_ConfirmedAt]
    ON [CollectionConfirmations]([PostId], [ConfirmedAt]);
END
