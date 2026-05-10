using System.Data;
using Npgsql;
using TableMasterApi.DAL.Handlers;

namespace TableMasterApi.Tests.Services
{
    public class PostgresTimeSpanHandlerTests
    {
        private readonly PostgresTimeSpanHandler _handler = new();

        [Fact]
        public void Parse_ShouldReturnTimeSpan_WhenValueIsTimeSpan()
        {
            var value = TimeSpan.FromHours(12);

            var result = _handler.Parse(value);

            result.Should().Be(value);
        }

        [Fact]
        public void Parse_ShouldConvertTimeOnlyToTimeSpan()
        {
            var value = new TimeOnly(12, 30, 15);

            var result = _handler.Parse(value);

            result.Should().Be(value.ToTimeSpan());
        }

        [Fact]
        public void Parse_ShouldConvertStringToTimeSpan()
        {
            var result = _handler.Parse("12:30:15");

            result.Should().Be(new TimeSpan(12, 30, 15));
        }

        [Fact]
        public void Parse_ShouldThrowDataException_WhenValueTypeIsUnsupported()
        {
            var action = () => _handler.Parse(123);

            action.Should().Throw<DataException>();
        }

        [Fact]
        public void SetValue_ShouldAssignTimeSpanToParameterValue()
        {
            var parameter = new NpgsqlParameter();
            var value = TimeSpan.FromHours(9);

            _handler.SetValue(parameter, value);

            parameter.Value.Should().Be(value);
        }
    }
}
