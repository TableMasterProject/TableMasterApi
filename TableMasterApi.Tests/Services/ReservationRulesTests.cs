using System.Text.Json;

namespace TableMasterApi.Tests.Services;

public class ReservationRulesTests
{
    private readonly ReservationRules rules = new(new FixedClock());
    private static readonly DailyActivityOut[] Hours = [new() { DayOfWeek = 1, StartTime = new(12, 15, 0), EndTime = new(14, 15, 0) }];

    [Theory]
    [InlineData(12, 45, true)]
    [InlineData(12, 30, false)]
    [InlineData(14, 15, false)]
    [InlineData(12, 15, false)]
    public void ClientSlotsRespectStartStepEndAndNotice(int hour, int minute, bool valid)
    {
        Action validate = () => rules.Validate(new DateTime(2026, 7, 20, hour, minute, 0), false, Hours, []);
        if (valid) validate.Should().NotThrow(); else validate.Should().Throw<ReservationRuleException>();
    }

    [Fact]
    public void QuickAllowsOutsideHoursButRejectsPastAndClosures()
    {
        rules.Validate(new(2026, 7, 20, 12, 1, 0), true, [], []);
        Action past = () => rules.Validate(new(2026, 7, 20, 11, 59, 0), true, [], []);
        past.Should().Throw<ReservationRuleException>();
        Action closed = () => rules.Validate(new(2026, 7, 20, 16, 0, 0), true, [], [new() { ExceptionDateBegin = new(2026, 7, 20), ExceptionDateEnd = new(2026, 7, 20) }]);
        closed.Should().Throw<ReservationRuleException>();
    }

    [Theory]
    [InlineData("2026-03-29T02:30:00")]
    [InlineData("2026-10-25T02:30:00")]
    [InlineData("2026-10-25T00:30:00Z")]
    public void InvalidOrAmbiguousParisHoursAreRejected(string date)
    {
        Action parse = () => JsonSerializer.Deserialize<ReservationIn>("{\"ReservationDate\":\"" + date + "\"}");
        parse.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("2026-07-20T12:45:00", 12)]
    [InlineData("2026-07-20T10:45:00Z", 12)]
    [InlineData("2026-07-20T13:45:00+03:00", 12)]
    public void OffsetInputsBecomeNaiveParisDates(string date, int expectedHour)
    {
        var reservation = JsonSerializer.Deserialize<ReservationIn>("{\"ReservationDate\":\"" + date + "\"}")!;
        reservation.ReservationDate.Hour.Should().Be(expectedHour);
        reservation.ReservationDate.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
    }
}
