using FluentAssertions;
using TableMasterApi.Model;
using Xunit;

namespace TableMasterApi.Tests.Model;

public class SearchReservationsSortTests
{
    [Fact]
    public void SortDescending_IsOptionalAndDefaultsToAscending()
    {
        var property = typeof(SearchReservations).GetProperty("SortDescending");

        property.Should().NotBeNull();
        property!.GetValue(new SearchReservations()).Should().Be(false);
    }
}
