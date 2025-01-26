CREATE TABLE [dbo].[Review] (
    [Id]           BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]       BIGINT         NOT NULL,
    [RestaurantId] BIGINT         NOT NULL,
    [Rating]       TINYINT        NOT NULL,
    [Comment]      NVARCHAR (MAX) NULL,
    [CreatedAt]    DATETIME       DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CHECK ([Rating]>=(1) AND [Rating]<=(5)),
    CONSTRAINT [FK_Review_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Review_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [UQ_Review_User_Restaurant] UNIQUE NONCLUSTERED ([UserId] ASC, [RestaurantId] ASC)
);

