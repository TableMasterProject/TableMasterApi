CREATE DATABASE TableMaster;
-- Ajoute d'autres requêtes SQL ici si nécessaire
/*
Script de déploiement pour TableMaster

Ce code a été généré par un outil.
La modification de ce fichier peut provoquer un comportement incorrect et sera perdue si
le code est régénéré.
*/

GO
SET ANSI_NULLS, ANSI_PADDING, ANSI_WARNINGS, ARITHABORT, CONCAT_NULL_YIELDS_NULL, QUOTED_IDENTIFIER ON;

SET NUMERIC_ROUNDABORT OFF;


GO
:setvar DatabaseName "TableMaster"
:setvar DefaultFilePrefix "TableMaster"
:setvar DefaultDataPath "/var/opt/mssql/data/"
:setvar DefaultLogPath "/var/opt/mssql/data/"

GO
:on error exit
GO
/*
Détectez le mode SQLCMD et désactivez l'exécution du script si le mode SQLCMD n'est pas pris en charge.
Pour réactiver le script une fois le mode SQLCMD activé, exécutez ce qui suit :
SET NOEXEC OFF; 
*/
:setvar __IsSqlCmdEnabled "True"
GO
IF N'$(__IsSqlCmdEnabled)' NOT LIKE N'True'
    BEGIN
        PRINT N'Le mode SQLCMD doit être activé de manière à pouvoir exécuter ce script.';
        SET NOEXEC ON;
    END


GO
USE [$(DatabaseName)];


GO
PRINT N'Création de Table [dbo].[ClosedDayException]...';


GO
CREATE TABLE [dbo].[ClosedDayException] (
    [Id]                 BIGINT         IDENTITY (1, 1) NOT NULL,
    [RestaurantId]       BIGINT         NOT NULL,
    [ExceptionDateBegin] DATETIME       NOT NULL,
    [ExceptionDateEnd]   DATETIME       NOT NULL,
    [Reason]             NVARCHAR (255) NULL,
    [CreatedAt]          DATETIME       NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Création de Table [dbo].[DailyActivity]...';


GO
CREATE TABLE [dbo].[DailyActivity] (
    [Id]           BIGINT   IDENTITY (1, 1) NOT NULL,
    [RestaurantId] BIGINT   NOT NULL,
    [DayOfWeek]    TINYINT  NOT NULL,
    [StartTime]    TIME (7) NOT NULL,
    [EndTime]      TIME (7) NOT NULL,
    [CreatedAt]    DATETIME NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Création de Table [dbo].[Menu]...';


GO
CREATE TABLE [dbo].[Menu] (
    [Id]           BIGINT          IDENTITY (1, 1) NOT NULL,
    [RestaurantId] BIGINT          NOT NULL,
    [Category]     NVARCHAR (100)  NOT NULL,
    [ItemName]     NVARCHAR (200)  NOT NULL,
    [Description]  NVARCHAR (MAX)  NULL,
    [Price]        DECIMAL (10, 2) NOT NULL,
    [CreatedAt]    DATETIME        NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Création de Table [dbo].[Reservation]...';


GO
CREATE TABLE [dbo].[Reservation] (
    [Id]              BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]          BIGINT         NOT NULL,
    [TableId]         BIGINT         NOT NULL,
    [RestaurantId]    BIGINT         NOT NULL,
    [ReservationDate] DATETIME       NOT NULL,
    [NumberOfPeople]  INT            NOT NULL,
    [SpecialRequest]  NVARCHAR (500) NULL,
    [CreatedAt]       DATETIME       NOT NULL,
    [IsValidate]      BIT            NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Reservation_Table_ReservationDate] UNIQUE NONCLUSTERED ([TableId] ASC, [ReservationDate] ASC),
    CONSTRAINT [UQ_Reservation_User_Table_ReservationDate] UNIQUE NONCLUSTERED ([UserId] ASC, [TableId] ASC, [ReservationDate] ASC)
);


GO
PRINT N'Création de Table [dbo].[Restaurant]...';


GO
CREATE TABLE [dbo].[Restaurant] (
    [Id]                        BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]                    BIGINT         NOT NULL,
    [RestaurantName]            NVARCHAR (200) NOT NULL,
    [StreetNumber]              NVARCHAR (10)  NULL,
    [StreetName]                NVARCHAR (200) NULL,
    [PostalCode]                NVARCHAR (10)  NULL,
    [City]                      NVARCHAR (100) NULL,
    [Latitude]                  DECIMAL (9, 6) NULL,
    [Longitude]                 DECIMAL (9, 6) NULL,
    [Phone]                     NVARCHAR (15)  NULL,
    [CuisineType]               NVARCHAR (100) NULL,
    [PaymentMethods]            NVARCHAR (200) NULL,
    [Description]               NVARCHAR (MAX) NULL,
    [CreatedAt]                 DATETIME       NOT NULL,
    [IsAutoValidateReservation] BIT            NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Création de Table [dbo].[Review]...';


GO
CREATE TABLE [dbo].[Review] (
    [Id]           BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserId]       BIGINT         NOT NULL,
    [RestaurantId] BIGINT         NOT NULL,
    [Rating]       TINYINT        NOT NULL,
    [Comment]      NVARCHAR (MAX) NULL,
    [CreatedAt]    DATETIME       NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Review_User_Restaurant] UNIQUE NONCLUSTERED ([UserId] ASC, [RestaurantId] ASC)
);


