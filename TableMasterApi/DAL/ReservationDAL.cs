using Dapper;
using Npgsql;
using TableMasterApi.Model;
using TableMasterApi.Service;
using TableMasterApi.DAL.Interfaces;

namespace TableMasterApi.DAL
{
    /// <summary>
    /// Data Access Layer pour la gestion des réservations
    /// </summary>
    public class ReservationDAL : IReservationDAL
    {
        private readonly IDbConnectionFactory _connections;
        private readonly ReservationRules _rules;

        public ReservationDAL(IDbConnectionFactory connections, ReservationRules rules)
        {
            _connections = connections;
            _rules = rules;
        }

        public async Task<ReservationOut> CreateReservationAsync(ReservationIn reservation)
        {
            reservation.ReservationDate = ReservationRules.Normalize(reservation.ReservationDate);
            using var connection = _connections.CreateConnection();
            await ((NpgsqlConnection)connection).OpenAsync();
            await using var transaction = await ((NpgsqlConnection)connection).BeginTransactionAsync();
            await LockTableAsync(connection, transaction, reservation.TableId);
            await ValidateAsync(connection, transaction, reservation, 0);
            var created = await connection.QuerySingleAsync<ReservationOut>("""
                INSERT INTO "Reservation" ("UserId","TableId","RestaurantId","ReservationDate","NumberOfPeople","SpecialRequest","GuestName","GuestPhone","Status")
                VALUES (@UserId,@TableId,@RestaurantId,@ReservationDate,@NumberOfPeople,@SpecialRequest,@GuestName,@GuestPhone,@Status) RETURNING *;
                """, new { reservation.UserId, reservation.TableId, reservation.RestaurantId, reservation.ReservationDate, reservation.NumberOfPeople,
                    reservation.SpecialRequest, reservation.GuestName, reservation.GuestPhone, Status = (short)reservation.Status }, transaction);
            await ReservationOutbox.EnqueueAsync(connection, transaction, created, "reservation_created");
            await transaction.CommitAsync();
            return created;
        }

        private static async Task LockTableAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, long? tableId)
        {
            if (tableId.HasValue)
            {
                await connection.ExecuteAsync("SELECT pg_advisory_xact_lock(@Id)", new { Id = tableId.Value }, transaction);
                await connection.QueryAsync<long>("SELECT \"Id\" FROM \"TableEntity\" WHERE \"Id\"=@Id FOR UPDATE", new { Id = tableId.Value }, transaction);
            }
        }

