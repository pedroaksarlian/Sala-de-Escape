using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace TP06.Models;

public class BD
{
    private readonly string _connectionString;

    public BD(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("EscapeRoom")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=EscapeRoom;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    public int? ObtenerSalaActual(string nombreParticipante)
    {
        using var connection = new SqlConnection(_connectionString);
        return connection.QuerySingleOrDefault<int?>(
            "SELECT TOP 1 IdSala FROM Partida WHERE NombreParticipante = @NombreParticipante ORDER BY Id DESC",
            new { NombreParticipante = nombreParticipante });
    }

    public bool ValidarAccesoSala(string nombreParticipante, int idSala)
    {
        using var connection = new SqlConnection(_connectionString);
        var sala = connection.QuerySingleOrDefault<int?>(
            "SELECT TOP 1 IdSala FROM Partida WHERE NombreParticipante = @NombreParticipante AND IdSala = @IdSala ORDER BY Id DESC",
            new { NombreParticipante = nombreParticipante, IdSala = idSala });

        return sala.HasValue;
    }

    public bool RegistrarSalaActual(string nombreParticipante, int idSala)
    {
        using var connection = new SqlConnection(_connectionString);

        var exists = connection.ExecuteScalar<int>(
            "SELECT COUNT(1) FROM Partida WHERE NombreParticipante = @NombreParticipante AND IdSala = @IdSala",
            new { NombreParticipante = nombreParticipante, IdSala = idSala });

        if (exists > 0)
        {
            return true;
        }

        var affected = connection.Execute(
            "INSERT INTO Partida (NombreParticipante, FechaInicio, IdSala) VALUES (@NombreParticipante, @FechaInicio, @IdSala)",
            new
            {
                NombreParticipante = nombreParticipante,
                FechaInicio = DateTime.Today,
                IdSala = idSala
            });

        return affected > 0;
    }
}