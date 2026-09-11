using System.Net;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;

namespace TableMasterApi.Service
{
    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly IEmailService _emailService;
        private readonly IAppLinkService _appLinkService;
        private readonly IUserDAL _userDAL;
        private readonly ILogger<EmailNotificationService> _logger;

        public EmailNotificationService(IEmailService emailService, IAppLinkService appLinkService, IUserDAL userDAL, ILogger<EmailNotificationService> logger)
        {
            _emailService = emailService;
            _appLinkService = appLinkService;
            _userDAL = userDAL;
            _logger = logger;
        }

        public async Task SendPasswordResetAsync(UserOut user, string resetLink, CancellationToken cancellationToken = default)
        {
            var displayName = FormatName(user.FirstName, user.LastName);
            await SendAsync(new EmailMessage
            {
                ToEmail = user.Email,
                ToName = displayName,
                Subject = "Reinitialisation de votre mot de passe TableMaster",
                HtmlContent = BuildHtml(
                    "Reinitialisation de mot de passe",
                    $"Bonjour {Html(displayName)},",
                    "Vous avez demande a reinitialiser votre mot de passe. Ce lien est valable pendant 1 heure.",
                    resetLink,
                    "Choisir un nouveau mot de passe"),
                TextContent = $"Bonjour {displayName},\n\nVous avez demande a reinitialiser votre mot de passe. Ce lien est valable pendant 1 heure :\n{resetLink}\n\nSi vous n'etes pas a l'origine de cette demande, ignorez cet email."
            }, cancellationToken);
        }

        public async Task SendReservationCreatedAsync(ReservationOut reservation, RestaurantOut restaurant, CancellationToken cancellationToken = default)
        {
            var reservationLink = _appLinkService.BuildReservationLink(reservation.Id);
            var reservationDate = FormatReservationDate(reservation.ReservationDate);
            var restaurantName = restaurant.RestaurantName ?? "votre restaurant";

            await SendUserReservationEmailAsync(
                reservation.UserId,
                reservation.Status == ReservationStatus.Validee ? "Votre reservation est confirmee" : "Votre reservation est enregistree",
                reservation.Status == ReservationStatus.Validee ? "Reservation confirmee" : "Reservation enregistree",
                $"Votre reservation chez {restaurantName} pour le {reservationDate} a bien ete {(reservation.Status == ReservationStatus.Validee ? "confirmee" : "enregistree")}.",
                reservationLink,
                cancellationToken);

            await SendUserReservationEmailAsync(
                restaurant.UserId,
                "Nouvelle reservation",
                "Nouvelle reservation",
                $"Une nouvelle reservation est prevue chez {restaurantName} le {reservationDate} pour {reservation.NumberOfPeople} personne(s).",
                reservationLink,
                cancellationToken);
        }

        public async Task SendReservationStatusUpdatedAsync(ReservationOut reservation, RestaurantOut restaurant, ReservationStatus status, CancellationToken cancellationToken = default)
        {
            var reservationLink = _appLinkService.BuildReservationLink(reservation.Id);
            var statusLabel = FormatStatus(status);
            var reservationDate = FormatReservationDate(reservation.ReservationDate);
            var restaurantName = restaurant.RestaurantName ?? "le restaurant";

            await SendUserReservationEmailAsync(
                reservation.UserId,
                $"Reservation {statusLabel}",
                "Statut de reservation mis a jour",
                $"Votre reservation chez {restaurantName} pour le {reservationDate} est maintenant : {statusLabel}.",
                reservationLink,
                cancellationToken);

            await SendUserReservationEmailAsync(
                restaurant.UserId,
                $"Reservation {statusLabel}",
                "Statut de reservation mis a jour",
                $"La reservation du {reservationDate} est maintenant : {statusLabel}.",
                reservationLink,
                cancellationToken);
        }

        public async Task SendReservationCancelledAsync(ReservationOut reservation, RestaurantOut? restaurant, CancellationToken cancellationToken = default)
        {
            var reservationLink = _appLinkService.BuildReservationLink(reservation.Id);
            var reservationDate = FormatReservationDate(reservation.ReservationDate);
            var restaurantName = restaurant?.RestaurantName ?? "le restaurant";

            await SendUserReservationEmailAsync(
                reservation.UserId,
                "Reservation annulee",
                "Reservation annulee",
                $"Votre reservation chez {restaurantName} pour le {reservationDate} a ete annulee.",
                reservationLink,
                cancellationToken);

            if (restaurant != null)
            {
                await SendUserReservationEmailAsync(
                    restaurant.UserId,
                    "Reservation annulee",
                    "Reservation annulee",
                    $"La reservation du {reservationDate} a ete annulee.",
                    reservationLink,
                    cancellationToken);
            }
        }

        private async Task SendUserReservationEmailAsync(long? userId, string subject, string title, string body, string link, CancellationToken cancellationToken)
        {
            if (userId == null)
            {
                return;
            }

            var user = await _userDAL.GetUserById(userId.Value);
            if (user == null)
            {
                return;
            }

            var displayName = FormatName(user.FirstName, user.LastName);
            await SendAsync(new EmailMessage
            {
                ToEmail = user.Email,
                ToName = displayName,
                Subject = subject,
                HtmlContent = BuildHtml(title, $"Bonjour {Html(displayName)},", body, link, "Ouvrir la reservation"),
                TextContent = $"Bonjour {displayName},\n\n{body}\n\nOuvrir la reservation : {link}"
            }, cancellationToken);
        }

        private async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (!await _emailService.SendEmailAsync(message, cancellationToken))
                throw new NotificationDeliveryException("email_unavailable");
        }

        private static string BuildHtml(string title, string greeting, string body, string link, string buttonLabel)
        {
            var encodedLink = Html(link);

            return $"""
                <!doctype html>
                <html lang="fr">
                <body style="font-family: Arial, sans-serif; color: #1f2937; line-height: 1.5;">
                    <h1 style="font-size: 22px;">{Html(title)}</h1>
                    <p>{greeting}</p>
                    <p>{Html(body)}</p>
                    <p>
                        <a href="{encodedLink}" style="display: inline-block; padding: 10px 16px; background: #0f766e; color: #ffffff; text-decoration: none; border-radius: 6px;">
                            {Html(buttonLabel)}
                        </a>
                    </p>
                    <p style="font-size: 13px; color: #6b7280;">Si le bouton ne fonctionne pas, copiez ce lien dans votre navigateur :<br>{encodedLink}</p>
                </body>
                </html>
                """;
        }

        private static string FormatName(string firstName, string lastName)
        {
            var name = $"{firstName} {lastName}".Trim();
            return string.IsNullOrWhiteSpace(name) ? "TableMaster" : name;
        }

        private static string FormatReservationDate(DateTime reservationDate)
        {
            return reservationDate.ToString("dd/MM/yyyy HH:mm");
        }

        private static string FormatStatus(ReservationStatus status)
        {
            return status switch
            {
                ReservationStatus.EnAttente => "en attente",
                ReservationStatus.Validee => "confirmee",
                ReservationStatus.Finie => "terminee",
                ReservationStatus.AnnuleeParResto => "annulee par le restaurant",
                ReservationStatus.AnnuleeParClient => "annulee par le client",
                _ => status.ToString()
            };
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value);
        }
    }
}
