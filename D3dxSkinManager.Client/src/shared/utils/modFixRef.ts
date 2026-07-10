/**
 * Parse the fix marker ModFixService records in ModEntity.Metadata
 * (ModFixService.WriteFixMetadata: metadata.fix = { lastFixedUtc }) and decide whether a mod may
 * need re-fixing after a game update.
 *
 * There is no reliable automatic, game-free signal for a game patch (the game/importer version isn't
 * readable and 3DMigoto's authoritative "hashes broke" signal only exists while the game runs), so the
 * flag is a DATE APPROXIMATION: the user marks the game as updated (ProfileConfiguration.gameUpdatedUtc)
 * and a mod is flagged when its last in-app fix predates that mark.
 */

export interface ModFixInfo {
  /** ISO time of the mod's last successful in-app fix, if any. */
  lastFixedUtc?: string;
}

export function parseModFixInfo(metadata?: string): ModFixInfo | undefined {
  if (!metadata) return undefined;
  try {
    const fix = JSON.parse(metadata)?.fix;
    if (!fix?.lastFixedUtc) return undefined;
    return { lastFixedUtc: fix.lastFixedUtc };
  } catch {
    return undefined;
  }
}

/**
 * True when a mod was fixed in-app BEFORE the user's "game updated" watermark — i.e. its hashes may
 * be stale. Mods never fixed in-app (no lastFixedUtc) are NOT flagged: there's no basis to judge them
 * (they may have shipped pre-fixed), and flagging every mod after an update would be pure noise.
 */
export function modNeedsRefix(metadata: string | undefined, gameUpdatedUtc: string | undefined): boolean {
  if (!gameUpdatedUtc) return false;
  const info = parseModFixInfo(metadata);
  if (!info?.lastFixedUtc) return false;
  const fixed = Date.parse(info.lastFixedUtc);
  const updated = Date.parse(gameUpdatedUtc);
  return Number.isFinite(fixed) && Number.isFinite(updated) && fixed < updated;
}
