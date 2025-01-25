CREATE TABLE [dbo].[TableEntity]
(
	[Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,  -- Identifiant unique pour chaque table
    [RestaurantId] BIGINT NOT NULL,                  -- Clé étrangère qui référence le restaurant
    [TableNumber] INT NOT NULL,                      -- Numéro de la table (pour la gestion)
    [NumberOfSeats] INT NOT NULL,                    -- Nombre de places
    [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE()  -- Date de création avec valeur par défaut
    CONSTRAINT FK_Table_Restaurant FOREIGN KEY (RestaurantId) REFERENCES [Restaurant](Id)
)
