using System.Data;
using System.Text.Json;
using Dapper;

namespace TableMasterApi.Service;

public sealed record ReservationNotification(Guid EventId, long ReservationId, long? RestaurantId, DateTime ReservationDate, string Type, string Status);
public sealed class OutboxTask
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Channel { get; set; } = "";
    public long UserId { get; set; }
    public string Payload { get; set; } = "";
    public int Attempts { get; set; }
    public Guid LeaseId { get; set; }
    public string CompletedRecipients { get; set; } = "[]";
}

public sealed class ReservationOutbox(IDbConnectionFactory connections, TimeProvider clock)
{
    private static readonly int[] RetryMinutes = [1, 5, 15, 30, 60];

    public static async Task EnqueueAsync(IDbConnection connection, IDbTransaction transaction, Model.ReservationOut reservation, string type)
    {
        var owner = await connection.QuerySingleOrDefaultAsync<long?>("SELECT \"UserId\" FROM \"Restaurant\" WHERE \"Id\"=@Id", new { Id = reservation.RestaurantId }, transaction);
        var eventId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new ReservationNotification(eventId, reservation.Id, reservation.RestaurantId, reservation.ReservationDate, type, reservation.Status.ToString()));
        foreach (var userId in new[] { owner, reservation.UserId }.Where(x => x.HasValue).Distinct())
            foreach (var channel in new[] { "push", "email" })
                await connection.ExecuteAsync("""
                    INSERT INTO "ReservationNotificationOutbox" ("Id","EventId","Channel","UserId","Payload")
                    VALUES (@Id,@EventId,@Channel,@UserId,CAST(@Payload AS jsonb));
                    """, new { Id = Guid.NewGuid(), EventId = eventId, Channel = channel, UserId = userId, Payload = payload }, transaction);
    }

    public async Task<OutboxTask?> LeaseAsync(CancellationToken ct = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OutboxTask>(new CommandDefinition("""
            WITH candidate AS (
                SELECT "Id" FROM "ReservationNotificationOutbox"
                WHERE ("State"='pending' AND "DueAt"<=@Now) OR ("State"='leased' AND "LeaseUntil"<=@Now)
                ORDER BY "DueAt", "Id" FOR UPDATE SKIP LOCKED LIMIT 1
            )
            UPDATE "ReservationNotificationOutbox" o SET "State"='leased',"LeaseId"=@LeaseId,"LeaseUntil"=@Until
            FROM candidate c WHERE o."Id"=c."Id"
            RETURNING o."Id",o."EventId",o."Channel",o."UserId",o."Payload"::text AS "Payload",o."Attempts",o."LeaseId",o."CompletedRecipients"::text AS "CompletedRecipients";
            """, new { Now = clock.GetUtcNow(), Until = clock.GetUtcNow().AddMinutes(2), LeaseId = Guid.NewGuid() }, cancellationToken: ct));
    }

    public async Task SaveRecipientsAsync(OutboxTask task, IEnumerable<string> hashes, CancellationToken ct)
    {
        using var connection = connections.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE "ReservationNotificationOutbox" SET "CompletedRecipients"=CAST(@Hashes AS jsonb)
            WHERE "Id"=@Id AND "LeaseId"=@LeaseId AND "State"='leased';
            """, new { task.Id, task.LeaseId, Hashes = JsonSerializer.Serialize(hashes) }, cancellationToken: ct));
        if (updated != 1) throw new InvalidOperationException("Bail de notification expiré.");
    }

    public async Task CompleteAsync(OutboxTask task, CancellationToken ct = default)
    {
        using var connection = connections.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE "ReservationNotificationOutbox" SET "State"='completed',"LeaseId"=NULL,"LeaseUntil"=NULL,"LastError"=NULL
            WHERE "Id"=@Id AND "LeaseId"=@LeaseId AND "State"='leased';
            """, task, cancellationToken: ct));
    }

    public async Task FailAsync(OutboxTask task, bool permanent, string errorCode, CancellationToken ct = default)
    {
        using var connection = connections.CreateConnection();
        var attempts = task.Attempts + 1;
        var dead = permanent || attempts >= 6;
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE "ReservationNotificationOutbox" SET "State"=@State,"Attempts"=@Attempts,"DueAt"=@Due,"LeaseId"=NULL,"LeaseUntil"=NULL,"LastError"=@Error
            WHERE "Id"=@Id AND "LeaseId"=@LeaseId AND "State"='leased';
            """, new { task.Id, task.LeaseId, State = dead ? "dead" : "pending", Attempts = attempts,
                Due = clock.GetUtcNow().AddMinutes(dead ? 0 : RetryMinutes[attempts - 1]), Error = errorCode }, cancellationToken: ct));
    }
}
