PRINT N'Création de Table [dbo].[UserDeviceTokens]...';
CREATE TABLE [dbo].[UserDeviceTokens] (
    [Id]             BIGINT IDENTITY (1, 1) NOT NULL,
    [UserId]         BIGINT NOT NULL,
    [DeviceToken]    NVARCHAR (500) NOT NULL,
    [DevicePlatform] NVARCHAR (50)  NULL,
    [LastSeenAt]     DATETIME      CONSTRAINT [DF_UserDeviceTokens_LastSeenAt] DEFAULT GETDATE() NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserDeviceTokens_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_UserDeviceTokens_User_DeviceToken] ON [dbo].[UserDeviceTokens]([UserId], [DeviceToken]);
GO
