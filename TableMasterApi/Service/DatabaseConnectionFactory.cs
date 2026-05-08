using System.Data;
using Npgsql;
using TableMasterApi.Model;

namespace TableMasterApi.Service
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }

    public class PostgresConnectionFactory : IDbConnectionFactory
    {
        private readonly ConfigPerso _config;

        public PostgresConnectionFactory(ConfigPerso config)
        {
            _config = config;
        }

        public IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_config.ConnectionString);
        }
    }
}
