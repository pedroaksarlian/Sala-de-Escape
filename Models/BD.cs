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

    public List<string> ObtenerPalabrasDemichelis()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var items = connection.Query<string>(
                "SELECT Palabra FROM Demichelis ORDER BY ID ASC");

            return items.Select(p => (p ?? string.Empty).Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();
        }
        catch
        {
            return new List<string>
            {
                "Tiempo",
                "Lejos",
                "Campos de entrenamiento"
            };
        }
    }

    public List<JugadorDiCarlo> ObtenerJugadoresDiCarlo()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            var rows = connection.Query<JugadorDiCarlo>(
                "SELECT Id, Nombre, Adivinanza, Posicion FROM Jugadores ORDER BY Id ASC");

            return rows
                .Where(j => !string.IsNullOrWhiteSpace(j.Nombre))
                .ToList();
        }
        catch
        {
            return new List<JugadorDiCarlo>
            {
                new() { Id = 1, Nombre = "Franco Armani", Adivinanza = "Bajo los tres palos parece gigante, con tapadas eternas en noches de gloria.", Posicion = "Arquero" },
                new() { Id = 2, Nombre = "Gonzalo Montiel", Adivinanza = "Por el lateral derecho no pasa nadie y es el que pone el sello final.", Posicion = "Defensor" },
                new() { Id = 3, Nombre = "Daniel Passarella", Adivinanza = "Defensor con gol, voz de mando y gran capitán.", Posicion = "Defensor" },
                new() { Id = 4, Nombre = "Jonatan Maidana", Adivinanza = "Casi no habla, pero deja la vida en cada cruce.", Posicion = "Defensor" },
                new() { Id = 5, Nombre = "Marcos Acuña", Adivinanza = "Banda izquierda, garra y empuje constante.", Posicion = "Defensor" },
                new() { Id = 6, Nombre = "Enzo Pérez", Adivinanza = "Se dejó la vida en el medio y ganó la historia.", Posicion = "Mediocampista" },
                new() { Id = 7, Nombre = "Leonardo Ponzio", Adivinanza = "Comandante eterno del mediocampo, con garra y liderazgo.", Posicion = "Mediocampista" },
                new() { Id = 8, Nombre = "Enzo Fernández", Adivinanza = "Surgido de la cantera y con clase para romper el partido.", Posicion = "Mediocampista" },
                new() { Id = 9, Nombre = "Julián Álvarez", Adivinanza = "Picó sin parar y hacía goles con hambre constante.", Posicion = "Delantero" },
                new() { Id = 10, Nombre = "Enzo Francescoli", Adivinanza = "Elegancia pura con la banda en el pecho y una clase infinita.", Posicion = "Delantero" },
                new() { Id = 11, Nombre = "Radamel Falcao", Adivinanza = "Tigre del área, letal en el remate y con hambre de gol.", Posicion = "Delantero" }
            };
        }
    }

    public void GuardarCodigo(string codigo, string progreso)
    {
        // No se usa por ahora; la aplicación no tiene tabla de códigos.
    }
}