using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Otteretro.Api;

namespace Otteretro.Inicio;

public record EstadoMudanza(string Paso, long Hecho, long Total, bool EnCurso, string? Destino, string? Error, string? Aviso);

/// <summary>Ejecuta la mudanza en segundo plano y la publica como evento "mudanza" (panel de tareas y Ajustes).</summary>
public sealed class ServicioMudanza(ContextoApp contexto, Eventos eventos)
{
    public EstadoMudanza Estado { get; private set; } = new("", 0, 0, false, null, null, null);
    readonly Lock cerrojo = new();

    /// <summary>Devuelve un error si no puede empezar; si empieza, el resultado llega por eventos.</summary>
    public string? Iniciar(string destino)
    {
        string origen;
        // Comprobar y marcar "en curso" de una vez: dos mudanzas a la vez pueden borrar origen y destino.
        lock (cerrojo)
        {
            if (Estado.EnCurso) return "Ya hay una mudanza en curso.";
            if (contexto.Config.CarpetaDatos is not { } actual) return "No hay carpeta de datos.";
            origen = actual;
            var error = Mudanza.Validar(origen, destino);
            if (error is not null) return error;
            destino = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destino));
            Cambiar(new("Preparando", 0, 0, true, destino, null, null));
        }
        // ponytail: en F0 nada más escribe en la carpeta de datos; cuando haya tareas que la usen, pausarlas antes de mudarse.
        _ = Task.Run(() =>
        {
            RegistroArchivo.Directorio = Path.Combine(Path.GetDirectoryName(Configuracion.RutaPorDefecto)!, "logs");
            try
            {
                Mudanza.CopiarYVerificar(origen, destino, p => Cambiar(Estado with { Paso = p.Paso, Hecho = p.Hecho, Total = p.Total }));
                try
                {
                    contexto.Config.CarpetaDatos = destino;
                    contexto.Config.Guardar(contexto.RutaConfig);
                }
                catch
                {
                    // config.json sigue apuntando al origen (se escribe de forma atómica): se deshace la copia o el movimiento.
                    contexto.Config.CarpetaDatos = origen;
                    Mudanza.Deshacer(origen, destino);
                    throw;
                }
                Cambiar(Estado with { Paso = "Borrando la carpeta anterior" });
                var aviso = Mudanza.BorrarOrigen(origen);
                RegistroArchivo.Directorio = CarpetaDatos.RutaLogs(destino);
                RegistroArchivo.Escribir(LogLevel.Information, "Mudanza", $"Carpeta de datos movida de {origen} a {destino}.");
                Cambiar(Estado with { Paso = "Listo", EnCurso = false, Aviso = aviso });
            }
            catch (Exception e)
            {
                // Si algo falló al deshacer, se dice dónde quedaron de verdad los datos.
                var donde = CarpetaDatos.EstaLista(origen) ? origen : destino;
                RegistroArchivo.Directorio = CarpetaDatos.RutaLogs(donde);
                RegistroArchivo.Escribir(LogLevel.Error, "Mudanza", e.ToString());
                var texto = donde == origen
                    ? $"No se movió nada; tus datos siguen en {origen}."
                    : $"La mudanza se cortó y tus datos quedaron en {destino}. Usa «Usar otra carpeta» con esa ruta.";
                Cambiar(Estado with { Paso = "Error", EnCurso = false, Error = $"{texto} {e.Message}" });
            }
        });
        return null;
    }

    void Cambiar(EstadoMudanza estado)
    {
        Estado = estado;
        eventos.Publicar("mudanza", estado);
    }
}

/// <summary>
/// Mueve la carpeta de datos a otra ubicación sin perder nada (P7):
/// copiar → verificar cada archivo por hash → (quien llama cambia la configuración) → borrar el origen.
/// En el mismo disco se mueve sin copiar.
/// </summary>
public static class Mudanza
{
    public record Progreso(string Paso, long Hecho, long Total);

    static readonly StringComparison SinMayusculas = StringComparison.OrdinalIgnoreCase;

