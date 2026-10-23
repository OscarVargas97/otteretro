using Dapper;
using Microsoft.Data.Sqlite;

namespace Otteretro.Data;

/// <summary>La base SQLite en modo WAL: la app, el lanzador y la CLI pueden usarla a la vez.</summary>
public sealed class Database(string filePath)
{
    public string FilePath { get; } = filePath;

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            ForeignKeys = true,
            DefaultTimeout = 5,
        }.ToString());
        connection.Open();
        connection.Execute("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
        return connection;
    }
}
