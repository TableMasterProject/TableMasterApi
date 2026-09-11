using Dapper;
using Npgsql;

namespace TableMasterApi.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class ReservationOutboxIntegrationTests(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly MovingClock clock = new();
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private ReservationOutbox Outbox => new(new PostgresConnectionFactory(fixture.Config), clock);
    private sealed class MovingClock : TimeProvider
    {
        public DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private async Task Seed()
    {
        fixture.IsAvailable.Should().BeTrue(fixture.UnavailableReason);
        await using var c = new NpgsqlConnection(fixture.ConnectionString);
        await c.ExecuteAsync("""
            INSERT INTO "User" ("Email","Password","FirstName","LastName") VALUES ('outbox@example.test','x','O','B');
            INSERT INTO "ReservationNotificationOutbox" ("Id","EventId","Channel","UserId","Payload","DueAt")
            VALUES (@Id,@EventId,'push',1,'{}','2026-01-01');
            """, new { Id = Guid.NewGuid(), EventId = Guid.NewGuid() });
    }

    [Fact]
    public async Task LeaseIsExclusiveAndRecoveredAfterRestartWithFencing()
    {
        await Seed();
        var leases = await Task.WhenAll(Outbox.LeaseAsync(), Outbox.LeaseAsync());
        leases.Count(x => x != null).Should().Be(1);
        var original = leases.Single(x => x != null)!;
        clock.Now = clock.Now.AddMinutes(3);
        var recovered = (await Outbox.LeaseAsync())!;
        recovered.Id.Should().Be(original.Id);
        recovered.LeaseId.Should().NotBe(original.LeaseId);
        await Outbox.CompleteAsync(original);
        await Outbox.FailAsync(recovered, false, "temporary");
        clock.Now = clock.Now.AddMinutes(1);
        (await Outbox.LeaseAsync())!.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task SixFailuresRespectBackoffAndBecomeDeadLetter()
    {
        await Seed();
        var delays = new[] { 1, 5, 15, 30, 60 };
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var task = (await Outbox.LeaseAsync())!;
            task.Should().NotBeNull();
            await Outbox.FailAsync(task, false, "temporary");
            (await Outbox.LeaseAsync()).Should().BeNull();
            if (attempt < 5) clock.Now = clock.Now.AddMinutes(delays[attempt]);
        }
        await using var c = new NpgsqlConnection(fixture.ConnectionString);
        (await c.QuerySingleAsync<string>("SELECT \"State\" FROM \"ReservationNotificationOutbox\"")).Should().Be("dead");
    }

    [Fact]
    public async Task FailedExternalSendPreservesSuccessfulRecipientCheckpoint()
    {
        await Seed();
        var sender = new Mock<IReservationNotificationSender>();
        sender.Setup(x => x.SendAsync(It.IsAny<OutboxTask>(), It.IsAny<Func<IEnumerable<string>, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<OutboxTask, Func<IEnumerable<string>, CancellationToken, Task>, CancellationToken>(async (_, checkpoint, ct) =>
            { await checkpoint(["recipient-hash"], ct); throw new NotificationDeliveryException("temporary"); });
        await ReservationNotificationWorker.ProcessOneAsync(Outbox, sender.Object, default);
        clock.Now = clock.Now.AddMinutes(1);
        var retry = (await Outbox.LeaseAsync())!;
        retry.CompletedRecipients.Should().Contain("recipient-hash");
        retry.Attempts.Should().Be(1);
    }
}
