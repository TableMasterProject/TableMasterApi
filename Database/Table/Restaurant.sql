CREATE TABLE [dbo].[Restaurant]
(
	[Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,  -- Identifiant unique pour chaque restaurant
    [UserId] BIGINT NOT NULL,                         -- Clé étrangère vers l'utilisateur
    [RestaurantName] NVARCHAR(200) NOT NULL,          -- Nom du restaurant
    [StreetNumber] NVARCHAR(10) NULL,                 -- Numéro de rue
    [StreetName] NVARCHAR(200) NULL,                  -- Nom de la rue
    [PostalCode] NVARCHAR(10) NULL,                   -- Code postal
    [City] NVARCHAR(100) NULL,                        -- Ville
    [Latitude] DECIMAL(9, 6) NULL,                    -- Latitude du restaurant
    [Longitude] DECIMAL(9, 6) NULL,                   -- Longitude du restaurant
    [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE()  -- Date de création avec valeur par défaut
    CONSTRAINT FK_Restaurant_User FOREIGN KEY (UserId) REFERENCES [User](Id)  -- Clé étrangère liant le restaurant à un utilisateur
)
