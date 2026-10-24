using System.Runtime.InteropServices;
using Dapper;

namespace Otteretro.Datos;

/// <summary>Aplica en orden los Datos/Migraciones/NNNN_nombre.sql que aún no se aplicaron (A1).</summary>
public static class Migraciones
{
    const string Marca = ".Migraciones.";

    /// <summary>Devuelve cuántas migraciones aplicó.</summary>
    public static int Aplicar(Bd bd)
    {
        using var c = bd.Abrir();
        c.Execute("CREATE TABLE IF NOT EXISTS migracion (nombre TEXT PRIMARY KEY, aplicada TEXT NOT NULL)");
        var hechas = c.Query<string>("SELECT nombre FROM migracion").ToHashSet();
        var ensamblado = typeof(Migraciones).Assembly;
        var aplicadas = 0;

        foreach (var recurso in ensamblado.GetManifestResourceNames()
                     .Where(r => r.Contains(Marca) && r.EndsWith(".sql", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var nombre = recurso[(recurso.IndexOf(Marca, StringComparison.Ordinal) + Marca.Length)..];
            if (hechas.Contains(nombre)) continue;

            using var lector = new StreamReader(ensamblado.GetManifestResourceStream(recurso)!);
            using var t = c.BeginTransaction();
            c.Execute(lector.ReadToEnd(), transaction: t);
            c.Execute("INSERT INTO migracion (nombre, aplicada) VALUES (@nombre, @fecha)",
                new { nombre, fecha = DateTime.UtcNow.ToString("O") }, t);
            t.Commit();
            aplicadas++;
        }
        return aplicadas;
    }

    /// <summary>Registra o actualiza este equipo (lo que depende de cada PC se guarda por equipo, §7.6).</summary>
    public static void RegistrarEquipo(Bd bd, string id)
    {
        using var c = bd.Abrir();
        c.Execute("""
            INSERT INTO equipo (id, nombre, so) VALUES (@id, @nombre, @so)
            ON CONFLICT(id) DO UPDATE SET nombre = excluded.nombre, so = excluded.so
            """, new { id, nombre = Environment.MachineName, so = RuntimeInformation.OSDescription });
    }
}
