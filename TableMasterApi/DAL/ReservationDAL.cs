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
        private readonly ConfigPerso _config;

        public ReservationDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<ReservationOut> CreateReservationAsync(ReservationIn reservation)
        {
            var query = @"
                INSERT INTO ""Reservation"" (""UserId"", ""TableId"", ""RestaurantId"", ""ReservationDate"", ""NumberOfPeople"", ""SpecialRequest"", ""GuestName"", ""GuestPhone"", ""Status"")
                VALUES (@UserId, @TableId, @RestaurantId, @ReservationDate, @NumberOfPeople, @SpecialRequest, @GuestName, @GuestPhone, @Status)
                RETURNING ""Id"", ""UserId"", ""TableId"", ""RestaurantId"", ""ReservationDate"", ""NumberOfPeople"", ""SpecialRequest"", ""GuestName"", ""GuestPhone"", ""CreatedAt"", ""Status"";";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                var result = await connection.QuerySingleAsync<ReservationOut>(query, new
                {
                    reservation.UserId,
                    reservation.TableId,
                    reservation.RestaurantId,
                    reservation.ReservationDate,
                    reservation.NumberOfPeople,
                    reservation.SpecialRequest,
                    reservation.GuestName,
                    reservation.GuestPhone,
                    Status = (short)reservation.Status
                });
                return result;
            }
        }

        public async Task<ReservationOut?> UpdateReservationStatus(long id, ReservationStatus reservationStatus)
        {
            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = @"
                            UPDATE ""Reservation"" 
                                SET ""Status"" = @ReservationStatus
                            WHERE ""Id"" = @Id
                            RETURNING ""Id"", ""UserId"", ""TableId"", ""RestaurantId"", ""ReservationDate"", ""NumberOfPeople"", ""SpecialRequest"", ""GuestName"", ""GuestPhone"", ""CreatedAt"", ""Status""";
                var updatedReservation = await connection.QuerySingleAsync<ReservationOut>(query, new
                {
                    Id = id,
                    ReservationStatus = (short)reservationStatus,
                });
                return updatedReservation;
            }
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

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
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

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
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
        public async Task<ReservationOut?> Delete(long id)
        {
            var selectQuery = @"SELECT * FROM ""Reservation"" WHERE ""Id"" = @Id;";
            var deleteQuery = @"DELETE FROM ""Reservation"" WHERE ""Id"" = @Id;";

            using (var connection = new NpgsqlConnection(_config.ConnectionString))
            {
                var reservation = await connection.QueryFirstOrDefaultAsync<ReservationOut>(selectQuery, new
                {
                    Id = id
                });

                if (reservation == null)
                    return null; // La réservation n'existe pas

                var affectedRows = await connection.ExecuteAsync(deleteQuery, new
                {
                    Id = id
                });

                return affectedRows > 0 ? reservation : null;
            }
        }

    }
}
