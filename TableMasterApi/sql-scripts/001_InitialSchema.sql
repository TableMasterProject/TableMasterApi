/*
  Script d'initialisation pour TableMaster (Version DbUp compatible)
  Ce script crée toutes les tables, contraintes et triggers nécessaires.
*/

-- =============================================
-- 1. CRÉATION DES TABLES (Indépendantes d'abord)
-- =============================================

PRINT N'Création de Table [dbo].[User]...';
CREATE TABLE [dbo].[User] (
    [Id]          BIGINT         IDENTITY (1, 1) NOT NULL,
    [Email]       NVARCHAR (320) NOT NULL,
    [Password]    NVARCHAR (255) NOT NULL,
    [FirstName]   NVARCHAR (100) NOT NULL,
    [LastName]    NVARCHAR (100) NOT NULL,
    [AccountType] TINYINT        CONSTRAINT [DF_User_AccountType] DEFAULT 0 NOT NULL,
    [CreatedAt]   DATETIME       CONSTRAINT [DF_User_CreatedAt] DEFAULT GETDATE() NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    UNIQUE NONCLUSTERED ([Email] ASC)
);
GO

-- =============================================
-- 2. TABLES DÉPENDANTES DE [User]
-- =============================================

PRINT N'Création de Table [dbo].[Restaurant]...';
CREATE TABLE [dbo].[Restaurant] (
    [Id]                        BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserId]                    BIGINT          NOT NULL,
    [RestaurantName]            NVARCHAR (200)  NOT NULL,
    [StreetNumber]              NVARCHAR (10)   NULL,
    [StreetName]                NVARCHAR (200)  NULL,
    [PostalCode]                NVARCHAR (10)   NULL,
    [City]                      NVARCHAR (100)  NULL,
    [Latitude]                  DECIMAL (9, 6)  NULL,
    [Longitude]                 DECIMAL (9, 6)  NULL,
    [Phone]                     NVARCHAR (15)   NULL,
    [CuisineType]               NVARCHAR (100)  NULL,
    [PaymentMethods]            NVARCHAR (200)  NULL,
    [Description]               NVARCHAR (MAX)  NULL,
    [CreatedAt]                 DATETIME        CONSTRAINT [DF_Restaurant_CreatedAt] DEFAULT GETDATE() NOT NULL,
    [IsAutoValidateReservation] BIT             CONSTRAINT [DF_Restaurant_AutoVal] DEFAULT 0 NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Restaurant_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);
GO

PRINT N'Création de Table [dbo].[UserRefreshTokens]...';
CREATE TABLE [dbo].[UserRefreshTokens] (
    [Id]         BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]     BIGINT         NOT NULL,
    [TokenHash]  NVARCHAR (MAX) NOT NULL,
    [ExpiryDate] DATETIME       NOT NULL,
    [CreatedAt]  DATETIME       CONSTRAINT [DF_RefreshToken_CreatedAt] DEFAULT GETDATE() NOT NULL,
    [DeviceInfo] NVARCHAR (255) NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserRefreshTokens_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE
);
GO

-- =============================================
-- 3. TABLES DÉPENDANTES DE [Restaurant]
-- =============================================

PRINT N'Création de Table [dbo].[TableEntity]...';
CREATE TABLE [dbo].[TableEntity] (
    [Id]            BIGINT   IDENTITY (1, 1) NOT NULL,
    [RestaurantId]  BIGINT   NOT NULL,
    [TableNumber]   INT      NOT NULL,
    [NumberOfSeats] INT      NOT NULL,
    [CreatedAt]     DATETIME CONSTRAINT [DF_Table_CreatedAt] DEFAULT GETDATE() NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Table_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id])
);
GO

