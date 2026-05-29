-- Ajoute le support des comptes authentifies par Google.

ALTER TABLE "User"
    ADD COLUMN IF NOT EXISTS "AuthProvider" VARCHAR(20) NOT NULL DEFAULT 'Password',
    ADD COLUMN IF NOT EXISTS "GoogleSubject" VARCHAR(255) NULL;

ALTER TABLE "User"
    ALTER COLUMN "Password" DROP NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_User_GoogleSubject"
    ON "User" ("GoogleSubject")
    WHERE "GoogleSubject" IS NOT NULL;

ALTER TABLE "User"
    DROP CONSTRAINT IF EXISTS "CK_User_AuthProvider";

ALTER TABLE "User"
    ADD CONSTRAINT "CK_User_AuthProvider"
    CHECK ("AuthProvider" IN ('Password', 'Google'));