GO
PRINT N'Création de Table [dbo].[TableEntity]...';


GO
CREATE TABLE [dbo].[TableEntity] (
    [Id]            BIGINT   IDENTITY (1, 1) NOT NULL,
    [RestaurantId]  BIGINT   NOT NULL,
    [TableNumber]   INT      NOT NULL,
    [NumberOfSeats] INT      NOT NULL,
    [CreatedAt]     DATETIME NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
PRINT N'Création de Table [dbo].[User]...';


GO
CREATE TABLE [dbo].[User] (
    [Id]          BIGINT         IDENTITY (1, 1) NOT NULL,
    [Email]       NVARCHAR (320) NOT NULL,
    [Password]    NVARCHAR (255) NOT NULL,
    [FirstName]   NVARCHAR (100) NOT NULL,
    [LastName]    NVARCHAR (100) NOT NULL,
    [AccountType] TINYINT        NOT NULL,
    [CreatedAt]   DATETIME       NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    UNIQUE NONCLUSTERED ([Email] ASC)
);

GO
PRINT N'Création de Table [dbo].[UserRefreshTokens]...';

GO
CREATE TABLE [dbo].[UserRefreshTokens] (
                                           [Id]         BIGINT         IDENTITY (1, 1) NOT NULL,
                                           [UserId]     BIGINT         NOT NULL,
                                           [TokenHash]  NVARCHAR (MAX) NOT NULL,
                                           [ExpiryDate] DATETIME       NOT NULL,
                                           [CreatedAt]  DATETIME       NOT NULL,
                                           [DeviceInfo] NVARCHAR (255) NULL,
                                           PRIMARY KEY CLUSTERED ([Id] ASC)
);

GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[ClosedDayException]...';


GO
ALTER TABLE [dbo].[ClosedDayException]
    ADD DEFAULT (getdate()) FOR [CreatedAt];


GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[DailyActivity]...';


GO
ALTER TABLE [dbo].[DailyActivity]
    ADD DEFAULT GETDATE() FOR [CreatedAt];


GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[Menu]...';


GO
ALTER TABLE [dbo].[Menu]
    ADD DEFAULT GETDATE() FOR [CreatedAt];


GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[Reservation]...';


GO
ALTER TABLE [dbo].[Reservation]
    ADD DEFAULT GETDATE() FOR [CreatedAt];


GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[Reservation]...';


GO
ALTER TABLE [dbo].[Reservation]
    ADD DEFAULT 0 FOR [IsValidate];


GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[Restaurant]...';


GO
ALTER TABLE [dbo].[Restaurant]
    ADD DEFAULT GETDATE() FOR [CreatedAt];


GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[Restaurant]...';


GO
ALTER TABLE [dbo].[Restaurant]
    ADD DEFAULT 0 FOR [IsAutoValidateReservation];


GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[Review]...';


GO
ALTER TABLE [dbo].[Review]
    ADD DEFAULT (getdate()) FOR [CreatedAt];


GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[TableEntity]...';


GO
ALTER TABLE [dbo].[TableEntity]
    ADD DEFAULT GETDATE() FOR [CreatedAt];


GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[User]...';


GO
ALTER TABLE [dbo].[User]
    ADD DEFAULT 0 FOR [AccountType];


GO
PRINT N'Création de Contrainte par défaut contrainte sans nom sur [dbo].[User]...';


GO
ALTER TABLE [dbo].[User]
    ADD DEFAULT GETDATE() FOR [CreatedAt];


GO
PRINT N'Création de Clé étrangère [dbo].[FK_ClosedDayException_Restaurant]...';


GO
ALTER TABLE [dbo].[ClosedDayException] WITH NOCHECK
    ADD CONSTRAINT [FK_ClosedDayException_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id]);


