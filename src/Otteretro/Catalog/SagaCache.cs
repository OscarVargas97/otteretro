using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Otteretro.Data;

namespace Otteretro.Catalog;

/// <summary>
/// Sagas se arma una vez y queda guardado en memoria y en `cache/sagas.json` (#225): al volver a abrir la app se lee ese archivo y no se
/// consulta el catálogo, aunque el escaneo esté usando el disco. Lo guardado lleva la generación de `cache_generation`, que suben los triggers
/// cuando cambia algo tuyo y <see cref="CatalogUpdate.Apply"/> al cambiar el catálogo.
/// </summary>
public static class SagaCache
{
    /// <param name="PopularityOn">Popularidad de cada saga contando solo sus juegos de una consola (#166).</param>
    public sealed record Data(long Generation, List<SagaCard> Cards, Dictionary<long, Dictionary<string, long>> PopularityOn, Dictionary<string, string?> Companies);

    static readonly Lock gate = new();
    static (string Path, Data Data)? memory;
    static Task? rebuilding;

    static string FileOf(Database db) => Path.Combine(Path.GetDirectoryName(db.FilePath)!, "cache", "sagas.json");

    static long Generation(Database db)
    {
        using var c = db.Open();
        return c.ExecuteScalar<long>("SELECT value FROM cache_generation");
    }

    /// <summary>
    /// Lo guardado. Si quedó viejo (el escaneo encontró algo, por ejemplo), se devuelve igual y se rehace aparte: la pantalla no espera, y
    /// lo nuevo llega en el próximo pedido. Solo la primera vez, sin nada guardado, se arma en el momento.
    /// </summary>
    public static Data Get(Database db)
    {
        var generation = Generation(db);
        Data? data;
        lock (gate) data = memory is { } m && m.Path == db.FilePath ? m.Data : null;
        if (data is null && Read(db) is { } saved)
        {
            data = saved;
            lock (gate) memory = (db.FilePath, saved);
        }
        if (data is null) return Refresh(db);
        if (data.Generation != generation) RefreshLater(db);
        return data;
    }

    /// <summary>Lo rehace ya: después de algo que hiciste (aceptar una versión, una favorita, ocultar una consola), para verlo al instante.</summary>
    public static Data Refresh(Database db)
    {
        var data = CatalogQueries.BuildSagas(db, Generation(db)); // la generación antes de armar: un cambio a mitad deja lo guardado viejo, no al revés
        lock (gate) memory = (db.FilePath, data);
        Write(db, data);
        return data;
    }

    static void RefreshLater(Database db)
    {
        lock (gate)
        {
            if (rebuilding is { IsCompleted: false }) return;
            // Si falla, sigue lo guardado y el próximo pedido lo intenta otra vez.
            rebuilding = Task.Run(() => { try { Refresh(db); } catch (Exception e) when (e is SqliteException or IOException) { } });
        }
    }

    static Data? Read(Database db)
    {
        try { return JsonSerializer.Deserialize<Data>(File.ReadAllBytes(FileOf(db)), JsonSerializerOptions.Web); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>A un temporal y después se renombra: un corte no deja un archivo a medias.</summary>
    static void Write(Database db, Data data)
    {
        var file = FileOf(db);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file + ".part", JsonSerializer.SerializeToUtf8Bytes(data, JsonSerializerOptions.Web));
            File.Move(file + ".part", file, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // sin archivo, la próxima apertura arma de nuevo
    }
}
