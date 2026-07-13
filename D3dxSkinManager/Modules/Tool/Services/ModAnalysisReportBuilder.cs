using System.Text.Json;
using D3dxSkinManager.Modules.Tool.Models;

namespace D3dxSkinManager.Modules.Tool.Services;

/// <summary>
/// Pure report-assembly for mod analysis: groups the analyzed findings into duplicate/similar sets and
/// builds the load-order conflict list. Extracted verbatim from <see cref="ModAnalysisService"/>
/// (behavior-preserving) — every method is a stateless static over the finding results, so this splits the
/// grouping algorithm out of the 1200-line service. The service calls <see cref="GroupDuplicates"/> +
/// <see cref="BuildConflicts"/>; the scoring/overlap helpers are internal to the algorithm.
/// </summary>
internal static class ModAnalysisReportBuilder
{
    public static void GroupDuplicates(List<ModAnalysisResult> results, FullAnalysisReport report)
    {
        // Phase 1: Exact buffer hash match (fast path — catches identical buffer sets)
        var exactGroups = results.Where(r => !string.IsNullOrEmpty(r.BufferHash))
            .GroupBy(r => r.BufferHash)
            .Where(g => g.Select(m => m.ModId).Distinct().Count() > 1);

        var groupedModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in exactGroups)
        {
            var mods = group.GroupBy(m => m.ModId).Select(g => g.First()).ToList();
            AddDuplicateGroup(mods, report);
            foreach (var m in mods) groupedModIds.Add(m.ModId);
        }