        private async Task ValidateAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, ReservationIn reservation, long excludeId)
        {
            var table = await connection.QuerySingleOrDefaultAsync<TableEntityOut>("SELECT * FROM \"TableEntity\" WHERE \"Id\"=@Id", new { Id = reservation.TableId }, transaction);
            if (table == null || table.RestaurantId != reservation.RestaurantId || reservation.NumberOfPeople <= 0 || table.NumberOfSeats < reservation.NumberOfPeople)
                throw new ReservationRuleException("La table ou sa capacité ne permet pas cette réservation.");
            var hours = await connection.QueryAsync<DailyActivityOut>("SELECT * FROM \"DailyActivity\" WHERE \"RestaurantId\"=@RestaurantId", new { reservation.RestaurantId }, transaction);
            var closures = await connection.QueryAsync<ClosedDayExceptionOut>("SELECT * FROM \"ClosedDayException\" WHERE \"RestaurantId\"=@RestaurantId", new { reservation.RestaurantId }, transaction);
            _rules.Validate(reservation.ReservationDate, reservation.UserId == null, hours, closures);
            var conflict = await connection.QuerySingleAsync<bool>("""
                SELECT EXISTS(SELECT 1 FROM "Reservation" WHERE "TableId"=@TableId AND "Id"<>@excludeId AND "Status"=1
                    AND ABS(EXTRACT(EPOCH FROM ("ReservationDate"-@ReservationDate))) < 5400)
                """, new { reservation.TableId, reservation.ReservationDate, excludeId }, transaction);
            if (conflict) throw new ReservationConflictException();
        }

        private static async Task<ReservationOut?> LockReservationAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, long id)
        {
            // The lookup is immutable routing information only: acquire the table before locking the reservation.
            var tableId = await connection.QuerySingleOrDefaultAsync<long?>("SELECT \"TableId\" FROM \"Reservation\" WHERE \"Id\"=@id", new { id }, transaction);
            await LockTableAsync(connection, transaction, tableId);
            return await connection.QuerySingleOrDefaultAsync<ReservationOut>("SELECT * FROM \"Reservation\" WHERE \"Id\"=@id FOR UPDATE", new { id }, transaction);
        }

        public async Task<ReservationOut?> UpdateReservationStatus(long id, ReservationStatus reservationStatus, ReservationStatus expectedStatus)
        {
            using var connection = _connections.CreateConnection();
            await ((NpgsqlConnection)connection).OpenAsync();
            await using var transaction = await ((NpgsqlConnection)connection).BeginTransactionAsync();
            var reservation = await LockReservationAsync(connection, transaction, id);
            if (reservation == null || reservation.Status != expectedStatus) throw new ReservationConflictException();
            var allowed = expectedStatus == ReservationStatus.EnAttente && reservationStatus is ReservationStatus.Validee or ReservationStatus.AnnuleeParResto ||
                expectedStatus == ReservationStatus.Validee && reservationStatus is ReservationStatus.Finie or ReservationStatus.AnnuleeParResto or ReservationStatus.AnnuleeParClient;
            if (!allowed) throw new ReservationRuleException("Cette transition de statut n'est pas autorisée.");
            if (reservationStatus == ReservationStatus.Validee) await ValidateAsync(connection, transaction, reservation, id);
            var updated = await connection.QuerySingleOrDefaultAsync<ReservationOut>("""
                UPDATE "Reservation" SET "Status"=@Status WHERE "Id"=@id AND "Status"=@Expected RETURNING *;
                """, new { id, Status = (short)reservationStatus, Expected = (short)expectedStatus }, transaction);
            if (updated == null) throw new ReservationConflictException();
            await ReservationOutbox.EnqueueAsync(connection, transaction, updated, "reservation_status_updated");
            await transaction.CommitAsync();
            return updated;
        }

        public async Task<IEnumerable<ReservationOut>> GetReservations(SearchReservations searchReservations)
        {
            List<string> filters = ["1=1"];
            if (searchReservations.Statuses != null && searchReservations.Statuses.Any())
            {
                filters.Add(@"R.""Status"" = ANY(@Statuses)");
            }
            if (searchReservations.tableId != null)
            {
                filters.Add(@"T.""Id"" = @tableId");
            }
            if (searchReservations.IdUser != null)
            {
                filters.Add(@"R.""UserId"" = @IdUser");
            }
            if (searchReservations.minDate != null)
            {
                filters.Add(@"CAST(R.""ReservationDate"" AS DATE) >= CAST(@minDateString AS DATE)");
            }
            if (searchReservations.maxDate != null)
            {
                filters.Add(@"CAST(R.""ReservationDate"" AS DATE) <= CAST(@maxDateString AS DATE)");
            }
            if (searchReservations.restaurantId != null)
            {
                filters.Add(@"R.""RestaurantId"" = @restaurantId");
            }

            var query = $@"
        SELECT 
            R.""Id"", R.""UserId"", R.""TableId"", R.""RestaurantId"", R.""ReservationDate"", R.""NumberOfPeople"", R.""SpecialRequest"", R.""GuestName"", R.""GuestPhone"", R.""CreatedAt"", R.""Status"",
            U.""Id"", U.""FirstName"", U.""LastName"", U.""Email"", U.""AccountType"", U.""CreatedAt"",
            T.""Id"", T.""RestaurantId"", T.""RoomId"", T.""TableNumber"", T.""NumberOfSeats"", T.""Shape"",
            T.""PositionX"", T.""PositionY"", T.""Width"", T.""Height"", T.""RotationDegrees"", T.""CreatedAt"",
            Res.""Id"", Res.""RestaurantName"", Res.""StreetNumber"", Res.""StreetName"", Res.""PostalCode"", Res.""City"", Res.""CuisineType""
        FROM ""Reservation"" R
        LEFT JOIN ""User"" U ON R.""UserId"" = U.""Id""
        LEFT JOIN ""TableEntity"" T ON R.""TableId"" = T.""Id""
        LEFT JOIN ""Restaurant"" Res ON R.""RestaurantId"" = Res.""Id""
        WHERE 
            {string.Join(" AND ", filters)}
        ORDER BY R.""ReservationDate"" ASC
        LIMIT @PageSize OFFSET @Offset;";

            using (var connection = _connections.CreateConnection())
            {
                // On ajoute RestaurantOut dans la liste des types génériques
                // Ordre : <T1, T2, T3, T4, TReturn>
                var reservations = await connection.QueryAsync<ReservationOut, UserOut, TableEntityOut, RestaurantOut, ReservationOut>(
                    query,
                    (reservation, user, table, restaurant) =>
                    {
                        reservation.User = user;
                        reservation.Table = table;
                        reservation.Restaurant = restaurant; // On lie le restaurant
                        return reservation;
                    },
                    new
                    {
                        Offset = searchReservations.Offset ?? 0,
                        PageSize = searchReservations.PageSize ?? 20,
                        Statuses = searchReservations.Statuses?.Select(status => (short)status).ToArray(),
                        searchReservations.IdUser,
                        searchReservations.tableId,
                        searchReservations.minDateString,
                        searchReservations.maxDateString,
                        searchReservations.restaurantId
                    },
                    splitOn: "Id,Id,Id" // ✅ On ajoute un "Id" pour marquer le début du Restaurant
                );

                return reservations;
            }
        }

        public async Task<IEnumerable<ReservationAvailabilityOut>> GetAvailability(SearchReservationAvailability search)
        {
            if (!search.IsValid) throw new ReservationRuleException("Une journée unique et un restaurant sont obligatoires.");
            using var connection = _connections.CreateConnection();
            return await connection.QueryAsync<ReservationAvailabilityOut>("""
                SELECT "TableId", "ReservationDate" FROM "Reservation"
                WHERE "RestaurantId"=@RestaurantId AND "Status"=1 AND "TableId" IS NOT NULL
                    AND "ReservationDate">@Start AND "ReservationDate"<@End
                ORDER BY "ReservationDate";
                """, new { RestaurantId = search.restaurantId, Start = search.minDate!.Value.ToDateTime(TimeOnly.MinValue).AddMinutes(-90),
                    End = search.maxDate!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddMinutes(90) });
        }
        public async Task<ReservationOut?> GetMyReservationById(long id)
        {
            var query = @"
                SELECT 
                R.""Id"",
                R.""UserId"", 
                R.""TableId"", 
                R.""RestaurantId"", 
                R.""ReservationDate"", 
                R.""NumberOfPeople"", 
                R.""SpecialRequest"", 
                R.""GuestName"",
                R.""GuestPhone"",
                R.""CreatedAt"",
                R.""Status"",
                U.""Id"" AS ""Id"",
                U.""FirstName"", 
                U.""LastName"", 
                U.""Email"", 
                U.""AccountType"", 
                U.""CreatedAt"",
                T.""Id"" AS ""Id"",
                T.""RestaurantId"",
                T.""RoomId"",
                T.""TableNumber"",
                T.""NumberOfSeats"",
                T.""Shape"",
                T.""PositionX"",
                T.""PositionY"",
                T.""Width"",
                T.""Height"",
                T.""RotationDegrees"",
                T.""CreatedAt""
            FROM ""Reservation"" R
            LEFT JOIN ""User"" U ON R.""UserId"" = U.""Id""
            LEFT JOIN ""TableEntity"" T ON R.""TableId"" = T.""Id""
            WHERE 
                R.""Id"" = @Id;";

            using (var connection = _connections.CreateConnection())
            {
                var reservations = await connection.QueryAsync<ReservationOut, UserOut, TableEntityOut, ReservationOut>(
                    query,
                    (reservation, user, table) =>
                    {
                        reservation.User = user;
                        reservation.Table = table;
                        return reservation;
                    },
                    new
                    {
                        Id = id,
                    },
                    splitOn: "Id,Id" // ✅ Correction ici
                );


                return reservations.FirstOrDefault();
            }
        }
        public async Task<ReservationOut?> Delete(long id, ReservationStatus expectedStatus)
        {
            using var connection = _connections.CreateConnection();
            await ((NpgsqlConnection)connection).OpenAsync();
            await using var transaction = await ((NpgsqlConnection)connection).BeginTransactionAsync();
            var reservation = await LockReservationAsync(connection, transaction, id);
            if (reservation == null || expectedStatus != ReservationStatus.EnAttente || reservation.Status != expectedStatus)
                throw new ReservationConflictException();
            var deleted = await connection.QuerySingleOrDefaultAsync<ReservationOut>("""
                DELETE FROM "Reservation" WHERE "Id"=@id AND "Status"=@Expected RETURNING *;
                """, new { id, Expected = (short)expectedStatus }, transaction);
            if (deleted == null) throw new ReservationConflictException();
            await ReservationOutbox.EnqueueAsync(connection, transaction, deleted, "reservation_cancelled");
            await transaction.CommitAsync();
            return deleted;
        }
    }
}
