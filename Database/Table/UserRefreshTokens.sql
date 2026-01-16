CREATE TABLE [dbo].[UserRefreshTokens]
(
    [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId] BIGINT NOT NULL,                         -- Clé étrangère vers [User]
    [TokenHash] NVARCHAR(MAX) NOT NULL,              -- Le token haché
    [ExpiryDate] DATETIME NOT NULL,                  -- Date d'expiration
    [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
    [DeviceInfo] NVARCHAR(255) NULL,                 -- Optionnel : Nom de l'appareil

    CONSTRAINT FK_User_RefreshTokens FOREIGN KEY ([UserId]) 
        REFERENCES [dbo].[User]([Id]) ON DELETE CASCADE
)