GO
PRINT N'Création de Clé étrangère [dbo].[FK_DailyActivity_Restaurant]...';


GO
ALTER TABLE [dbo].[DailyActivity] WITH NOCHECK
    ADD CONSTRAINT [FK_DailyActivity_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id]);


GO
PRINT N'Création de Clé étrangère [dbo].[FK_Menu_Restaurant]...';


GO
ALTER TABLE [dbo].[Menu] WITH NOCHECK
    ADD CONSTRAINT [FK_Menu_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id]);


GO
PRINT N'Création de Clé étrangère [dbo].[FK_Reservation_User]...';


GO
ALTER TABLE [dbo].[Reservation] WITH NOCHECK
    ADD CONSTRAINT [FK_Reservation_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]);


GO
PRINT N'Création de Clé étrangère [dbo].[FK_Reservation_Table]...';


GO
ALTER TABLE [dbo].[Reservation] WITH NOCHECK
    ADD CONSTRAINT [FK_Reservation_Table] FOREIGN KEY ([TableId]) REFERENCES [dbo].[TableEntity] ([Id]);


GO
PRINT N'Création de Clé étrangère [dbo].[FK_Reservation_Restaurant]...';


GO
ALTER TABLE [dbo].[Reservation] WITH NOCHECK
    ADD CONSTRAINT [FK_Reservation_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id]);


GO
PRINT N'Création de Clé étrangère [dbo].[FK_Restaurant_User]...';


GO
ALTER TABLE [dbo].[Restaurant] WITH NOCHECK
    ADD CONSTRAINT [FK_Restaurant_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]);


GO
PRINT N'Création de Clé étrangère [dbo].[FK_Review_Restaurant]...';


