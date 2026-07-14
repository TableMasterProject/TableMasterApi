using System.Collections.Generic;
using System.Threading.Tasks;
using TableMasterApi.Model;

namespace TableMasterApi.DAL.Interfaces
{
    /// <summary>
    /// Interface pour la gestion des réservations
    /// </summary>
    public interface IReservationDAL
    {
        Task<ReservationOut> CreateReservationAsync(ReservationIn reservation);
        Task<ReservationOut?> UpdateReservationStatus(long id, ReservationStatus reservationStatus);
        Task<IEnumerable<ReservationOut>> GetReservations(SearchReservations searchReservations);
        Task<IEnumerable<ReservationAvailabilityOut>> GetAvailability(SearchReservations searchReservations);
        Task<ReservationOut?> GetMyReservationById(long id);
        Task<ReservationOut?> Delete(long id);
    }
}
