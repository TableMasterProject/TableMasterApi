namespace TableMasterApi.Service
{
    public class GoogleMapsException : Exception
    {
        public int StatusCode { get; }
        public string? GoogleStatus { get; }

        public GoogleMapsException(int statusCode, string message, string? googleStatus = null, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            GoogleStatus = googleStatus;
        }
    }
}
