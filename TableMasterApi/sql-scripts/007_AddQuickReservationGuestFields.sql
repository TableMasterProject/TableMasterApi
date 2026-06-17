-- Ajoute les informations client saisies par le restaurateur pour les reservations rapides.

ALTER TABLE "Reservation"
    ADD COLUMN IF NOT EXISTS "GuestName" VARCHAR(120) NULL,
    ADD COLUMN IF NOT EXISTS "GuestPhone" VARCHAR(30) NULL;
