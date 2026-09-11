CREATE TABLE IF NOT EXISTS "ReservationNotificationOutbox" (
    "Id" uuid PRIMARY KEY,
    "EventId" uuid NOT NULL,
    "Channel" text NOT NULL CHECK ("Channel" IN ('push','email')),
    "UserId" bigint NOT NULL REFERENCES "User"("Id") ON DELETE CASCADE,
    "Payload" jsonb NOT NULL,
    "State" text NOT NULL DEFAULT 'pending' CHECK ("State" IN ('pending','leased','completed','dead')),
    "Attempts" integer NOT NULL DEFAULT 0,
    "DueAt" timestamptz NOT NULL DEFAULT now(),
    "LeaseId" uuid NULL,
    "LeaseUntil" timestamptz NULL,
    "CompletedRecipients" jsonb NOT NULL DEFAULT '[]',
    "LastError" text NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    UNIQUE ("EventId","Channel","UserId")
);
CREATE INDEX IF NOT EXISTS "IX_ReservationNotificationOutbox_Due" ON "ReservationNotificationOutbox" ("DueAt") WHERE "State" IN ('pending','leased');
