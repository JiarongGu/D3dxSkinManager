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
