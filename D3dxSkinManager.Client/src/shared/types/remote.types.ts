/**
 * Remote mod library types — mirror Modules/Remote/Models/RemoteModels.cs.
 * NOTE: Enums are camelCase because IpcHandler serializes with JsonStringEnumConverter(CamelCase);
 * the resolver `type` here is a plain string union ('cloudreve' | 'external') defined lowercase in
 * the adapter JSON itself.
 */

export interface RemoteListConfig {
  id: string;
  name: string;
}

export interface RemoteSourceInfo {
  id: string;
  name: string;
  baseUrl: string;
  lists: RemoteListConfig[];
  hasSearch: boolean;
  /** Per-language display labels for site tags: lang → (raw tag → label). Raw names stay identity. */
  tagLabels: Record<string, Record<string, string>>;
  /** Per-site card-thumbnail display config (nested for future knobs). */
  thumbnail?: RemoteThumbnailConfig;
  /** Library-configurable input params the source declares — rendered as fields on library add/edit. */
  params: RemoteSourceParam[];
  /** camelCase-serialized C# string: "default" (shipped, unmodified), "customized" (shipped + local
   *  overlay), or "custom" (user-added source). Drives the origin badge + reset-to-default action. */
  origin: 'default' | 'customized' | 'custom';
}

/** A library-configurable input param a source declares (a text input or a select). Its value goes
 *  into the library's paramValues and substitutes for {param.<key>} in the effective source config. */
export interface RemoteSourceParam {
  key: string;
  label: string;
  /** camelCase-serialized C# string: "input" (free text) or "select" (pick from options). */
  type: 'input' | 'select';
  options: RemoteParamOption[];
  default?: string;
  required: boolean;
}

export interface RemoteParamOption {
  value: string;
  label: string;
}

/** How a site's card thumbnail is displayed (cover-crop tuning). */
export interface RemoteThumbnailConfig {
  /** CSS object-position for the crop, e.g. "50% 20%" (keep more top). Empty = centered. */
  position?: string;
}

export interface RemoteModCard {
  title: string;
  detailUrl: string;
  imageUrl: string;
  /** Site tags (standardized across engines — e.g. GameBanana super category). */
  tags: string[];
  dateHint?: string;
  /** The SITE's content rating: true = rated sensitive (always veiled), false = rated safe
   * (never veiled), null/undefined = no rating — the pixel heuristic decides. */
  sensitive?: boolean | null;
}

export interface RemoteBrowseResult {
  cards: RemoteModCard[];
  page: number;
  totalPages?: number;
}

/** Resolver `type` a download option carries (camelCase; defined in the adapter JSON + C#
 *  RemoteResolverRule.Type). "external"/unknown = browser-only. */
export type RemoteDownloadType = 'cloudreve' | 'quark' | 'baidu' | 'mega' | 'kodbox' | 'direct' | 'external';

/** Types the app downloads + imports IN-APP; every other type opens in the system browser.
 *  MUST mirror the backend `RemoteImportService.IsImportable` — add a new importable resolver type to
 *  BOTH lists or the button lies (a kodbox/mega option opened the browser because this drifted, fixed
 *  2026-07-14; `baidu` added 2026-07-14 once its resolver shipped). Note: `quark`/`baidu` import in-app
 *  only when that account is logged in; otherwise the resolve surfaces QUARK_NOT_LOGGED_IN / a Baidu login. */
export const IMPORTABLE_DOWNLOAD_TYPES = ['cloudreve', 'quark', 'baidu', 'mega', 'kodbox', 'direct'] as const;

export const isImportableDownloadType = (type: string): boolean =>
  (IMPORTABLE_DOWNLOAD_TYPES as readonly string[]).includes(type);

export interface RemoteDownloadOption {
  name: string;
  url: string;
  type: RemoteDownloadType | string;
  /** Declared file size in bytes from the site's metadata (GameBanana _nFilesize); 0/absent when unknown. */
  sizeBytes?: number;
  /** Site-known unzip password from the adapter's resolver rule (import uses it by default). */
  unzipPassword?: string;
  /** Resolver opted into the recursive-unwrap workflow (carve disguised + unwrap nested layers). */
  unwrapNested?: boolean;
}

export interface RemoteModDetail {
  title: string;
  detailUrl: string;
  images: string[];
  downloads: RemoteDownloadOption[];
  /** Site tags visible on the detail page (e.g. GameBanana sub category). */
  tags: string[];
  /** Plain-text page description, when the engine extracts one (GameBanana _sText). */
  description?: string;
}

