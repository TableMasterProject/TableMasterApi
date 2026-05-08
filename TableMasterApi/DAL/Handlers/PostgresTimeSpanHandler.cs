using System.Data;
using Dapper;

namespace TableMasterApi.DAL.Handlers
{
    public sealed class PostgresTimeSpanHandler : SqlMapper.TypeHandler<TimeSpan>
    {
        public override TimeSpan Parse(object value)
        {
            return value switch
            {
                TimeSpan timeSpan => timeSpan,
                TimeOnly timeOnly => timeOnly.ToTimeSpan(),
                string text => TimeSpan.Parse(text),
                _ => throw new DataException($"Impossible de convertir {value.GetType().Name} en TimeSpan.")
            };
        }

        public override void SetValue(IDbDataParameter parameter, TimeSpan value)
        {
            parameter.Value = value;
        }
    }
}
