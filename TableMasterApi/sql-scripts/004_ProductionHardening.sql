-- Indexes and constraints for production-readiness.

CREATE INDEX IF NOT EXISTS "IX_Restaurant_UserId"
    ON "Restaurant" ("UserId");

CREATE INDEX IF NOT EXISTS "IX_TableEntity_RestaurantId"
    ON "TableEntity" ("RestaurantId");

CREATE INDEX IF NOT EXISTS "IX_Reservation_Restaurant_Date_Status"
    ON "Reservation" ("RestaurantId", "ReservationDate", "Status");

CREATE INDEX IF NOT EXISTS "IX_Reservation_Table_Date_Status"
    ON "Reservation" ("TableId", "ReservationDate", "Status");

CREATE INDEX IF NOT EXISTS "IX_Reservation_UserId"
    ON "Reservation" ("UserId");

CREATE INDEX IF NOT EXISTS "IX_UserRefreshTokens_TokenHash"
    ON "UserRefreshTokens" ("TokenHash");

CREATE INDEX IF NOT EXISTS "IX_Review_RestaurantId"
    ON "Review" ("RestaurantId");

ALTER TABLE "TableEntity"
    ADD CONSTRAINT "CK_TableEntity_NumberOfSeats"
    CHECK ("NumberOfSeats" > 0);

ALTER TABLE "Menu"
    ADD CONSTRAINT "CK_Menu_Price"
    CHECK ("Price" >= 0);

ALTER TABLE "DailyActivity"
    ADD CONSTRAINT "CK_DailyActivity_TimeRange"
    CHECK ("EndTime" > "StartTime");

ALTER TABLE "ClosedDayException"
    ADD CONSTRAINT "CK_ClosedDayException_DateRange"
    CHECK ("ExceptionDateEnd" >= "ExceptionDateBegin");

ALTER TABLE "Reservation"
    ADD CONSTRAINT "CK_Reservation_NumberOfPeople"
    CHECK ("NumberOfPeople" > 0);
