CREATE TABLE [dbo].[Reservation]
(
    [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,           -- Identifiant unique pour chaque réservation
    [UserId] BIGINT NOT NULL,                                  -- Clé étrangère vers l'utilisateur (client)
    [TableId] BIGINT NOT NULL,                                  -- Clé étrangère vers la table réservée
    [RestaurantId] BIGINT NOT NULL,
    [ReservationDate] DATETIME NOT NULL,                        -- Date et heure de la réservation
    [NumberOfPeople] INT NOT NULL,                             -- Nombre de personnes pour la réservation
    [SpecialRequest] NVARCHAR(500) NULL,                       -- Demande spéciale (facultatif)
    [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),           
    [Status] TINYINT NOT NULL DEFAULT 0,

    CONSTRAINT FK_Reservation_User FOREIGN KEY (UserId) REFERENCES [dbo].[User](Id),  -- Clé étrangère vers l'utilisateur
    CONSTRAINT FK_Reservation_Table FOREIGN KEY (TableId) REFERENCES [dbo].[TableEntity](Id),  -- Clé étrangère vers la table réservée
    CONSTRAINT FK_Reservation_Restaurant FOREIGN KEY (RestaurantId) REFERENCES [dbo].[Restaurant](Id),

    -- Contrainte d'unicité pour éviter les doubles réservations sur la même table à la même date
    CONSTRAINT UQ_Reservation_Table_ReservationDate UNIQUE (TableId, ReservationDate),

    -- Contrainte d'unicité pour éviter qu'un utilisateur réserve plusieurs fois la même table à la même date
    CONSTRAINT UQ_Reservation_User_Table_ReservationDate UNIQUE (UserId, TableId, ReservationDate)
);
