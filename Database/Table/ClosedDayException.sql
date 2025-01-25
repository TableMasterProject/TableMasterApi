CREATE TABLE [dbo].[ClosedDayException]
(
	[Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,        -- Identifiant unique pour chaque exception de fermeture
    [RestaurantId] BIGINT NOT NULL,                         -- Clé étrangère vers le restaurant
    [ExceptionDateBegin] DATETIME NOT NULL,                           -- Date spécifique de fermeture exceptionnelle
    [ExceptionDateEnd] DATETIME NOT NULL,                           -- Date spécifique de fermeture exceptionnelle
    [Reason] NVARCHAR(255) NULL,                         -- Raison de la fermeture (vacances, jour férié, etc.)
    [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE()  -- Date de création avec valeur par défaut
    CONSTRAINT FK_ClosedDayException_Restaurant FOREIGN KEY (RestaurantId) REFERENCES [dbo].[Restaurant](Id) -- Clé étrangère vers le restaurant
)
