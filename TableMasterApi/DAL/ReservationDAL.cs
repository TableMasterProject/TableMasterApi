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
                    U.Id AS UserId, 
                    U.Name AS UserName, 
                    T.Id AS TableId, 
                    T.Number AS TableNumber
                FROM [Reservation] R
                JOIN [User] U ON R.UserId = U.Id
                JOIN [Table] T ON R.TableId = T.Id
                WHERE 
                    T.RestaurantId = @RestaurantId
                    AND CAST(R.ReservationDate AS DATE) = @ReservationDate
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
                        ReservationDate = reservationDate // Prend uniquement la partie Date
                    }
                );

                return reservations;
            }
        }

        public async Task<ReservationOut> CreateReservationAsync(ReservationIn reservation)
        {
            var query = @"
                INSERT INTO [Reservation] (UserId, TableId, RestaurantId, ReservationDate, NumberOfPeople, SpecialRequest, CreatedAt)
                OUTPUT 
                    INSERTED.Id, 
                    INSERTED.UserId,
                    INSERTED.TableId,
                    INSERTED.RestaurantId,
                    INSERTED.ReservationDate,
                    INSERTED.NumberOfPeople,
                    INSERTED.SpecialRequest,
                    INSERTED.CreatedAt
                VALUES (@UserId, @TableId, @RestaurantId, @ReservationDate, @NumberOfPeople, @SpecialRequest, GETDATE());";

            using (var connection = new SqlConnection(_config.ConnectionString))
            {
                var result = await connection.QuerySingleAsync<ReservationOut>(query, reservation);
                return result;
            }
        }



    }
}
