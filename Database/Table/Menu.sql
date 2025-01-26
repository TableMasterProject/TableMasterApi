CREATE TABLE [dbo].[Menu]
(
	[Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,    -- Identifiant unique pour chaque item du menu
	[RestaurantId] BIGINT NOT NULL,                   -- Clé étrangère vers le restaurant
	[Category] NVARCHAR(100) NOT NULL,                    -- Catégorie (Entrée, Plat, Dessert, Boisson)
	[ItemName] NVARCHAR(200) NOT NULL,                -- Nom de l'élément du menu
	[Description] NVARCHAR(MAX) NULL,                 -- Description de l'élément
	[Price] DECIMAL(10, 2) NOT NULL,                  -- Prix de l'élément
	[CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),  -- Date de création

	CONSTRAINT FK_Menu_Restaurant FOREIGN KEY (RestaurantId) REFERENCES [Restaurant](Id)  -- Relation avec le restaurant
)