    /// <summary>Devuelve un mensaje de error o null si se puede mover de <paramref name="origen"/> a <paramref name="destino"/>.</summary>
    public static string? Validar(string origen, string? destino)
    {
        if (string.IsNullOrWhiteSpace(destino) || !Path.IsPathFullyQualified(destino))
            return "La ruta tiene que ser completa, por ejemplo E:\\Otteretro.";
        var o = Normalizar(origen);
        var d = Normalizar(destino);
        if (string.Equals(o, d, SinMayusculas))
            return "Es la misma carpeta que la actual.";
        if (d.StartsWith(o + Path.DirectorySeparatorChar, SinMayusculas))
            return "La nueva carpeta no puede estar dentro de la actual.";
        if (File.Exists(d))
            return "Esa ruta es un archivo, no una carpeta.";
        if (Directory.Exists(d) && Directory.EnumerateFileSystemEntries(d).Any())
            return "La carpeta de destino tiene que estar vacía.";
        if (!MismoDisco(o, d))
        {
            var raiz = Path.GetPathRoot(d)!;
            if (!Directory.Exists(raiz)) return $"No se encuentra el disco {raiz}.";
            try
            {
                var libre = new DriveInfo(raiz).AvailableFreeSpace;
                var necesario = Tamano(o);
                if (libre < necesario)
                    return $"No hay espacio en {raiz}: hacen falta {Gb(necesario)} y hay {Gb(libre)} libres.";
            }
            catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // Rutas de red (\\nas\carpeta) o discos sin acceso.
                return $"No se puede usar {raiz}: {e.Message}";
            }
        }
        return null;
    }

    /// <summary>Copia (o mueve, en el mismo disco) y verifica. El origen sigue intacto salvo en el movimiento del mismo disco.</summary>
    /// <param name="copiar">Forzar la copia aunque sea el mismo disco (las pruebas lo usan para probar la copia).</param>
    public static void CopiarYVerificar(string origen, string destino, Action<Progreso> avisar, bool copiar = false)
    {
        CerrarBaseDeDatos(origen);

        if (!copiar && MismoDisco(origen, destino))
        {
            avisar(new("Moviendo", 0, 1));
            if (Directory.Exists(destino)) Directory.Delete(destino); // está vacía (lo garantiza Validar)
            Directory.CreateDirectory(Path.GetDirectoryName(Normalizar(destino))!);
            Directory.Move(origen, destino);
            avisar(new("Moviendo", 1, 1));
            return;
        }

        var archivos = Directory.EnumerateFiles(origen, "*", SearchOption.AllDirectories)
            .Select(f => (relativa: Path.GetRelativePath(origen, f), tamano: new FileInfo(f).Length))
            .ToList();
        var total = archivos.Sum(a => a.tamano);
        try
        {
            foreach (var carpeta in Directory.EnumerateDirectories(origen, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(destino, Path.GetRelativePath(origen, carpeta)));

            long hecho = 0;
            foreach (var (relativa, tamano) in archivos)
            {
                var hacia = Path.Combine(destino, relativa);
                Directory.CreateDirectory(Path.GetDirectoryName(hacia)!);
                File.Copy(Path.Combine(origen, relativa), hacia);
                AlDisco(hacia);
                avisar(new("Copiando", hecho += tamano, total));
            }

            hecho = 0;
            foreach (var (relativa, tamano) in archivos)
            {
                if (!Iguales(Path.Combine(origen, relativa), Path.Combine(destino, relativa)))
                    throw new IOException($"El archivo {relativa} no se copió bien.");
                avisar(new("Verificando", hecho += tamano, total));
            }
        }
        catch
        {
            try { Deshacer(origen, destino); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>
    /// Deja todo como antes de <see cref="CopiarYVerificar"/>: si se movió (mismo disco) la devuelve a su sitio;
    /// si se copió, borra del destino solo lo copiado y las carpetas que quedan vacías, nunca otra cosa.
    /// </summary>
    public static void Deshacer(string origen, string destino)
    {
        if (!Directory.Exists(destino)) return;
        if (!Directory.Exists(origen))
        {
            Directory.Move(destino, origen);
            return;
        }
        foreach (var f in Directory.EnumerateFiles(origen, "*", SearchOption.AllDirectories))
        {
            var copia = Path.Combine(destino, Path.GetRelativePath(origen, f));
            if (!File.Exists(copia)) continue;
            File.SetAttributes(copia, FileAttributes.Normal);
            File.Delete(copia);
        }
        var carpetas = Directory.EnumerateDirectories(origen, "*", SearchOption.AllDirectories)
            .Select(c => Path.Combine(destino, Path.GetRelativePath(origen, c)))
            .Append(destino)
            .OrderByDescending(c => c.Length);
        foreach (var c in carpetas)
            if (Directory.Exists(c) && !Directory.EnumerateFileSystemEntries(c).Any())
                Directory.Delete(c);
    }

    /// <summary>Obliga a Windows a escribir el archivo en el disco: sin esto, la verificación puede leer la caché y se borraría el origen antes de tiempo.</summary>
    static void AlDisco(string ruta)
    {
        var atributos = File.GetAttributes(ruta);
        if (atributos.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(ruta, atributos & ~FileAttributes.ReadOnly);
        using (var f = new FileStream(ruta, FileMode.Open, FileAccess.Write))
            f.Flush(flushToDisk: true);
        File.SetAttributes(ruta, atributos);
    }

    /// <summary>Borra la carpeta anterior. Si algo queda bloqueado, devuelve un aviso en vez de fallar: los datos ya están en el destino.</summary>
    public static string? BorrarOrigen(string origen)
    {
        if (!Directory.Exists(origen)) return null;
        try
        {
            Directory.Delete(origen, true);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"Los datos ya están en la carpeta nueva, pero no se pudo borrar del todo {origen}: {e.Message} Puedes borrarla a mano.";
        }
    }

    public static long Tamano(string carpeta) =>
        Directory.Exists(carpeta)
            ? Directory.EnumerateFiles(carpeta, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
            : 0;

    /// <summary>Vuelca el WAL a la base y suelta las conexiones, para copiar o mover el archivo sin que esté en uso.</summary>
    static void CerrarBaseDeDatos(string carpeta)
    {
        var ruta = CarpetaDatos.RutaBd(carpeta);
        if (File.Exists(ruta))
        {
            using var c = new SqliteConnection($"Data Source={ruta}");
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
    }

    static bool Iguales(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        using var fa = File.OpenRead(a);
        using var fb = File.OpenRead(b);
        return SHA256.HashData(fa).AsSpan().SequenceEqual(SHA256.HashData(fb));
    }

    static bool MismoDisco(string a, string b) => string.Equals(Path.GetPathRoot(a), Path.GetPathRoot(b), SinMayusculas);
    static string Normalizar(string ruta) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(ruta));
    static string Gb(long bytes) => $"{bytes / 1024d / 1024 / 1024:0.#} GB";
}