PRINT N'Création de Table [dbo].[Menu]...';
CREATE TABLE [dbo].[Menu] (
    [Id]           BIGINT          IDENTITY (1, 1) NOT NULL,
    [RestaurantId] BIGINT          NOT NULL,
    [Category]     NVARCHAR (100)  NOT NULL,
    [ItemName]     NVARCHAR (200)  NOT NULL,
    [Description]  NVARCHAR (MAX)  NULL,
    [Price]        DECIMAL (10, 2) NOT NULL,
    [CreatedAt]    DATETIME        CONSTRAINT [DF_Menu_CreatedAt] DEFAULT GETDATE() NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Menu_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id])
);
GO

PRINT N'Création de Table [dbo].[DailyActivity]...';
CREATE TABLE [dbo].[DailyActivity] (
    [Id]           BIGINT   IDENTITY (1, 1) NOT NULL,
    [RestaurantId] BIGINT   NOT NULL,
    [DayOfWeek]    TINYINT  NOT NULL,
    [StartTime]    TIME (7) NOT NULL,
    [EndTime]      TIME (7) NOT NULL,
    [CreatedAt]    DATETIME CONSTRAINT [DF_DailyActivity_CreatedAt] DEFAULT GETDATE() NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_DailyActivity_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id])
);
GO

PRINT N'Création de Table [dbo].[ClosedDayException]...';
CREATE TABLE [dbo].[ClosedDayException] (
    [Id]                 BIGINT         IDENTITY (1, 1) NOT NULL,
    [RestaurantId]       BIGINT         NOT NULL,
    [ExceptionDateBegin] DATETIME       NOT NULL,
    [ExceptionDateEnd]   DATETIME       NOT NULL,
    [Reason]             NVARCHAR (255) NULL,
    [CreatedAt]          DATETIME       CONSTRAINT [DF_ClosedDay_CreatedAt] DEFAULT GETDATE() NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ClosedDayException_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id])
);
GO

-- =============================================
-- 4. TABLES MULTI-DÉPENDANTES (Jointures)
-- =============================================

PRINT N'Création de Table [dbo].[Reservation]...';
CREATE TABLE [dbo].[Reservation] (
    [Id]              BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]          BIGINT         NOT NULL,
    [TableId]         BIGINT         NOT NULL,
    [RestaurantId]    BIGINT         NOT NULL,
    [ReservationDate] DATETIME       NOT NULL,
    [NumberOfPeople]  INT            NOT NULL,
    [SpecialRequest]  NVARCHAR (500) NULL,
    [CreatedAt]       DATETIME       CONSTRAINT [DF_Reservation_CreatedAt] DEFAULT GETDATE() NOT NULL,
    [IsValidate]      BIT            CONSTRAINT [DF_Reservation_IsVal] DEFAULT 0 NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Reservation_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]),
    CONSTRAINT [FK_Reservation_Table] FOREIGN KEY ([TableId]) REFERENCES [dbo].[TableEntity] ([Id]),
    CONSTRAINT [FK_Reservation_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id])
);
GO

PRINT N'Création de Table [dbo].[Review]...';
CREATE TABLE [dbo].[Review] (
    [Id]           BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]       BIGINT         NOT NULL,
    [RestaurantId] BIGINT         NOT NULL,
    [Rating]       TINYINT        NOT NULL,
    [Comment]      NVARCHAR (MAX) NULL,
    [CreatedAt]    DATETIME       CONSTRAINT [DF_Review_CreatedAt] DEFAULT GETDATE() NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Review_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Review_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [UQ_Review_User_Restaurant] UNIQUE NONCLUSTERED ([UserId] ASC, [RestaurantId] ASC),
    CONSTRAINT [CK_Review_Rating] CHECK ([Rating]>=(1) AND [Rating]<=(5))
);
GO

-- =============================================
-- 5. LOGIQUE MÉTIER (Triggers)
-- =============================================

PRINT N'Création de Déclencheur [dbo].[DeleteOldClosedDayExceptions]...';
GO
CREATE TRIGGER DeleteOldClosedDayExceptions
ON [dbo].[ClosedDayException]
AFTER INSERT, UPDATE
AS 
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[ClosedDayException]
    WHERE ExceptionDateEnd < GETDATE();
END
GO

PRINT N'Migration terminée avec succès.';