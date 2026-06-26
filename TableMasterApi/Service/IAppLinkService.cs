namespace TableMasterApi.Service
{
    public interface IAppLinkService
    {
        string BuildPasswordResetLink(string token);
        string BuildReservationLink(long reservationId);
    }
}
