namespace TableMasterApi.Model;

/// <summary>Une journée de disponibilité complète, sans pagination.</summary>
public sealed class SearchReservationAvailability
{
    public long? restaurantId { get; set; }
    public DateOnly? minDate { get; set; }
    public DateOnly? maxDate { get; set; }
    public bool IsValid => restaurantId > 0 && minDate.HasValue && minDate == maxDate && minDate > DateOnly.MinValue && maxDate < DateOnly.MaxValue;
}
