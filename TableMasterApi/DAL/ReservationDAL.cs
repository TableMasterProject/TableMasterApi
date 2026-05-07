using Dapper;
using Microsoft.Data.SqlClient;
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
                INSERT INTO [Reservation] (UserId, TableId, RestaurantId, ReservationDate, NumberOfPeople, SpecialRequest, Status)
                OUTPUT 
                    INSERTED.Id, 
                    INSERTED.UserId,
                    INSERTED.TableId,
                    INSERTED.RestaurantId,
                    INSERTED.ReservationDate,
                    INSERTED.NumberOfPeople,
                    INSERTED.SpecialRequest,
                    INSERTED.CreatedAt,
                    INSERTED.Status
                VALUES (@UserId, @TableId, @RestaurantId, @ReservationDate, @NumberOfPeople, @SpecialRequest, @Status);";

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                var result = await connection.QuerySingleAsync<ReservationOut>(query, reservation);
                return result;
            }
        }

        public async Task<ReservationOut?> UpdateReservationStatus(long id, ReservationStatus reservationStatus)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = @"
                            UPDATE [Reservation] 
                                SET Status = @reservationStatus
                            OUTPUT 
                                INSERTED.Id, 
                                INSERTED.UserId,
                                INSERTED.TableId,
                                INSERTED.RestaurantId,
                                INSERTED.ReservationDate,
                                INSERTED.NumberOfPeople,
                                INSERTED.SpecialRequest,
                                INSERTED.CreatedAt,
                                INSERTED.Status
                            WHERE Id = @Id";
                var updatedReservation = await connection.QuerySingleAsync<ReservationOut>(query, new
                {
                    Id = id,
                    reservationStatus,
                });
                return updatedReservation;
            }
        }

        public async Task<IEnumerable<ReservationOut>> GetReservations(SearchReservations searchReservations)
        {
            List<string> filters = ["1=1"];
            if (searchReservations.Statuses != null && searchReservations.Statuses.Any())
            {
                filters.Add("R.Status IN @Statuses");
            }
            if (searchReservations.tableId != null)
            {
                filters.Add("T.Id = @tableId");
            }
            if (searchReservations.IdUser != null)
            {
                filters.Add("R.UserId = @IdUser");
            }
            if (searchReservations.minDate != null)
            {
                filters.Add("CAST(R.ReservationDate AS DATE) >= @minDateString");
            }
            if (searchReservations.maxDate != null)
            {
                filters.Add("CAST(R.ReservationDate AS DATE) <= @maxDateString");
            }
            if (searchReservations.restaurantId != null)
            {
                filters.Add("R.RestaurantId = @restaurantId");
            }

            var query = $@"
        SELECT 
            R.Id, R.UserId, R.TableId, R.RestaurantId, R.ReservationDate, R.NumberOfPeople, R.SpecialRequest, R.CreatedAt, R.Status,
            U.Id, U.FirstName, U.LastName, U.Email, U.AccountType, U.CreatedAt,
            T.Id, T.RestaurantId, T.TableNumber, T.NumberOfSeats, T.CreatedAt,
            Res.Id, Res.RestaurantName, Res.StreetNumber, Res.StreetName, Res.PostalCode, Res.City, Res.CuisineType -- Ajout des colonnes Restaurant
        FROM [Reservation] R
        JOIN [User] U ON R.UserId = U.Id
        JOIN [TableEntity] T ON R.TableId = T.Id
        JOIN [Restaurant] Res ON R.RestaurantId = Res.Id -- Nouveau JOIN
        WHERE 
            {string.Join(" AND ", filters)}
        ORDER BY R.ReservationDate ASC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using (var connection = new SqlConnection(_config.ConnectionString))
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
                        searchReservations.Offset,
                        searchReservations.PageSize,
                        searchReservations.Statuses,
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
                R.Id,
                R.UserId, 
                R.TableId, 
                R.RestaurantId, 
                R.ReservationDate, 
                R.NumberOfPeople, 
                R.SpecialRequest, 
                R.CreatedAt,
                R.Status,
                U.Id AS Id,
                U.FirstName, 
                U.LastName, 
                U.Email, 
                U.Password, 
                U.AccountType, 
                U.CreatedAt,
                T.Id AS Id,
                T.RestaurantId, 
                T.TableNumber, 
                T.NumberOfSeats, 
                T.CreatedAt
            FROM [Reservation] R
            JOIN [User] U ON R.UserId = U.Id
            JOIN [TableEntity] T ON R.TableId = T.Id
            WHERE 
                R.Id = @Id;";

            using (var connection = new SqlConnection(_config.ConnectionString))
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
            var selectQuery = @"SELECT * FROM [Reservation] WHERE Id = @Id;";
            var deleteQuery = @"DELETE FROM [Reservation] WHERE Id = @Id;";

            using (var connection = new SqlConnection(_config.ConnectionString))
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
