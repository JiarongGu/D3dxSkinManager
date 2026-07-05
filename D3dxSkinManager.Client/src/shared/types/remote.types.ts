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
}

export interface RemoteModCard {
  title: string;
  detailUrl: string;
  imageUrl: string;
}

export interface RemoteBrowseResult {
  cards: RemoteModCard[];
  page: number;
  totalPages?: number;
}

export type RemoteDownloadType = 'cloudreve' | 'external';

export interface RemoteDownloadOption {
  name: string;
  url: string;
  type: RemoteDownloadType | string;
}

export interface RemoteModDetail {
  title: string;
  detailUrl: string;
  images: string[];
  downloads: RemoteDownloadOption[];
}

export interface RemoteResolveResult {
  fileName: string;
  size: number;
  downloadUrl: string;
}

export interface RemoteDownloadImportAck {
  started: boolean;
  processId: string;
}

export interface RemoteIndexEntry {
  id: string;
  title: string;
  detailUrl: string;
  imageUrl: string;
  dateHint?: string;
  sortKey: number;
  firstSeenUtc: string;
  lastSeenUtc: string;
  /** True when a mod in the current profile was imported from this entry. */
  imported: boolean;
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
export interface RemoteSourceConfigDto {
  id: string;
  name: string;
  baseUrl: string;
  engine?: string;
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
  resolvers: { match: string; type: string; name: string }[];
}

export interface RemoteSourceTestResult {
  cardCount: number;
  sampleTitles: string[];
  totalPages?: number;
  detailTitle?: string;
  detailDownloads: RemoteDownloadOption[];
  detailImageCount: number;
}
