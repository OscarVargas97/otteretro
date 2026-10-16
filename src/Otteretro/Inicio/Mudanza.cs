using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Otteretro.Api;

namespace Otteretro.Inicio;

public record EstadoMudanza(string Paso, long Hecho, long Total, bool EnCurso, string? Destino, string? Error, string? Aviso);

/// <summary>Ejecuta la mudanza en segundo plano y la publica como evento "mudanza" (panel de tareas y Ajustes).</summary>
public sealed class ServicioMudanza(ContextoApp contexto, Eventos eventos)
{
    public EstadoMudanza Estado { get; private set; } = new("", 0, 0, false, null, null, null);

    /// <summary>Devuelve un error si no puede empezar; si empieza, el resultado llega por eventos.</summary>
    public string? Iniciar(string destino)
    {
        if (Estado.EnCurso) return "Ya hay una mudanza en curso.";
        var origen = contexto.Config.CarpetaDatos;
        if (origen is null) return "No hay carpeta de datos.";
        var error = Mudanza.Validar(origen, destino);
        if (error is not null) return error;

        destino = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destino));
        Cambiar(new("Preparando", 0, 0, true, destino, null, null));
        // ponytail: en F0 nada más escribe en la carpeta de datos; cuando haya tareas que la usen, pausarlas antes de mudarse.
        _ = Task.Run(() =>
        {
            var logs = RegistroArchivo.Directorio;
            RegistroArchivo.Directorio = Path.Combine(Path.GetDirectoryName(Configuracion.RutaPorDefecto)!, "logs");
            try
            {
                Mudanza.CopiarYVerificar(origen, destino, p => Cambiar(Estado with { Paso = p.Paso, Hecho = p.Hecho, Total = p.Total }));
                contexto.Config.CarpetaDatos = destino;
                contexto.Config.Guardar(contexto.RutaConfig);
                Cambiar(Estado with { Paso = "Borrando la carpeta anterior" });
                var aviso = Mudanza.BorrarOrigen(origen);
                RegistroArchivo.Directorio = CarpetaDatos.RutaLogs(destino);
                RegistroArchivo.Escribir(LogLevel.Information, "Mudanza", $"Carpeta de datos movida de {origen} a {destino}.");
                Cambiar(Estado with { Paso = "Listo", EnCurso = false, Aviso = aviso });
            }
            catch (Exception e)
            {
                RegistroArchivo.Directorio = logs;
                RegistroArchivo.Escribir(LogLevel.Error, "Mudanza", e.ToString());
                Cambiar(Estado with { Paso = "Error", EnCurso = false, Error = $"No se movió nada; tus datos siguen en {origen}. {e.Message}" });
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
            var libre = new DriveInfo(raiz).AvailableFreeSpace;
            var necesario = Tamano(o);
            if (libre < necesario)
                return $"No hay espacio en {raiz}: hacen falta {Gb(necesario)} y hay {Gb(libre)} libres.";
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
            // El destino estaba vacío: se deja como estaba y el origen no se tocó.
            try { Directory.Delete(destino, true); } catch (IOException) { }
            throw;
        }
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
