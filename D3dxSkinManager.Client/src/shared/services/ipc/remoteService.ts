/**
 * Remote mod library IPC service — REMOTE module (RemoteFacade).
 * Browse configured remote sites, resolve downloads, kick off download+import
 * (fire-and-forget; progress arrives via the Activity panel / ProcessRegistry).
 */

import { BaseModuleService } from '../baseModuleService';
import type {
  RemoteBrowseResult,
  RemoteDownloadImportAck,
  RemoteDownloadOption,
  RemoteIndexPage,
  RemoteLibrariesState,
  RemoteLibrary,
  RemoteLibraryAddResult,
  RemoteModDetail,
  RemoteResolveResult,
  RemoteSourceConfig,
  RemoteSourceInfo,
  RemoteSourceTestResult,
  RemoteTagCount,
  RemoteTagRule,
  OnlineStorageAccountInfo,
} from '../../types/remote.types';

export class RemoteService extends BaseModuleService {
  constructor() {
    super('REMOTE');
  }

  async getSources(profileId: string): Promise<RemoteSourceInfo[]> {
    return this.sendArrayMessage<RemoteSourceInfo>('GET_SOURCES', profileId);
  }

  async browse(profileId: string, sourceId: string, listId: string, page: number): Promise<RemoteBrowseResult> {
    return this.sendMessage<RemoteBrowseResult>('BROWSE', profileId, { sourceId, listId, page });
  }

  async search(profileId: string, sourceId: string, query: string, listId?: string): Promise<RemoteBrowseResult> {
    return this.sendMessage<RemoteBrowseResult>('SEARCH', profileId, { sourceId, query, listId });
  }

  /** listId lets the backend merge detail-page tags (e.g. GameBanana's sub category) into the index. */
  async getDetail(profileId: string, sourceId: string, url: string, listId?: string): Promise<RemoteModDetail> {
    return this.sendMessage<RemoteModDetail>('GET_DETAIL', profileId, { sourceId, url, listId });
  }

  async resolveDownload(profileId: string, option: RemoteDownloadOption): Promise<RemoteResolveResult> {
    return this.sendMessage<RemoteResolveResult>('RESOLVE_DOWNLOAD', profileId, { option });
  }

  /** Immediate ack — the download+import runs in the background (Activity panel).
   * listId/entryId record the durable remote identity; tags feed the library's tag→category rules;
   * categoryId is the user's download-time choice and overrides the rules; password is the user's
   * unzip password (overrides the resolver's site default — imports always extract + repack). */
  async downloadImport(
    profileId: string,
    sourceId: string,
    detail: RemoteModDetail,
    option: RemoteDownloadOption,
    context?: { listId?: string; entryId?: string; tags?: string[]; categoryId?: string; password?: string },
  ): Promise<RemoteDownloadImportAck> {
    return this.sendMessage<RemoteDownloadImportAck>('DOWNLOAD_IMPORT', profileId, {
      sourceId, detail, option,
      listId: context?.listId, entryId: context?.entryId, tags: context?.tags,
      categoryId: context?.categoryId, password: context?.password,
    });
  }

  /** Filtered + paged slice of the SYNCED local index (instant; empty info when never synced). */
  async indexQuery(
    profileId: string,
    sourceId: string,
    listId: string,
    search: string | undefined,
    page: number,
    pageSize: number,
    sort?: string,
    tag?: string,
  ): Promise<RemoteIndexPage> {
    return this.sendMessage<RemoteIndexPage>('INDEX_QUERY', profileId, { sourceId, listId, search, page, pageSize, sort, tag });
  }

  /** Distinct site tags present in the synced index (for the filter dropdown), by frequency. */
  async indexTags(profileId: string, sourceId: string, listId: string): Promise<RemoteTagCount[]> {
    return this.sendArrayMessage<RemoteTagCount>('INDEX_TAGS', profileId, { sourceId, listId });
  }

