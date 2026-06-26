namespace TableMasterApi.Service
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }

    public sealed class EmailMessage
    {
        public required string ToEmail { get; init; }
        public string? ToName { get; init; }
        public required string Subject { get; init; }
        public required string HtmlContent { get; init; }
        public required string TextContent { get; init; }
    }
}
