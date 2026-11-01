using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TableMasterApi.DAL;
using TableMasterApi.Model;
using TableMasterApi.Service;
using Xunit;

namespace TableMasterApi.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class RestaurantViewportIntegrationTests(PostgresFixture fixture)
{
    [SkippableFact]
    public async Task Search_IncludesBothSidesOfDateLine()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.UnavailableReason ?? "Docker indisponible");
        await fixture.ResetAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var userId = await connection.QuerySingleAsync<long>(
            """INSERT INTO "User" ("Email", "Password", "FirstName", "LastName") VALUES ('date-line@example.com', 'x', 'Map', 'Test') RETURNING "Id";""");
        await connection.ExecuteAsync(
            """INSERT INTO "Restaurant" ("UserId", "RestaurantName", "Latitude", "Longitude") VALUES (@UserId, 'East', -17, 178), (@UserId, 'West', -17, -175), (@UserId, 'Outside', -17, 160)""",
            new { UserId = userId });
        var dal = new RestaurantDAL(
            fixture.Config,
            new GoogleMapsService(fixture.Config, new HttpClient(), NullLogger<GoogleMapsService>.Instance));

        var results = (await dal.GetRestaurants(new SearchRestaurant
        {
            Latitude = -17,
            Longitude = 179,
            SouthLatitude = -20,
            NorthLatitude = -10,
            WestLongitude = 170,
            EastLongitude = -170
        })).ToList();

        results.Select(r => r.RestaurantName).Should().BeEquivalentTo(["East", "West"]);
    }

    [SkippableFact]
    public async Task Search_UsesOptionalViewportWithoutBreakingExistingRequests()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.UnavailableReason ?? "Docker indisponible");
        await fixture.ResetAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var userId = await connection.QuerySingleAsync<long>(
            """INSERT INTO "User" ("Email", "Password", "FirstName", "LastName") VALUES ('map@example.com', 'x', 'Map', 'Test') RETURNING "Id";"""
        );
        await connection.ExecuteAsync(
            """INSERT INTO "Restaurant" ("UserId", "RestaurantName", "Latitude", "Longitude") VALUES (@UserId, 'Paris', 48.8566, 2.3522), (@UserId, 'Marseille', 43.2965, 5.3698)""",
            new { UserId = userId });
        var dal = new RestaurantDAL(
            fixture.Config,
            new GoogleMapsService(fixture.Config, new HttpClient(), NullLogger<GoogleMapsService>.Instance));

        var existing = (await dal.GetRestaurants(new SearchRestaurant
        {
            Latitude = 46.6m,
            Longitude = 2.2m
        })).ToList();
        var bounded = (await dal.GetRestaurants(new SearchRestaurant
        {
            Latitude = 48.8m,
            Longitude = 2.3m,
            SouthLatitude = 48m,
            NorthLatitude = 49m,
            WestLongitude = 2m,
            EastLongitude = 3m
        })).ToList();

        existing.Should().HaveCount(2);
        bounded.Should().ContainSingle().Which.RestaurantName.Should().Be("Paris");
    }
}
