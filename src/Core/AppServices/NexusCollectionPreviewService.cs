using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DivinityModManager.AppServices;

public sealed record NexusCollectionLink(string Slug, int? Revision)
{
    public static bool TryParse(string text, out NexusCollectionLink link)
    {
        link = null;
        if (text == null || text.Length > 2048 || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0
            || uri.Host.ToLowerInvariant() is not ("www.nexusmods.com" or "nexusmods.com" or "next.nexusmods.com")
            || uri.Query.Length != 0 || uri.Fragment.Length != 0) return false;
        var match = Regex.Match(uri.AbsolutePath, @"^/(?:games/)?baldursgate3/collections/([a-zA-Z0-9_-]{1,100})(?:/revisions/([1-9][0-9]*))?/?$", RegexOptions.CultureInvariant);
        if (!match.Success) return false;
        int? revision = null;
        if (match.Groups[2].Success)
        {
            if (!Int32.TryParse(match.Groups[2].Value, out var number)) return false;
            revision = number;
        }
        link = new(match.Groups[1].Value, revision);
        return true;
    }
}

public sealed record NexusCollectionFile(long ModId, long FileId, string ModName, string FileName,
    string Version, long SizeBytes, bool Optional, bool Available)
{
    public string Author { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Category { get; init; } = "";
};

public sealed record NexusCollectionPreview(string Slug, string Name, string Summary, int Revision,
    IReadOnlyList<NexusCollectionFile> Files)
{
    public string ThumbnailUrl { get; init; } = "";
    public string ManifestLink { get; init; } = "";
};

/// <summary>Reads collection metadata only. Download authorization and installation remain in the NXM pipeline.</summary>
public sealed class NexusCollectionPreviewService
{
    private const int MaximumResponseBytes = 8 * 1024 * 1024;
    private readonly HttpClient _client;
    public NexusCollectionPreviewService(HttpClient client) => _client = client ?? throw new ArgumentNullException(nameof(client));

    // Nexus GraphQL collection metadata, using variables rather than interpolated user input.
    private const string Query = """
        query ReduxCollectionPreview($slug: String!) {
          collection(slug: $slug) {
            slug name summary tileImage { url } game { domainName }
            latestPublishedRevision {
              revisionNumber downloadLink
              modFiles { fileId optional file { fileId name version sizeInBytes mod { modId name author summary category } } }
            }
          }
        }
        """;

    public async Task<NexusCollectionPreview> LoadAsync(NexusCollectionLink link, string apiKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (String.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Connect your Nexus Mods account before loading a collection.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.nexusmods.com/v2/graphql");
        request.Headers.Add("apikey", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new { query = Query, variables = new { slug = link.Slug } }), Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if ((int)response.StatusCode == 429) throw new InvalidOperationException("Nexus Mods is busy. Try loading the collection again later.");
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Nexus Mods could not load this collection. Check your connection and account.");
        if (response.Content.Headers.ContentLength > MaximumResponseBytes) throw new InvalidDataException("The collection response is too large.");
        using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(block.AsMemory(), cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaximumResponseBytes) throw new InvalidDataException("The collection response is too large.");
            await buffer.WriteAsync(block.AsMemory(0, read), cancellationToken);
        }
        return ParseResponse(Encoding.UTF8.GetString(buffer.ToArray()), link);
    }

    public static NexusCollectionPreview ParseResponse(string json, NexusCollectionLink link)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            throw new InvalidDataException("Nexus Mods could not return a complete collection preview.");
        var collection = Child(Child(root, "data"), "collection");
        if (collection.ValueKind != JsonValueKind.Object) throw new InvalidDataException("This collection is unavailable.");
        if (!Text(Child(collection, "game"), "domainName").Equals("baldursgate3", StringComparison.OrdinalIgnoreCase)
            || !Text(collection, "slug").Equals(link.Slug, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Nexus Mods returned a different collection or game.");
        var revision = Child(collection, "latestPublishedRevision");
        var revisionNumber = Number(revision, "revisionNumber");
        if (revisionNumber <= 0 || revisionNumber > Int32.MaxValue) throw new InvalidDataException("No published revision is available.");
        if (link.Revision.HasValue && link.Revision != revisionNumber)
            throw new InvalidDataException("This link requests an older revision. Previewing older revisions is not supported yet.");
        var files = Child(revision, "modFiles");
        if (files.ValueKind != JsonValueKind.Array || files.GetArrayLength() > 10000)
            throw new InvalidDataException("The collection file list is unavailable or too large.");
        var result = new List<NexusCollectionFile>();
        var filesById = new Dictionary<long, NexusCollectionFile>();
        foreach (var entry in files.EnumerateArray())
        {
            var file = Child(entry, "file");
            var mod = Child(file, "mod");
            var id = Number(entry, "fileId");
            if (id <= 0) throw new InvalidDataException("A collection entry has no valid file identifier.");
            var modId = Number(mod, "modId");
            var available = modId > 0 && Number(file, "fileId") == id;
            var previewFile = new NexusCollectionFile(modId, id, Text(mod, "name"), Text(file, "name"), Text(file, "version"),
                Math.Max(0, Number(file, "sizeInBytes")), Child(entry, "optional").ValueKind == JsonValueKind.True, available) { Author = Text(mod, "author"), Summary = Text(mod, "summary"), Category = Text(mod, "category") };
            if (filesById.TryAdd(id, previewFile))
                result.Add(previewFile);
            else if (filesById[id] != previewFile)
                throw new InvalidDataException($"Collection {link.Slug} revision {revisionNumber} lists conflicting entries for file {id}.");
        }
        return new(link.Slug, Text(collection, "name"), Text(collection, "summary"), (int)revisionNumber, result.AsReadOnly()) { ThumbnailUrl = SafeImageUrl(Text(Child(collection, "tileImage"), "url")), ManifestLink = Text(revision, "downloadLink") };
    }

    public async Task<IReadOnlyList<DivinityModManager.Models.DivinityLoadOrderEntry>> LoadOrderAsync(NexusCollectionPreview preview, string apiKey, CancellationToken token)
    {
        if (!Regex.IsMatch(preview.ManifestLink ?? "", @"^/v2/collections/[1-9][0-9]*/revisions/[1-9][0-9]*/download_link$"))
            throw new InvalidDataException("This collection does not expose a manifest download.");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.nexusmods.com" + preview.ManifestLink);
        request.Headers.Add("apikey", apiKey);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        using var body = await ReadBoundedAsync(await response.Content.ReadAsStreamAsync(token), token);
        using var links = JsonDocument.Parse(body);
        var candidates = Child(links.RootElement, "download_links");
        var candidate = candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0 ? candidates[0] : Child(links.RootElement, "download_link");
        var url = Text(candidate, "URI");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0
            || !(uri.Host.EndsWith(".nexusmods.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".nexus-cdn.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Nexus returned an unsupported collection download address.");
        // No account headers are forwarded to the download host, and redirects are disabled by the caller.
        using var download = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        download.EnsureSuccessStatusCode();
        using var archiveData = await ReadBoundedAsync(await download.Content.ReadAsStreamAsync(token), token);
        using var archive = SharpCompress.Archives.ArchiveFactory.OpenArchive(archiveData, new SharpCompress.Readers.ReaderOptions());
        var manifests = archive.Entries.Where(e => !e.IsDirectory && e.Key?.Replace('\\', '/') == "collection.json").Take(2).ToArray();
        if (manifests.Length != 1) throw new InvalidDataException("The collection has no unique collection.json manifest.");
        using var entry = manifests[0].OpenEntryStream();
        using var data = await ReadBoundedAsync(entry, token);
        return ParseLoadOrder(Encoding.UTF8.GetString(data.ToArray()));
    }

    private static async Task<MemoryStream> ReadBoundedAsync(Stream stream, CancellationToken token)
    {
        using (stream)
        {
            var result = new MemoryStream();
            try
            {
                var block = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(block.AsMemory(), token)) > 0)
                {
                    if (result.Length + read > MaximumResponseBytes) throw new InvalidDataException("The collection manifest is too large.");
                    result.Write(block, 0, read);
                }
                result.Position = 0;
                return result;
            }
            catch { result.Dispose(); throw; }
        }
    }

    public static IReadOnlyList<DivinityModManager.Models.DivinityLoadOrderEntry> ParseLoadOrder(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!Text(Child(root, "info"), "domainName").Equals("baldursgate3", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The collection manifest is not for Baldur’s Gate 3.");
        var order = Child(root, "loadOrder");
        if (order.ValueKind != JsonValueKind.Array || order.GetArrayLength() > 10000)
            throw new InvalidDataException("This collection does not include a supported BG3 load order.");
        var result = new List<DivinityModManager.Models.DivinityLoadOrderEntry>();
        var seen = new HashSet<Guid>();
        foreach (var item in order.EnumerateArray())
        {
            if (Child(item, "enabled").ValueKind != JsonValueKind.True) continue;
            var data = Child(item, "data");
            // Listed game/override packages do not belong in the player's ordered PAK list.
            if (Child(data, "isListed").ValueKind == JsonValueKind.True) continue;
            if (!Guid.TryParse(Text(data, "uuid"), out var uuid) || uuid == Guid.Empty || !seen.Add(uuid))
                throw new InvalidDataException("The collection load order contains invalid or duplicate mod identifiers.");
            result.Add(new() { UUID = uuid.ToString(), Name = Text(item, "name") });
        }
        if (result.Count == 0) throw new InvalidDataException("This collection does not provide an enabled BG3 load order to save.");
        return result.AsReadOnly();
    }

    public static string SafeImageUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.Host is "media.nexusmods.com" or "images.nexusmods.com" or "staticdelivery.nexusmods.com" ? value : "";

    private static JsonElement Child(JsonElement parent, string name) => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var child) ? child : default;
    private static string Text(JsonElement parent, string name) => Child(parent, name) is var child && child.ValueKind == JsonValueKind.String ? child.GetString() ?? "" : "";
    private static long Number(JsonElement parent, string name)
    {
        var child = Child(parent, name);
        if (child.ValueKind == JsonValueKind.Number && child.TryGetInt64(out var number)) return number;
        return child.ValueKind == JsonValueKind.String && Int64.TryParse(child.GetString(), out number) ? number : 0;
    }
}
