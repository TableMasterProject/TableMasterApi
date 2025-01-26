CREATE TABLE [dbo].[ClosedDayException] (
    [Id]                 BIGINT         IDENTITY (1, 1) NOT NULL,
    [RestaurantId]       BIGINT         NOT NULL,
    [ExceptionDateBegin] DATETIME       NOT NULL,
    [ExceptionDateEnd]   DATETIME       NOT NULL,
    [Reason]             NVARCHAR (255) NULL,
    [CreatedAt]          DATETIME       DEFAULT (getdate()) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ClosedDayException_Restaurant] FOREIGN KEY ([RestaurantId]) REFERENCES [dbo].[Restaurant] ([Id])
);


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