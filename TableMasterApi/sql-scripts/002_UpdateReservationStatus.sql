PRINT N'Début de la mise à jour de la table [dbo].[Reservation]...';

-- 1. Supprimer la contrainte par défaut qui bloque la suppression
IF EXISTS (SELECT * FROM sys.default_constraints WHERE name = N'DF_Reservation_IsVal')
BEGIN
    ALTER TABLE [dbo].[Reservation] DROP CONSTRAINT [DF_Reservation_IsVal];
    PRINT N'Contrainte par défaut [DF_Reservation_IsVal] supprimée.';
END
GO

-- 2. Supprimer l'ancienne colonne IsValidate
IF EXISTS (SELECT * FROM sys.columns WHERE Name = N'IsValidate' AND Object_ID = OBJECT_ID(N'[dbo].[Reservation]'))
BEGIN
    ALTER TABLE [dbo].[Reservation] DROP COLUMN [IsValidate];
    PRINT N'Ancienne colonne [IsValidate] supprimée.';
END
GO

-- 3. Ajouter la nouvelle colonne Status
-- 0: En attente, 1: Validée, 2: Finie, 3: Annulée (Resto), 4: Annulée (Client)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'Status' AND Object_ID = OBJECT_ID(N'[dbo].[Reservation]'))
BEGIN
    ALTER TABLE [dbo].[Reservation] 
    ADD [Status] TINYINT NOT NULL CONSTRAINT [DF_Reservation_Status] DEFAULT 0;
    PRINT N'Nouvelle colonne [Status] ajoutée avec succès.';
END
GO

PRINT N'Mise à jour terminée.';