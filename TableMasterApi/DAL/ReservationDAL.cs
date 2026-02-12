using Dapper;
using Microsoft.Data.SqlClient;
using TableMasterApi.Model;
using TableMasterApi.Service;

namespace TableMasterApi.DAL
{
    public class ReservationDAL
    {
        private readonly ConfigPerso _config;

        public ReservationDAL(ConfigPerso config)
        {
            _config = config;
        }

        public async Task<IEnumerable<ReservationOut>> GetReservationsByRestaurantAsync(long restaurantId, DateOnly reservationDate, long? tableId = null)
        {
            var query = @"
                SELECT 
                    R.Id, R.UserId, R.TableId, R.RestaurantId, R.ReservationDate, R.NumberOfPeople, 
                    R.SpecialRequest, R.CreatedAt, R.IsValidate,
                    U.Id AS Id, U.FirstName, U.LastName, U.Email, U.AccountType, U.CreatedAt,
                    T.Id AS Id, T.RestaurantId, T.TableNumber, T.NumberOfSeats, T.CreatedAt
                FROM [Reservation] R
                JOIN [User] U ON R.UserId = U.Id
                JOIN [TableEntity] T ON R.TableId = T.Id
                WHERE 
                    T.RestaurantId = @RestaurantId
                    AND CAST(R.ReservationDate AS DATE) >= @ReservationDate
                    AND R.IsValidate = 1
                    AND (@TableId IS NULL OR T.Id = @TableId) -- Filtre optionnel
                ORDER BY R.ReservationDate ASC;";

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                return await connection.QueryAsync<ReservationOut, UserOut, TableEntityOut, ReservationOut>(
                    query,
                    (reservation, user, table) => {
                        reservation.User = user;
                        reservation.Table = table;
                        return reservation;
                    },
                    new {
                        RestaurantId = restaurantId,
                        ReservationDate = reservationDate.ToString("yyyy-MM-dd"),
                        TableId = tableId // Dapper gère le null automatiquement
                    },
                    splitOn: "Id,Id"
                );
            }
        }

        public async Task<IEnumerable<ReservationOut>> GetPendingReservationsAsync(long restaurantId, long? tableId = null)
        {
            var query = @"
                SELECT 
                    R.*, 
                    U.Id AS Id, U.FirstName, U.LastName, U.Email, U.AccountType, U.CreatedAt,
                    T.Id AS Id, T.RestaurantId, T.TableNumber, T.NumberOfSeats, T.CreatedAt
                FROM [Reservation] R
                JOIN [User] U ON R.UserId = U.Id
                JOIN [TableEntity] T ON R.TableId = T.Id
                WHERE 
                    R.RestaurantId = @RestaurantId
                    AND R.IsValidate = 0
                    AND (@TableId IS NULL OR T.Id = @TableId) -- Filtre optionnel
                ORDER BY R.ReservationDate ASC;";

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                return await connection.QueryAsync<ReservationOut, UserOut, TableEntityOut, ReservationOut>(
                    query,
                    (reservation, user, table) => {
                        reservation.User = user;
                        reservation.Table = table;
                        return reservation;
                    },
                    new { RestaurantId = restaurantId, TableId = tableId },
                    splitOn: "Id,Id"
                );
            }
        }

        public async Task<ReservationOut> CreateReservationAsync(ReservationIn reservation)
        {
            var query = @"
                INSERT INTO [Reservation] (UserId, TableId, RestaurantId, ReservationDate, NumberOfPeople, SpecialRequest, IsValidate)
                OUTPUT 
                    INSERTED.Id, 
                    INSERTED.UserId,
                    INSERTED.TableId,
                    INSERTED.RestaurantId,
                    INSERTED.ReservationDate,
                    INSERTED.NumberOfPeople,
                    INSERTED.SpecialRequest,
                    INSERTED.CreatedAt,
                    INSERTED.IsValidate
                VALUES (@UserId, @TableId, @RestaurantId, @ReservationDate, @NumberOfPeople, @SpecialRequest, @IsValidate);";

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                var result = await connection.QuerySingleAsync<ReservationOut>(query, reservation);
                return result;
            }
        }

        public async Task<ReservationOut?> validateReservation(long id,bool IsValidate)
        {
            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                connection.Open();
                var query = @"
                            UPDATE [Reservation] 
                                SET IsValidate = @IsValidate
                            OUTPUT 
                                INSERTED.Id, 
                                INSERTED.UserId,
                                INSERTED.TableId,
                                INSERTED.RestaurantId,
                                INSERTED.ReservationDate,
                                INSERTED.NumberOfPeople,
                                INSERTED.SpecialRequest,
                                INSERTED.CreatedAt,
                                INSERTED.IsValidate
                            WHERE Id = @Id";
                var updatedReservation = await connection.QuerySingleAsync<ReservationOut>(query, new
                {
                    Id = id,
                    IsValidate,
                });
                return updatedReservation;
            }
        }

        internal async Task<IEnumerable<ReservationOut>> GetMyReservations(long idUserToken, SearchReservations searchReservations)
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
                R.IsValidate,
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
                R.UserId = @UserId
            ORDER BY R.ReservationDate ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

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
                        UserId = idUserToken,
                        searchReservations.Offset,
                        searchReservations.PageSize,
                    },
                    splitOn: "Id,Id" // ✅ Correction ici
                );


                return reservations;
            }
        }
        internal async Task<ReservationOut?> GetMyReservationById(long id)
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
                R.IsValidate,
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
