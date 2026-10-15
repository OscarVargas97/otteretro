using Dapper;
using Microsoft.Data.Sqlite;

namespace Otteretro.Datos;

/// <summary>La base SQLite en modo WAL: la app, el lanzador y la CLI pueden usarla a la vez.</summary>
public sealed class Bd(string ruta)
{
    public string Ruta { get; } = ruta;

    public SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Ruta,
            ForeignKeys = true,
            DefaultTimeout = 5,
        }.ToString());
        conexion.Open();
        conexion.Execute("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
        return conexion;
    }
}