  /**
   * Start a background sync (Activity panel; immediate ack). `full` forces a complete re-crawl of
   * every page and prunes entries the site no longer lists; the default is a cheap incremental update
   * that stops at the first page with nothing new.
   */
  async indexSync(profileId: string, sourceId: string, listId: string, full = false): Promise<RemoteDownloadImportAck> {
    return this.sendMessage<RemoteDownloadImportAck>('INDEX_SYNC', profileId, { sourceId, listId, full });
  }

  /** Validate + persist a (possibly user-authored) adapter. */
  async saveSource(profileId: string, config: RemoteSourceConfig): Promise<RemoteSourceConfig> {
    return this.sendMessage<RemoteSourceConfig>('SAVE_SOURCE', profileId, { config });
  }

  async deleteSource(profileId: string, sourceId: string): Promise<boolean> {
    return this.sendMessage<boolean>('DELETE_SOURCE', profileId, { sourceId });
  }

  /** Run a candidate config against the live site (page 1 + first detail). */
  async testSource(profileId: string, config: RemoteSourceConfig, listId?: string): Promise<RemoteSourceTestResult> {
    return this.sendMessage<RemoteSourceTestResult>('TEST_SOURCE', profileId, { config, listId });
  }

  // ---- configured libraries (a profile owns many; switchable) ------------------------------

  async libraryGetState(profileId: string): Promise<RemoteLibrariesState> {
    return this.sendMessage<RemoteLibrariesState>('LIBRARY_GET_STATE', profileId);
  }

  /** Add a library; sync=true starts its index crawl immediately. */
  async libraryAdd(
    profileId: string,
    sourceId: string,
    listId: string,
    name?: string,
    tagRules?: RemoteTagRule[],
    sync = true,
  ): Promise<RemoteLibraryAddResult> {
    return this.sendMessage<RemoteLibraryAddResult>('LIBRARY_ADD', profileId, { sourceId, listId, name, tagRules, sync });
  }

  /** Edit name/tag rules (source+game identity is fixed after creation). */
  async libraryUpdate(profileId: string, library: RemoteLibrary): Promise<RemoteLibrary> {
    return this.sendMessage<RemoteLibrary>('LIBRARY_UPDATE', profileId, { library });
  }

  async libraryRemove(profileId: string, libraryId: string): Promise<boolean> {
    return this.sendMessage<boolean>('LIBRARY_REMOVE', profileId, { libraryId });
  }

  async librarySetActive(profileId: string, libraryId: string): Promise<RemoteLibrariesState> {
    return this.sendMessage<RemoteLibrariesState>('LIBRARY_SET_ACTIVE', profileId, { libraryId });
  }

  async getSourceTemplate(profileId: string): Promise<string> {
    return this.sendMessage<string>('GET_SOURCE_TEMPLATE', profileId);
  }

  /** The FULL adapter config (for the edit screen; GET_SOURCES only carries display info). */
  async getSourceConfig(profileId: string, sourceId: string): Promise<RemoteSourceConfig> {
    return this.sendMessage<RemoteSourceConfig>('GET_SOURCE_CONFIG', profileId, { sourceId });
  }

  // ---- online-storage accounts (auth'd download hosts, e.g. Quark) --------------------------

  /** Saved logins for auth'd download hosts (cookie-free view). */
  async accountList(profileId: string): Promise<OnlineStorageAccountInfo[]> {
    return this.sendArrayMessage<OnlineStorageAccountInfo>('ACCOUNT_LIST', profileId);
  }

  /** Open the in-app login window for a provider. Fire-and-forget (a real QR login outlives the IPC
   * bridge timeout) — returns immediately; the account status updates via the ONLINE_ACCOUNT_CHANGED
   * event when the window finishes. */
  async accountLogin(profileId: string, provider: string): Promise<{ started: boolean }> {
    return this.sendMessage<{ started: boolean }>('ACCOUNT_LOGIN', profileId, { provider });
  }

  /** Log out (remove the saved cookie) and return the remaining accounts. */
  async accountRemove(profileId: string, provider: string): Promise<OnlineStorageAccountInfo[]> {
    return this.sendMessage<OnlineStorageAccountInfo[]>('ACCOUNT_REMOVE', profileId, { provider });
  }
}
