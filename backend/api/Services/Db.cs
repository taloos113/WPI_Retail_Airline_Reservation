using MySqlConnector;

namespace WpiReservation.Api.Services;

public class Db(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("FlightData")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:FlightData in appsettings.json");

    public MySqlConnection CreateConnection() => new(_connectionString);
}