GO
ALTER TABLE [dbo].[Review] WITH NOCHECK
    ADD CONSTRAINT [FK_Review_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Création de Clé étrangère [dbo].[FK_Review_User]...';


GO
ALTER TABLE [dbo].[Review] WITH NOCHECK
    ADD CONSTRAINT [FK_Review_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE;


GO
PRINT N'Création de Clé étrangère [dbo].[FK_Table_Restaurant]...';


GO
ALTER TABLE [dbo].[TableEntity] WITH NOCHECK
    ADD CONSTRAINT [FK_Table_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id]);


GO
PRINT N'Création de Contrainte de validation contrainte sans nom sur [dbo].[Review]...';


GO
ALTER TABLE [dbo].[Review] WITH NOCHECK
    ADD CHECK ([Rating]>=(1) AND [Rating]<=(5));

GO
PRINT N'Création de Contrainte par défaut sur [dbo].[UserRefreshTokens]...';

GO
ALTER TABLE [dbo].[UserRefreshTokens]
    ADD DEFAULT GETDATE() FOR [CreatedAt];

GO
PRINT N'Création de Déclencheur [dbo].[DeleteOldClosedDayExceptions]...';


GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE TRIGGER DeleteOldClosedDayExceptions
ON [ClosedDayException]
AFTER INSERT, UPDATE
AS 
BEGIN
    -- Empêche l'affichage de résultats supplémentaires
    SET NOCOUNT ON;

    -- Supprimer toutes les lignes où ExceptionDateEnd est inférieure à la date actuelle
    DELETE FROM [ClosedDayException]
    WHERE ExceptionDateEnd < GETDATE();
END
GO
PRINT N'Vérification de données existantes par rapport aux nouvelles contraintes';


GO
USE [$(DatabaseName)];


GO
ALTER TABLE [dbo].[ClosedDayException] WITH CHECK CHECK CONSTRAINT [FK_ClosedDayException_Restaurant];

ALTER TABLE [dbo].[DailyActivity] WITH CHECK CHECK CONSTRAINT [FK_DailyActivity_Restaurant];

ALTER TABLE [dbo].[Menu] WITH CHECK CHECK CONSTRAINT [FK_Menu_Restaurant];

ALTER TABLE [dbo].[Reservation] WITH CHECK CHECK CONSTRAINT [FK_Reservation_User];

ALTER TABLE [dbo].[Reservation] WITH CHECK CHECK CONSTRAINT [FK_Reservation_Table];

ALTER TABLE [dbo].[Reservation] WITH CHECK CHECK CONSTRAINT [FK_Reservation_Restaurant];

ALTER TABLE [dbo].[Restaurant] WITH CHECK CHECK CONSTRAINT [FK_Restaurant_User];

ALTER TABLE [dbo].[TableEntity] WITH CHECK CHECK CONSTRAINT [FK_Table_Restaurant];


GO
CREATE TABLE [#__checkStatus] (
    id           INT            IDENTITY (1, 1) PRIMARY KEY CLUSTERED,
    [Schema]     NVARCHAR (256),
    [Table]      NVARCHAR (256),
    [Constraint] NVARCHAR (256)
);

SET NOCOUNT ON;

DECLARE tableconstraintnames CURSOR LOCAL FORWARD_ONLY
    FOR SELECT SCHEMA_NAME([schema_id]),
               OBJECT_NAME([parent_object_id]),
               [name],
               0
        FROM   [sys].[objects]
        WHERE  [parent_object_id] IN (OBJECT_ID(N'dbo.Review'))
               AND [type] IN (N'F', N'C')
                   AND [object_id] IN (SELECT [object_id]
                                       FROM   [sys].[check_constraints]
                                       WHERE  [is_not_trusted] <> 0
                                              AND [is_disabled] = 0
                                       UNION
                                       SELECT [object_id]
                                       FROM   [sys].[foreign_keys]
                                       WHERE  [is_not_trusted] <> 0
                                              AND [is_disabled] = 0);

DECLARE @schemaname AS NVARCHAR (256);

DECLARE @tablename AS NVARCHAR (256);

DECLARE @checkname AS NVARCHAR (256);

DECLARE @is_not_trusted AS INT;

DECLARE @statement AS NVARCHAR (1024);

BEGIN TRY
    OPEN tableconstraintnames;
    FETCH tableconstraintnames INTO @schemaname, @tablename, @checkname, @is_not_trusted;
    WHILE @@fetch_status = 0
        BEGIN
            PRINT N'Vérification de la contrainte : ' + @checkname + N' [' + @schemaname + N'].[' + @tablename + N']';
            SET @statement = N'ALTER TABLE [' + @schemaname + N'].[' + @tablename + N'] WITH ' + CASE @is_not_trusted WHEN 0 THEN N'CHECK' ELSE N'NOCHECK' END + N' CHECK CONSTRAINT [' + @checkname + N']';
            BEGIN TRY
                EXECUTE [sp_executesql] @statement;
            END TRY
            BEGIN CATCH
                INSERT  [#__checkStatus] ([Schema], [Table], [Constraint])
                VALUES                  (@schemaname, @tablename, @checkname);
            END CATCH
            FETCH tableconstraintnames INTO @schemaname, @tablename, @checkname, @is_not_trusted;
        END
END TRY
BEGIN CATCH
    PRINT ERROR_MESSAGE();
END CATCH

IF CURSOR_STATUS(N'LOCAL', N'tableconstraintnames') >= 0
    CLOSE tableconstraintnames;

IF CURSOR_STATUS(N'LOCAL', N'tableconstraintnames') = -1
    DEALLOCATE tableconstraintnames;

SELECT N'Échec de vérification de la contrainte :' + [Schema] + N'.' + [Table] + N',' + [Constraint]
FROM   [#__checkStatus];

IF @@ROWCOUNT > 0
    BEGIN
        DROP TABLE [#__checkStatus];
        RAISERROR (N'Une erreur s''est produite lors de la vérification des contraintes', 16, 127);
    END

SET NOCOUNT OFF;

DROP TABLE [#__checkStatus];


GO
PRINT N'Mise à jour terminée.';


GO
