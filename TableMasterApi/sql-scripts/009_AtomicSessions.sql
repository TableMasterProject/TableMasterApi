-- Les anciens doublons sont tous revoques avant d'imposer l'unicite globale du hash.
DELETE FROM "UserRefreshTokens" token
USING (
    SELECT "TokenHash"
    FROM "UserRefreshTokens"
    GROUP BY "TokenHash"
    HAVING COUNT(*) > 1
) duplicate
WHERE token."TokenHash" = duplicate."TokenHash";

DROP INDEX IF EXISTS "IX_UserRefreshTokens_TokenHash";

CREATE UNIQUE INDEX "IX_UserRefreshTokens_TokenHash"
    ON "UserRefreshTokens" ("TokenHash");
