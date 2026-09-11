-- Detach legacy inconsistent links without deleting tables or reservations.
UPDATE "TableEntity" t
SET "RoomId" = NULL
FROM "RestaurantRoom" r
WHERE t."RoomId" = r."Id" AND t."RestaurantId" <> r."RestaurantId";

ALTER TABLE "RestaurantRoom"
    ADD CONSTRAINT "UQ_RestaurantRoom_Id_Restaurant" UNIQUE ("Id", "RestaurantId");

ALTER TABLE "TableEntity"
    ADD CONSTRAINT "FK_TableEntity_RoomOwnership"
    FOREIGN KEY ("RoomId", "RestaurantId")
    REFERENCES "RestaurantRoom" ("Id", "RestaurantId")
    ON DELETE SET NULL ("RoomId");
