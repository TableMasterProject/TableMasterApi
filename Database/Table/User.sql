CREATE TABLE [dbo].[User]
(
	[Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY, -- Identifiant unique auto-incrémenté
    [Email] NVARCHAR(320) NOT NULL UNIQUE,           -- Email de l'utilisateur, unique
    [Password] NVARCHAR(255) NOT NULL,               -- Mot de passe (haché)
    [FirstName] NVARCHAR(100) NOT NULL,              -- Prénom de l'utilisateur
    [LastName] NVARCHAR(100) NOT NULL,               -- Nom de l'utilisateur
    [AccountType] TINYINT NOT NULL DEFAULT 0,        -- Type de compte (0 = standard, 1 = admin, etc.)
    [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE()  -- Date de création avec valeur par défaut
)
