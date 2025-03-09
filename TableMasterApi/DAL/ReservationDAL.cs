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

        public async Task<IEnumerable<ReservationOut>> GetReservationsByRestaurantAsync(long restaurantId, DateOnly reservationDate)
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
                T.RestaurantId = @RestaurantId
                AND CONVERT(VARCHAR, R.ReservationDate, 23) = @ReservationDate
            ORDER BY R.ReservationDate ASC;";

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
                        RestaurantId = restaurantId,
                        ReservationDate = reservationDate.ToString("yyyy-MM-dd") // Conversion en string
                    },
                    splitOn: "Id,Id" // ✅ Correction ici
                );


                return reservations;
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
        public async Task<TableEntityOut?> Delete(long idUser, long id)
        {
            var selectQuery = @"SELECT * FROM [Reservation] WHERE Id = @Id AND UserId = @UserId;";
            var deleteQuery = @"DELETE FROM [Reservation] WHERE Id = @Id AND UserId = @UserId;";

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                var reservation = await connection.QueryFirstOrDefaultAsync<TableEntityOut>(selectQuery, new
                {
                    Id = id,
                    UserId = idUser
                });

                if (reservation == null)
                    return null; // La réservation n'existe pas

                var affectedRows = await connection.ExecuteAsync(deleteQuery, new
                {
                    Id = id,
                    UserId = idUser
                });

                return affectedRows > 0 ? reservation : null;
            }
        }

    }
}
