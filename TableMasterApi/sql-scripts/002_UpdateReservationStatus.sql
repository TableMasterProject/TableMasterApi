-- Remplace l'ancien booleen IsValidate par un statut de reservation.

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Reservation'
          AND column_name = 'IsValidate'
    ) THEN
        ALTER TABLE "Reservation" DROP COLUMN "IsValidate";
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Reservation'
          AND column_name = 'Status'
    ) THEN
        ALTER TABLE "Reservation"
        ADD COLUMN "Status" SMALLINT NOT NULL DEFAULT 0;
    END IF;
END $$;
