namespace TableMasterApi.Service
{
    public sealed class ReservationConflictException : Exception
    {
        public ReservationConflictException()
            : base("La table est déjà occupée dans la fenêtre de sécurité de 1 h 30.")
        {
        }
    }
}