export interface RemoteResolveResult {
  fileName: string;
  size: number;
  downloadUrl: string;
  /** Headers the download GET must carry (auth'd hosts like Quark — cookie + UA). */
  downloadHeaders?: Record<string, string>;
}

/** A saved login for an auth'd download host (Quark). Cookie-free — the backend never ships it. */
export interface OnlineStorageAccountInfo {
  provider: string;
  displayName: string;
  loggedIn: boolean;
  savedAtUtc?: string;
}

export interface RemoteDownloadImportAck {
  started: boolean;
  processId: string;
}

/** A distinct site tag present in the index + its mod count (filter dropdown). */
export interface RemoteTagCount {
  name: string;
  count: number;
}

export interface RemoteIndexEntry {
  id: string;
  title: string;
  detailUrl: string;
  imageUrl: string;
  /** Site tags (chips + filter). */
  tags: string[];
  dateHint?: string;
  /** Site content rating (see RemoteModCard.sensitive). */
  sensitive?: boolean | null;
  sortKey: number;
  firstSeenUtc: string;
  lastSeenUtc: string;
  /** True when a mod in the current profile was imported from this entry. */
  imported: boolean;
  /** Local mod id(s) imported from this entry (when imported) — lets the UI jump to them ("locate").
   * A list because an entry can be downloaded multiple times. */
  localModIds?: string[];
}

export interface RemoteIndexInfo {
  sourceId: string;
  listId: string;
  syncedAtUtc?: string;
  totalPages: number;
  entryCount: number;
}

export interface RemoteIndexPage {
  info: RemoteIndexInfo;
  entries: RemoteIndexEntry[];
  /** Entries matching the filter (before paging). */
  total: number;
}

/** Full adapter config — mirrors RemoteSourceConfig (the editable JSON). */
export interface RemoteSourceConfig {
  id: string;
  name: string;
  baseUrl: string;
  engine?: string;
  /** Transport: "http" (default) or "webview" (render JS in an off-screen WebView2). Separate from engine. */
  fetcher?: string;
  lists: RemoteListConfig[];
  listUrlFirstPage: string;
  listUrlTemplate?: string;
  searchUrlTemplate?: string;
  cardPattern: string;
  cardScopePattern?: string;
  totalPagesPattern?: string;
  detailTitlePattern: string;
  detailImagePattern?: string;
  downloadLinkPattern: string;
  entryIdPattern?: string;
  imageDatePattern?: string;
  /** Regex (named group: tag) deriving a tag from the TITLE for sites with no tag taxonomy —
   * applied only to entries that have no tags of their own (e.g. huihui `^(?<tag>\S+)\s`). */
  titleTagPattern?: string;
  /** Per-language display labels for site tags (lang → raw tag → label). */
  tagLabels?: Record<string, Record<string, string>>;
  /** Library-configurable input params the source declares (substituted for {param.<key>}). */
  params?: RemoteSourceParam[];
  resolvers: { match: string; type: string; name: string }[];
}

export interface RemoteSourceTestResult {
  /** True when the list page fetched + parsed without error (cards may still be 0). */
  success: boolean;
  /** Failure reason when success is false (network/parse/validation) — shown in the red indicator. */
  error?: string;
  cardCount: number;
  sampleTitles: string[];
  totalPages?: number;
  /** True once the first card's detail page was fetched. */
  detailFetched: boolean;
  detailTitle?: string;
  detailDownloads: RemoteDownloadOption[];
  detailImageCount: number;
}

/** One ordered import rule: matches when ALL `tags` are carried (if any) AND `titlePattern` matches
 * the title (if set) — at least one criterion required. Title regex covers tagless sites (huihui). */
export interface RemoteTagRule {
  name: string;
  tags: string[];
  /** Optional case-insensitive regex against the mod title. */
  titlePattern?: string;
  categoryId: string;
}

/** A configured remote library (site + game + import rules). A profile owns MANY, switchable. */
export interface RemoteLibrary {
  id: string;
  sourceId: string;
  listId: string;
  name: string;
  /** Ordered — first rule whose tags all match wins; no match = uncategorized. */
  tagRules: RemoteTagRule[];
  /** This library's values for the source's declared params (key → value). */
  paramValues: Record<string, string>;
  /** Detail fetch mode: false = live-first (fetch fresh, fall back to the saved copy on failure — the
   *  default), true = cache-first (serve the saved copy, refresh on demand). */
  preferCache: boolean;
  addedAtUtc: string;
}

export interface RemoteLibrariesState {
  libraries: RemoteLibrary[];
  activeLibraryId?: string;
}

export interface RemoteLibraryAddResult {
  library: RemoteLibrary;
  processId: string;
}
