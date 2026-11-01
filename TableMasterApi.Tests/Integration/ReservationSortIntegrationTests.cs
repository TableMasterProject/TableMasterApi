using Dapper;
using Npgsql;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using Xunit;

namespace TableMasterApi.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class ReservationSortIntegrationTests(PostgresFixture fixture)
{
    [SkippableFact]
    public async Task GetReservations_SortsBeforePaginationInBothDirections()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.UnavailableReason ?? "Docker indisponible");
        await fixture.ResetAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.ExecuteAsync("""
            INSERT INTO "User" ("Email","Password","FirstName","LastName") VALUES ('sort-owner@example.test','x','O','W');
            INSERT INTO "Restaurant" ("UserId","RestaurantName") VALUES (1,'Sort tests');
            INSERT INTO "TableEntity" ("RestaurantId","TableNumber","NumberOfSeats") VALUES (1,1,4);
            INSERT INTO "Reservation" ("TableId","RestaurantId","ReservationDate","NumberOfPeople","Status") VALUES
              (1,1,'2036-07-20 12:00',2,1),
              (1,1,'2036-07-21 12:00',2,1),
              (1,1,'2036-07-21 12:00',2,1);
            """);
        var dal = new ReservationDAL(new PostgresConnectionFactory(fixture.Config), new ReservationRules(TimeProvider.System));

        var ascending = (await dal.GetReservations(new SearchReservations { restaurantId = 1, PageSize = 2 })).ToList();
        var descending = (await dal.GetReservations(new SearchReservations { restaurantId = 1, PageSize = 2, SortDescending = true })).ToList();
        var next = (await dal.GetReservations(new SearchReservations { restaurantId = 1, PageSize = 2, Offset = 2, SortDescending = true })).ToList();

        ascending.Select(r => r.Id).Should().BeInAscendingOrder();
        descending.Select(r => r.Id).Should().BeInDescendingOrder();
        next.Should().ContainSingle();
        next[0].Id.Should().Be(ascending[0].Id);
    }
}