        // Phase 2: SCORED SIMILARITY — replaces the old buffer-only ≥80%-subset check, which was
        // brittle: single hard cutoff, ignored textures and target hashes entirely, and gave the
        // user no signal of HOW similar a group is. The overlap coefficient (|∩| / min size)
        // handles both cases the old check aimed at (a merged mod CONTAINING another; edited
        // variants sharing most bytes) plus retexture-only variants it missed.
        var resultLookup = results.GroupBy(r => r.ModId).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        GroupSimilar(results, report, groupedModIds, resultLookup);
    }

    /// <summary>Minimum weighted similarity for two mods to land in one Similar group.</summary>
    private const double SimilarityThreshold = 0.70;

    /// <summary>
    /// Overlap coefficient: |A ∩ B| / min(|A|, |B|). 1.0 when one set contains the other — the
    /// right notion for "mod B is mod A plus extras" (merges, added variants). Empty sets score 0
    /// ("no evidence", never "identical") so ini-only mods can't match everything.
    /// </summary>
    private static double Overlap(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        int inter = a.Count < b.Count ? a.Count(b.Contains) : b.Count(a.Contains);
        return (double)inter / Math.Min(a.Count, b.Count);
    }

    /// <summary>
    /// Weighted similarity of two analyzed mods. Asset BYTES dominate: shared target hashes only
    /// say "same character/part", which every mod for that character satisfies — weighting it high
    /// grouped three DIFFERENT outfits that merely shared a base mesh (user report 2026-07-05).
    /// A pair must also clear a hard byte-overlap gate (≥60% of the smaller mod's buffers OR
    /// textures shared) before the weighted score even applies.
    /// Exact clones → 1.0; retexture variant (same buffers) → ~0.75; a merge containing the mod →
    /// ~1.0; different outfits off a common base (~half-shared buffers) → gated out or ~0.55.
    /// </summary>
    private static double SimilarityScore(ModAnalysisResult x, ModAnalysisResult y)
    {
        var tx = new HashSet<string>(x.TargetHashes, StringComparer.OrdinalIgnoreCase);
        var ty = new HashSet<string>(y.TargetHashes, StringComparer.OrdinalIgnoreCase);
        var bx = new HashSet<string>(x.BufferFileHashes, StringComparer.OrdinalIgnoreCase);
        var by = new HashSet<string>(y.BufferFileHashes, StringComparer.OrdinalIgnoreCase);
        var xx = new HashSet<string>(x.TextureFileHashes, StringComparer.OrdinalIgnoreCase);
        var xy = new HashSet<string>(y.TextureFileHashes, StringComparer.OrdinalIgnoreCase);

        var bufferOverlap = Overlap(bx, by);
        var textureOverlap = Overlap(xx, xy);
        if (Math.Max(bufferOverlap, textureOverlap) < 0.6) return 0; // byte-overlap gate

        return 0.2 * Overlap(tx, ty) + 0.55 * bufferOverlap + 0.25 * textureOverlap;
    }

    private static void GroupSimilar(
        List<ModAnalysisResult> results,
        FullAnalysisReport report,
        HashSet<string> groupedModIds,
        Dictionary<string, ModAnalysisResult> resultLookup)
    {
        // Candidates: ungrouped mods with enough signal (a target hash set AND some hashed assets).
        var candidates = results
            .Where(r => !groupedModIds.Contains(r.ModId) &&
                        r.TargetHashes.Count > 0 &&
                        (r.BufferFileHashes.Count > 0 || r.TextureFileHashes.Count > 0))
            .GroupBy(r => r.ModId).Select(g => g.First()).ToList();
        if (candidates.Count < 2) return;

        // Inverted index on target hashes — only pairs overriding at least one shared hash are scored.
        var hashToMods = new Dictionary<string, List<ModAnalysisResult>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in candidates)
            foreach (var h in mod.TargetHashes)
            {
                if (!hashToMods.TryGetValue(h, out var list)) { list = []; hashToMods[h] = list; }
                list.Add(mod);
            }

        var groups = new List<(HashSet<string> Ids, List<double> Scores)>();
        var scoredPairs = new HashSet<string>();

        foreach (var mod in candidates)
        {
            var partners = mod.TargetHashes
                .Where(hashToMods.ContainsKey)
                .SelectMany(h => hashToMods[h])
                .Where(c => !string.Equals(c.ModId, mod.ModId, StringComparison.OrdinalIgnoreCase))
                .GroupBy(c => c.ModId).Select(g => g.First());

            foreach (var partner in partners)
            {
                var pairKey = string.Compare(mod.ModId, partner.ModId, StringComparison.OrdinalIgnoreCase) < 0
                    ? $"{mod.ModId}|{partner.ModId}" : $"{partner.ModId}|{mod.ModId}";
                if (!scoredPairs.Add(pairKey)) continue;

                var score = SimilarityScore(mod, partner);
                if (score < SimilarityThreshold) continue;

                var existing = groups.FirstOrDefault(g => g.Ids.Contains(mod.ModId) || g.Ids.Contains(partner.ModId));
                if (existing.Ids != null) { existing.Ids.Add(mod.ModId); existing.Ids.Add(partner.ModId); existing.Scores.Add(score); }
                else groups.Add((new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mod.ModId, partner.ModId }, [score]));
            }
        }

        foreach (var (ids, scores) in groups)
        {
            var mods = ids.Where(resultLookup.ContainsKey).Select(id => resultLookup[id]).ToList();
            if (mods.Count < 2) continue;
            AddDuplicateGroup(mods, report, DuplicateType.Similar, scores.Average());
            foreach (var m in mods) groupedModIds.Add(m.ModId);
        }
    }

    private static void AddDuplicateGroup(List<ModAnalysisResult> mods, FullAnalysisReport report,
        DuplicateType? forcedType = null, double? similarityScore = null)
    {
        var textureGroups = mods.GroupBy(m => m.TextureHash).ToList();
        var firstHashes = string.Join(",", mods[0].TargetHashes.OrderBy(h => h, StringComparer.OrdinalIgnoreCase));
        var allHashesMatch = mods.Count > 1 && mods.Skip(1).All(m =>
            string.Join(",", m.TargetHashes.OrderBy(h => h, StringComparer.OrdinalIgnoreCase)) == firstHashes);

        // Same-asset groups split into the dedup taxonomy: exact clone (same ini too) vs INI
        // VARIANT — the hash-fixed / keybind-updated copy of the same mod. The diff tokens say
        // WHAT changed so the user can decide which copy to keep.
        DuplicateType type;
        var iniDifferences = new List<string>();
        if (forcedType != null)
        {
            type = forcedType.Value;
        }
        else if (textureGroups.Count > 1)
        {
            type = DuplicateType.TextureVariant;
        }
        else
        {
            if (!allHashesMatch) iniDifferences.Add("hashes");
            iniDifferences.AddRange(CompareIniAspects(mods));
            type = iniDifferences.Count == 0 ? DuplicateType.Identical : DuplicateType.IniVariant;
        }

        // Similar groups: show the hashes the mods actually have in COMMON (first mod's list is
        // misleading when the sets only mostly overlap).
        var sharedHashes = mods.First().TargetHashes;
        if (type == DuplicateType.Similar)
        {
            IEnumerable<string> common = mods.First().TargetHashes;
            foreach (var m in mods.Skip(1))
                common = common.Intersect(m.TargetHashes, StringComparer.OrdinalIgnoreCase);
            sharedHashes = common.ToList();
        }

        report.DuplicateGroups.Add(new DuplicateGroup
        {
            Type = type,
            GroupLabel = mods.First().CategoryName,
            SharedHashes = sharedHashes,
            Mods = mods,
            AllHashesMatch = allHashesMatch,
            SimilarityScore = similarityScore,
            IniDifferences = iniDifferences
        });
        switch (type)
        {
            case DuplicateType.Identical: report.IdenticalCount++; break;
            case DuplicateType.TextureVariant: report.TextureVariantCount++; break;
            case DuplicateType.IniVariant: report.IniVariantCount++; break;
            case DuplicateType.Similar: report.SimilarCount++; break;
        }
    }

    /// <summary>
    /// Which ini ASPECTS differ across a same-asset group: "keys" (bindings), "constants"
    /// (defaults), "logic" (command lists / overrides minus hash lines). Findings without
    /// fingerprints (pre-2026-07 scans) compare as unknown — no aspect tokens are reported.
    /// </summary>
    private static List<string> CompareIniAspects(List<ModAnalysisResult> mods)
    {
        var diffs = new List<string>();
        var parsed = mods.Select(m =>
        {
            try { return m.IniFingerprints == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(m.IniFingerprints); }
            catch { return null; }
        }).ToList();
        if (parsed.Any(p => p == null)) return diffs;

        foreach (var aspect in new[] { "key", "constants", "logic" })
        {
            var values = parsed.Select(p => p!.GetValueOrDefault(aspect, "")).Distinct().ToList();
            if (values.Count > 1)
                diffs.Add(aspect == "key" ? "keys" : aspect);
        }
        return diffs;
    }

    public static void BuildConflicts(List<ModAnalysisResult> loadedMods, FullAnalysisReport report)
    {
        var hashToMods = new Dictionary<string, List<ModAnalysisResult>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in loadedMods)
            foreach (var hash in mod.TargetHashes)
            {
                if (!hashToMods.ContainsKey(hash)) hashToMods[hash] = new List<ModAnalysisResult>();
                hashToMods[hash].Add(mod);
            }
        report.Conflicts = hashToMods.Where(kv => kv.Value.Count > 1).Select(kv => new ModConflict { Hash = kv.Key, Mods = kv.Value }).ToList();
        report.ConflictCount = report.Conflicts.Count;
        report.AffectedModCount = report.Conflicts.SelectMany(c => c.Mods.Select(m => m.ModId)).Distinct().Count();
    }
}
