using Dapper;
using Npgsql;
using TableMasterApi.DAL;

namespace TableMasterApi.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class ReservationDalIntegrationTests(PostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private ReservationDAL Dal => new(new PostgresConnectionFactory(fixture.Config), new ReservationRules(new FixedClock()));
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2036, 7, 21, 8, 0, 0, TimeSpan.Zero); }

    private async Task Seed()
    {
        fixture.IsAvailable.Should().BeTrue(fixture.UnavailableReason);
        await using var c = new NpgsqlConnection(fixture.ConnectionString);
        await c.ExecuteAsync("""
            INSERT INTO "User" ("Email","Password","FirstName","LastName") VALUES ('owner@example.test','x','O','W'),('client@example.test','x','C','L');
            INSERT INTO "Restaurant" ("UserId","RestaurantName") VALUES (1,'Tests');
            INSERT INTO "TableEntity" ("RestaurantId","TableNumber","NumberOfSeats") VALUES (1,1,4);
            INSERT INTO "DailyActivity" ("RestaurantId","DayOfWeek","StartTime","EndTime") VALUES (1,1,'12:00','23:59');
            """);
    }
    private static ReservationIn Input(int hour = 20) => new() { UserId = 2, TableId = 1, RestaurantId = 1, NumberOfPeople = 2, ReservationDate = new(2036,7,21,hour,0,0), Status = ReservationStatus.Validee };

    [Fact]
    public async Task AvailabilityReturnsAllSlotsIncludingMidnightMargins()
    {
        await Seed();
        await using var c = new NpgsqlConnection(fixture.ConnectionString);
        await c.ExecuteAsync("""
            INSERT INTO "Reservation" ("UserId","TableId","RestaurantId","ReservationDate","NumberOfPeople","Status")
            SELECT 2,1,1,timestamp '2036-07-21 12:00' + n * interval '1 minute',2,1 FROM generate_series(1,210) n;
            INSERT INTO "Reservation" ("UserId","TableId","RestaurantId","ReservationDate","NumberOfPeople","Status")
            VALUES (2,1,1,'2036-07-20 23:30',2,1),(2,1,1,'2036-07-22 00:30',2,1);
            """);
        var slots = await Dal.GetAvailability(new SearchReservationAvailability { restaurantId = 1, minDate = new(2036,7,21), maxDate = new(2036,7,21) });
        slots.Should().HaveCount(212);
    }

    [Fact]
    public async Task ConcurrentCreatesHaveOneWinnerAndOnlyCommittedOutboxTasks()
    {
        await Seed();
        async Task<bool> Create() { try { await Dal.CreateReservationAsync(Input()); return true; } catch (ReservationConflictException) { return false; } }
        var results = await Task.WhenAll(Create(), Create());
        results.Count(x => x).Should().Be(1);
        await using var c = new NpgsqlConnection(fixture.ConnectionString);
        (await c.QuerySingleAsync<int>("SELECT count(*) FROM \"Reservation\"")).Should().Be(1);
        (await c.QuerySingleAsync<int>("SELECT count(*) FROM \"ReservationNotificationOutbox\"")).Should().Be(4);
    }

    [Fact]
    public async Task StaleStatusAndDeleteCannotOverwriteCommittedTransition()
    {
        await Seed();
        var input = Input(); input.Status = ReservationStatus.EnAttente;
        var created = await Dal.CreateReservationAsync(input);
        await Dal.UpdateReservationStatus(created.Id, ReservationStatus.Validee, ReservationStatus.EnAttente);
        Func<Task> stale = () => Dal.UpdateReservationStatus(created.Id, ReservationStatus.AnnuleeParResto, ReservationStatus.EnAttente);
        await stale.Should().ThrowAsync<ReservationConflictException>();
        Func<Task> delete = () => Dal.Delete(created.Id, ReservationStatus.EnAttente);
        await delete.Should().ThrowAsync<ReservationConflictException>();
    }

    [Fact]
    public async Task MidnightConflictAndExactNinetyMinuteBoundaryAreEnforced()
    {
        await Seed();
        var late = Input(); late.UserId = null; late.ReservationDate = new(2036, 7, 21, 23, 30, 0);
        await Dal.CreateReservationAsync(late);
        var nextDay = Input(); nextDay.UserId = null; nextDay.ReservationDate = new(2036, 7, 22, 0, 30, 0);
        Func<Task> conflict = () => Dal.CreateReservationAsync(nextDay);
        await conflict.Should().ThrowAsync<ReservationConflictException>();
        nextDay.ReservationDate = new(2036, 7, 22, 1, 0, 0);
        (await Dal.CreateReservationAsync(nextDay)).Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FailedOutboxInsertRollsBackReservationMutation()
    {
        await Seed();
        await using var c = new NpgsqlConnection(fixture.ConnectionString);
        await c.ExecuteAsync("""
            CREATE FUNCTION fail_test_outbox() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'test failure'; END; $$;
            CREATE TRIGGER fail_test_outbox BEFORE INSERT ON "ReservationNotificationOutbox" FOR EACH ROW EXECUTE FUNCTION fail_test_outbox();
            """);
        try
        {
            Func<Task> create = () => Dal.CreateReservationAsync(Input());
            await create.Should().ThrowAsync<PostgresException>();
            (await c.QuerySingleAsync<int>("SELECT count(*) FROM \"Reservation\"")).Should().Be(0);
            (await c.QuerySingleAsync<int>("SELECT count(*) FROM \"ReservationNotificationOutbox\"")).Should().Be(0);
        }
        finally
        {
            await c.ExecuteAsync("DROP TRIGGER fail_test_outbox ON \"ReservationNotificationOutbox\"; DROP FUNCTION fail_test_outbox();");
        }
    }

    [Fact]
    public async Task ValidationRechecksClosuresAndRollsBackOutbox()
    {
        await Seed();
        var input = Input(); input.Status = ReservationStatus.EnAttente;
        var created = await Dal.CreateReservationAsync(input);
        await using var c = new NpgsqlConnection(fixture.ConnectionString);
        await c.ExecuteAsync("INSERT INTO \"ClosedDayException\" (\"RestaurantId\",\"ExceptionDateBegin\",\"ExceptionDateEnd\") VALUES (1,'2036-07-21','2036-07-21')");
        var closures = (await c.QueryAsync<ClosedDayExceptionOut>("SELECT * FROM \"ClosedDayException\"")).ToArray();
        closures.Should().ContainSingle();
        closures[0].ExceptionDateBegin.Should().Be(new DateTime(2036, 7, 21));
        created.ReservationDate.Should().Be(input.ReservationDate);
        Func<Task> validate = () => Dal.UpdateReservationStatus(created.Id, ReservationStatus.Validee, ReservationStatus.EnAttente);
        await validate.Should().ThrowAsync<ReservationRuleException>();
        (await c.QuerySingleAsync<int>("SELECT count(*) FROM \"ReservationNotificationOutbox\"")).Should().Be(4);
        (await Dal.GetMyReservationById(created.Id))!.Status.Should().Be(ReservationStatus.EnAttente);
    }
}
