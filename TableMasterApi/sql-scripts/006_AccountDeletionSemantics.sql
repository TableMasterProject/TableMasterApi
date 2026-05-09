-- Permet de conserver l'historique des reservations quand un compte,
-- un restaurant ou une table est supprime.

ALTER TABLE "Reservation"
    ALTER COLUMN "UserId" DROP NOT NULL,
    ALTER COLUMN "TableId" DROP NOT NULL,
    ALTER COLUMN "RestaurantId" DROP NOT NULL;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Reservation_User') THEN
        ALTER TABLE "Reservation" DROP CONSTRAINT "FK_Reservation_User";
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Reservation_Table') THEN
        ALTER TABLE "Reservation" DROP CONSTRAINT "FK_Reservation_Table";
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Reservation_Restaurant') THEN
        ALTER TABLE "Reservation" DROP CONSTRAINT "FK_Reservation_Restaurant";
    END IF;

    ALTER TABLE "Reservation"
        ADD CONSTRAINT "FK_Reservation_User"
        FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE SET NULL;

    ALTER TABLE "Reservation"
        ADD CONSTRAINT "FK_Reservation_Table"
        FOREIGN KEY ("TableId") REFERENCES "TableEntity" ("Id") ON DELETE SET NULL;

    ALTER TABLE "Reservation"
        ADD CONSTRAINT "FK_Reservation_Restaurant"
        FOREIGN KEY ("RestaurantId") REFERENCES "Restaurant" ("Id") ON DELETE SET NULL;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TableEntity_RestaurantRoom') THEN
        ALTER TABLE "TableEntity" DROP CONSTRAINT "FK_TableEntity_RestaurantRoom";
        ALTER TABLE "TableEntity"
            ADD CONSTRAINT "FK_TableEntity_RestaurantRoom"
            FOREIGN KEY ("RoomId") REFERENCES "RestaurantRoom" ("Id") ON DELETE SET NULL;
    END IF;
END $$;
