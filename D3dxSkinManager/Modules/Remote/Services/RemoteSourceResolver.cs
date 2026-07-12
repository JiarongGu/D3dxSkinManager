using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Pure 3-tier resolution of a library's EFFECTIVE remote-source config (remote-library-redesign.md):
/// <c>res default ← sparse local override ← the library's param values</c>. Done at the JSON layer so
/// "absent key = inherit" is TRUE sparse inheritance — a res update to a field the local didn't override
/// (a new game, a fixed regex) flows straight through. Then <c>{param.&lt;key&gt;}</c> placeholders are
/// substituted from the library's values (source param Defaults fill the gaps). No I/O, no state.
///
/// The overlay is a RAW JSON string (not a typed config) on purpose: a typed <c>RemoteSourceConfig</c>
/// can't be sparse — its default-valued fields would serialize and clobber the base. The local override
/// file stores only the overridden keys; <see cref="Diff"/> produces exactly that.
/// </summary>
public interface IRemoteSourceResolver
{
    /// <summary>Effective config = deep-merge(<paramref name="baseCfg"/>, <paramref name="sparseOverlayJson"/>)
    /// with every <c>{param.&lt;key&gt;}</c> substituted from <paramref name="paramValues"/> (declared param
    /// Defaults fill unset keys). A null/blank overlay = the base as-is.</summary>
    RemoteSourceConfig Resolve(RemoteSourceConfig baseCfg, string? sparseOverlayJson, IReadOnlyDictionary<string, string>? paramValues);

    /// <summary>The SPARSE overlay JSON to persist as a local override: only the keys where
    /// <paramref name="edited"/> differs from <paramref name="baseCfg"/> (plus <c>id</c> so it's
    /// addressable). Storing just the diff keeps res updates to untouched fields flowing through.</summary>
    string Diff(RemoteSourceConfig baseCfg, RemoteSourceConfig edited);
}

public class RemoteSourceResolver : IRemoteSourceResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public RemoteSourceConfig Resolve(RemoteSourceConfig baseCfg, string? sparseOverlayJson, IReadOnlyDictionary<string, string>? paramValues)
    {
        var node = ToNode(baseCfg);
        if (!string.IsNullOrWhiteSpace(sparseOverlayJson) && TryParseObject(sparseOverlayJson, out var overlay))
            DeepMerge(node, overlay!);

        // Declared params come from the MERGED config (an overlay may add/replace them).
        var declaredParams = node["params"]?.Deserialize<List<RemoteSourceParam>>(JsonOptions) ?? new();
        var values = BuildValues(declaredParams, paramValues);
        if (values.Count > 0) Substitute(node, values);

        return node.Deserialize<RemoteSourceConfig>(JsonOptions) ?? new RemoteSourceConfig();
    }

    public string Diff(RemoteSourceConfig baseCfg, RemoteSourceConfig edited)
    {
        var diff = DiffNode(ToNode(baseCfg), ToNode(edited)) as JsonObject ?? new JsonObject();
        diff["id"] = edited.Id; // always addressable — the overlay is keyed by source id
        return diff.ToJsonString(JsonOptions);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static JsonObject ToNode(RemoteSourceConfig cfg) =>
        JsonNode.Parse(JsonSerializer.Serialize(cfg, JsonOptions)) as JsonObject ?? new JsonObject();

    private static bool TryParseObject(string json, out JsonObject? obj)
    {
        try { obj = JsonNode.Parse(json) as JsonObject; return obj != null; }
        catch { obj = null; return false; }
    }

    /// <summary>Deep-merge <paramref name="overlay"/> INTO <paramref name="target"/>: matching object
    /// keys recurse; any other node the overlay provides REPLACES target's. A key the overlay omits keeps
    /// the target (res) value — that omission IS the inheritance.</summary>
    private static void DeepMerge(JsonObject target, JsonObject overlay)
    {
        foreach (var (key, ov) in overlay)
        {
            if (ov is JsonObject oObj && target[key] is JsonObject tObj) DeepMerge(tObj, oObj);
            else target[key] = ov?.DeepClone();
        }
    }

    /// <summary>Replace <c>{param.&lt;key&gt;}</c> in every string leaf of the tree.</summary>
    private static void Substitute(JsonNode? node, IReadOnlyDictionary<string, string> values)
    {
        if (node is JsonObject obj)
            foreach (var key in obj.Select(kv => kv.Key).ToList())
                ReplaceChild(child => obj[key] = child, obj[key], values);
        else if (node is JsonArray arr)
            for (var i = 0; i < arr.Count; i++)
            {
                var idx = i;
                ReplaceChild(child => arr[idx] = child, arr[i], values);
            }
    }

    private static void ReplaceChild(Action<JsonNode?> set, JsonNode? child, IReadOnlyDictionary<string, string> values)
    {
        if (child is JsonValue v && v.TryGetValue<string>(out var s))
        {
            var replaced = Apply(s, values);
            if (!ReferenceEquals(replaced, s)) set(replaced);
        }
        else
        {
            Substitute(child, values);
        }
    }

    private static string Apply(string s, IReadOnlyDictionary<string, string> values)
    {
        if (!s.Contains("{param.", StringComparison.Ordinal)) return s;
        foreach (var (k, val) in values) s = s.Replace("{param." + k + "}", val, StringComparison.Ordinal);
        return s;
    }

    /// <summary>Merge declared param Defaults (low priority) with the library's values (win).</summary>
    private static Dictionary<string, string> BuildValues(List<RemoteSourceParam> declared, IReadOnlyDictionary<string, string>? paramValues)
    {
        var v = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in declared)
            if (!string.IsNullOrEmpty(p.Key) && p.Default != null) v[p.Key] = p.Default;
        if (paramValues != null)
            foreach (var (k, val) in paramValues)
                if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(val)) v[k] = val;
        return v;
    }

    /// <summary>Recursive structural diff: object keys recurse; a scalar/array equal to base → dropped
    /// (inherited), different → the edited value. `edited` is a full config, so there are no removals.
    /// Empty-vs-absent is NOT a diff: a field the base omits (null) that the edited config carries as ""
    /// (or []/{}) is treated as unchanged — else a form that fills blanks would write a spurious overlay
    /// and the source would read as "modified" when it isn't.</summary>
    private static JsonNode? DiffNode(JsonNode? baseNode, JsonNode? editedNode)
    {
        if (IsEmptyish(baseNode) && IsEmptyish(editedNode)) return null; // both empty → no change
        if (editedNode is JsonObject e && baseNode is JsonObject b)
        {
            var diff = new JsonObject();
            foreach (var (key, ev) in e)
            {
                var d = DiffNode(b[key], ev);
                if (d != null) diff[key] = d.DeepClone();
            }
            return diff.Count > 0 ? diff : null;
        }
        return JsonNode.DeepEquals(baseNode, editedNode) ? null : editedNode?.DeepClone();
    }

    /// <summary>An absent/null node, an empty string, or an empty array/object — all "no value".</summary>
    private static bool IsEmptyish(JsonNode? n) => n switch
    {
        null => true,
        JsonValue v => v.TryGetValue<string>(out var s) && string.IsNullOrEmpty(s),
        JsonArray a => a.Count == 0,
        JsonObject o => o.Count == 0,
        _ => false,
    };
}
