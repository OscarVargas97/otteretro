using CG.Web.MegaApiClient;
using Otteretro.Api;
using Otteretro.App;

namespace Otteretro.Libraries;

/// <summary>
/// Mega (#205) con MegaApiClient, que descifra el archivo mientras baja: la clave viene en el enlace. Sin cuenta usa la cuota gratis de Mega; con
/// tu cuenta, la tuya. Esta librería no retoma: un corte vuelve a empezar ese archivo, nunca los anteriores.
/// </summary>
public sealed class MegaSite : IDownloadSite, IOwnTransfer
{
    public string Id => "mega";
    public bool Handles(Uri url) => url.Host is "mega.nz" or "www.mega.nz" or "mega.co.nz" or "www.mega.co.nz";

    /// <summary>Los enlaces de Mega no se resuelven a uno directo: los baja <see cref="Transfer"/>.</summary>
    public Task<ResolvedLink> Resolve(Uri url, Credentials credentials, CancellationToken ct) => Task.FromResult(new ResolvedLink(url.ToString(), Probe: false));

    static async Task<MegaApiClient> Login(Credentials credentials)
    {
        var client = new MegaApiClient();
        var (email, password) = (credentials.Read("mega.email"), credentials.Read("mega.password"));
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) await client.LoginAnonymousAsync();
        else await client.LoginAsync(email, password);
        return client;
    }

    static bool IsFolder(Uri url) => url.Fragment.StartsWith("#F!", StringComparison.Ordinal) || url.AbsolutePath.StartsWith("/folder/", StringComparison.Ordinal);

    public async Task<(string? Name, long? Size)> Describe(Uri url, Credentials credentials, CancellationToken ct)
    {
        var client = await Login(credentials);
        try
        {
            if (IsFolder(url))
            {
                var files = (await client.GetNodesFromLinkAsync(url)).Where(n => n.Type == NodeType.File).ToList();
                return (null, files.Sum(n => n.Size));
            }
            var node = await client.GetNodeFromLinkAsync(url);
            return (node.Name, node.Size);
        }
        finally { await client.LogoutAsync(); }
    }

    public async Task Transfer(Uri url, string folder, Credentials credentials, Action<double, string> progress, CancellationToken ct)
    {
        var client = await Login(credentials);
        try
        {
            if (IsFolder(url))
            {
                var files = (await client.GetNodesFromLinkAsync(url)).Where(n => n.Type == NodeType.File).ToList();
                for (var i = 0; i < files.Count; i++)
                {
                    var (node, index) = (files[i], i);
                    await Get(Path.Combine(folder, Safe(node.Name)), temp => client.DownloadFileAsync(node, temp, new Progress<double>(p => progress((index + p / 100) * 100 / files.Count, node.Name)), ct));
                }
                return;
            }
            var file = await client.GetNodeFromLinkAsync(url);
            await Get(Path.Combine(folder, Safe(file.Name)), temp => client.DownloadFileAsync(file, temp, new Progress<double>(p => progress(p, file.Name)), ct));
        }
        catch (ApiException e) when (e.ApiResultCode == ApiResultCode.ResourceNotExists)
        {
            throw new TaskException(ApiError.Of("download.unavailable", ("file", url.ToString()), ("status", "404")));
        }
        finally { await client.LogoutAsync(); }
    }

    /// <summary>Baja a un temporal, que queda con su nombre solo cuando la librería comprobó el archivo.</summary>
    static async Task Get(string path, Func<string, Task> download)
    {
        if (File.Exists(path)) return;
        var temp = path + ".parte";
        File.Delete(temp);
        await download(temp);
        File.Move(temp, path);
    }

    /// <summary>Un nombre que llega de afuera no puede salirse de la carpeta.</summary>
    static string Safe(string name)
    {
        name = Path.GetFileName(name.Replace('\\', '/'));
        foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        return name.Length > 0 ? name : "descarga";
    }
}
