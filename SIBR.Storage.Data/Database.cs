using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using Npgsql;
using NpgsqlTypes;
using Serilog;

namespace SIBR.Storage.Data
{
    public class Database
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;

        public static void Init()
        {
            NpgsqlConnection.GlobalTypeMapper.UseJsonNet().UseNodaTime();
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            SqlMapper.AddTypeHandler(new PassthroughTypeHandler<Instant>(NpgsqlDbType.TimestampTz));
            SqlMapper.AddTypeHandler(new JsonTypeHandler<JToken>());
            SqlMapper.AddTypeHandler(new JsonTypeHandler<JValue>());
            SqlMapper.AddTypeHandler(new JsonTypeHandler<JArray>());
            SqlMapper.AddTypeHandler(new JsonTypeHandler<JObject>());
        }

        public Database(IServiceProvider services, string connectionString)
        {
            _logger = services.GetRequiredService<ILogger>();
            _connectionString = new NpgsqlConnectionStringBuilder(connectionString)
                {
                    Enlist = false,
                    NoResetOnClose = true,
                    WriteBufferSize = 1024*64
                }
                .ConnectionString;
        }

        public async Task<NpgsqlConnection> Obtain()
        {
            var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async Task RunMigrations()
        {
            await using var connection = await Obtain();
            var evolve = new Evolve.Evolve(connection, msg => _logger.Information("Evolve: {Message}", msg))
            {
                EmbeddedResourceAssemblies = new[] {typeof(Database).Assembly},
                IsEraseDisabled = true,
                SqlMigrationPrefix = "v",
                SqlRepeatableMigrationPrefix = "r"
            };

            // Evolve isn't async >.>
            await Task.Run(() => evolve.Migrate());
        }

        private class JsonTypeHandler<T> : SqlMapper.TypeHandler<T> where T: JToken
        {
            public override void SetValue(IDbDataParameter parameter, T value)
            {
                var p = (NpgsqlParameter) parameter;
                
                p.Value = value;
                p.NpgsqlDbType = NpgsqlDbType.Jsonb;
            }

            public override T Parse(object value)
            {
                return JsonConvert.DeserializeObject<T>((string) value);
            }
        }
        
        private class PassthroughTypeHandler<T>: SqlMapper.TypeHandler<T>
        {
            private readonly NpgsqlDbType _type;

            public PassthroughTypeHandler(NpgsqlDbType type)
            {
                _type = type;
            }

            public override void SetValue(IDbDataParameter parameter, T value)
            {
                parameter.Value = value;
                ((NpgsqlParameter) parameter).NpgsqlDbType = _type;
            }

            public override T Parse(object value) => (T) value;
        }
    }
}