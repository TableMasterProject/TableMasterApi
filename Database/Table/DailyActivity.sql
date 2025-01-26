CREATE TABLE [dbo].[DailyActivity]
(
	[Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,      -- Identifiant unique pour chaque plage d'activité
    [RestaurantId] BIGINT NOT NULL,                        -- Clé étrangère pour lier l'activité au restaurant
    [DayOfWeek] TINYINT NOT NULL,                          -- Jour de la semaine (0 = Lundi, 1 = Mardi, etc.)
    [StartTime] TIME NOT NULL,                         -- Heure de début de l'activité
    [EndTime] TIME NOT NULL,                           -- Heure de fin de l'activité
    [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE()  -- Date de création avec valeur par défaut
    CONSTRAINT FK_DailyActivity_Restaurant FOREIGN KEY (RestaurantId) REFERENCES [dbo].[Restaurant](Id)  -- Clé étrangère liant l'activité au restaurant
)
