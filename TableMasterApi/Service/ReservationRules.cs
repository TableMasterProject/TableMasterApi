using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TableMasterApi.Model;

namespace TableMasterApi.Service;

public sealed class ReservationRuleException(string message) : Exception(message);

public sealed class ReservationRules(TimeProvider clock)
{
    public static TimeZoneInfo Paris { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    public static DateTime Normalize(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Unspecified ? value : TimeZoneInfo.ConvertTimeFromUtc(value.ToUniversalTime(), Paris);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Paris.IsInvalidTime(local) || Paris.IsAmbiguousTime(local))
            throw new ReservationRuleException("Cette heure est inexistante ou ambiguë dans le fuseau Europe/Paris.");
        return local;
    }

    public void Validate(DateTime date, bool quick, IEnumerable<DailyActivityOut> hours, IEnumerable<ClosedDayExceptionOut> closedDays)
    {
        date = Normalize(date);
        var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Paris).DateTime;
        if (date < now || (!quick && date <= now.AddMinutes(30)))
            throw new ReservationRuleException("Le créneau doit être futur et respecter le préavis de 30 minutes.");
        if (closedDays.Any(day => date.Date >= day.ExceptionDateBegin.Date && date.Date <= day.ExceptionDateEnd.Date))
            throw new ReservationRuleException("Le restaurant est fermé à cette date.");
        var weekDay = (int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek;
        if (!quick && !hours.Any(hour => hour.DayOfWeek == weekDay && date.TimeOfDay >= hour.StartTime && date.TimeOfDay < hour.EndTime &&
            (date.TimeOfDay - hour.StartTime).Ticks % TimeSpan.FromMinutes(30).Ticks == 0))
            throw new ReservationRuleException("Le créneau ne correspond pas aux horaires du restaurant.");
    }
}

public sealed class ParisReservationDateConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        try
        {
            if (raw == null) throw new FormatException();
            DateTime date;
            if (raw.EndsWith('Z') || raw.LastIndexOf('+') > 10 || raw.LastIndexOf('-') > 10)
                date = TimeZoneInfo.ConvertTime(DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture), ReservationRules.Paris).DateTime;
            else
                date = DateTime.SpecifyKind(DateTime.Parse(raw, CultureInfo.InvariantCulture), DateTimeKind.Unspecified);
            return ReservationRules.Normalize(date);
        }
        catch (Exception ex) when (ex is FormatException or ReservationRuleException)
        {
            throw new JsonException("Date de réservation invalide pour Europe/Paris.");
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Unspecified).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
}
