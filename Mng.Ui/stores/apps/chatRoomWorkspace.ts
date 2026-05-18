import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import type { HubMessage } from '@/stores/hub';
import {
  hubPayloadDatasetName,
  isChtMessagesHubPayload,
  normalizeHubMessageForChat,
} from '@/utils/chatRoomDgHub';
import { humanizeGroupDisplayToken, uniqueTrimmedPreserveOrder } from '@/utils/chatRoomGroups';
import {
  directConversationLinksPair,
  directConversationPeerStoredId,
  displayNameForStoredPersonId,
  messageAuthorIsCurrentUser,
  normalizePeerIdForDirectChat,
  participantMatchesAnyAlias,
} from '@/utils/chatRoomPersonId';
import type {
  ChatRoomSelection,
  ChtDirectConversationVm,
  ChtGroupChatVm,
  ChtMessageVm,
  ChtTopicRoomVm,
} from '@/types/apps/chatRoom';

const DS_MESSAGES = 'cht_messages';
const DS_DIRECT = 'cht_direct_conversations';
const DS_TOPICS = 'cht_topic_rooms';
const DS_GROUPS = 'cht_group_chats';

/** Hub’da ardışık cht_messages olaylarında sol paneli tek GET ile güncelle */
let sidebarHubRefreshTimer: ReturnType<typeof setTimeout> | null = null;
const SIDEBAR_HUB_DEBOUNCE_MS = 500;

function parseArrayResponse(response: unknown): unknown[] {
  if (response && Array.isArray(response)) return response;
  if (response && typeof response === 'object' && 'items' in response && Array.isArray((response as any).items))
    return (response as any).items;
  if (response && typeof response === 'object' && 'data' in response && Array.isArray((response as any).data))
    return (response as any).data;
  if (response && typeof response === 'object' && 'Data' in response && Array.isArray((response as any).Data))
    return (response as any).Data;
  return [];
}

function normId(v: unknown): string {
  if (v == null) return '';
  if (typeof v === 'string') return v;
  if (typeof v === 'object' && v !== null && ('__dataId' in v || 'dataId' in v))
    return String((v as any).__dataId ?? (v as any).dataId ?? '');
  return String(v);
}

function mapDirect(raw: any): ChtDirectConversationVm {
  return {
    dataId: normId(raw.__dataId ?? raw.DataId ?? raw.dataId),
    participantAId: String(raw.participantAId ?? raw.ParticipantAId ?? ''),
    participantBId: String(raw.participantBId ?? raw.ParticipantBId ?? ''),
    lastMessageAt: raw.lastMessageAt ?? raw.LastMessageAt ?? null,
  };
}

function mapTopic(raw: any): ChtTopicRoomVm {
  const parent = raw.parentTopicRoomId ?? raw.ParentTopicRoomId;
  return {
    dataId: normId(raw.__dataId ?? raw.DataId ?? raw.dataId),
    title: String(raw.title ?? raw.Title ?? '').trim() || '—',
    parentTopicRoomId:
      parent == null || parent === '' ? null : String(parent),
    archived: Boolean(raw.archived ?? raw.Archived ?? false),
  };
}

function mapGroup(raw: any): ChtGroupChatVm {
  return {
    dataId: normId(raw.__dataId ?? raw.DataId ?? raw.dataId),
    keycloakGroupId: String(raw.keycloakGroupId ?? raw.KeycloakGroupId ?? ''),
    displayNameCache: raw.displayNameCache ?? raw.DisplayNameCache ?? null,
  };
}

/** CHAT_ROOM_ROADMAP §3.1b: canonicalKey = sıralı iki katılımcı (min|max). */
function orderedDirectParticipants(me: string, other: string): {
  canonicalKey: string;
  participantAId: string;
  participantBId: string;
} {
  const a = me.trim();
  const b = other.trim();
  const cmp = a.localeCompare(b, undefined, { sensitivity: 'variant', numeric: true });
  if (cmp <= 0) return { canonicalKey: `${a}|${b}`, participantAId: a, participantBId: b };
  return { canonicalKey: `${b}|${a}`, participantAId: b, participantBId: a };
}

function chatErr(code: string) {
  return Object.assign(new Error(code), { code });
}

/** DataController SuccessResponse: { success, data, meta } — tek kayıt create/update cevabı */
function unwrapDataGatewayWriteResponse(res: unknown): any {
  if (res == null || typeof res !== 'object') return res;
  const o = res as Record<string, unknown>;
  const d = o.data ?? o.Data;
  if (d != null && typeof d === 'object') return d;
  return res;
}

function strPick(v: unknown): string {
  if (v == null) return '';
  if (typeof v === 'string') return v.trim();
  const t = String(v).trim();
  return t;
}

/**
 * DG şemada `persons` alanı genişletildiğinde `authorPersonId` bazen @users nesnesi olur (veya eşleşmezse null).
 * Ham JWT sub / Mongo id için listede `expand=false` kullanıyoruz; yine de nesne gelirse buradan id çıkarılır.
 */
function authorIdFromExpandedPerson(v: unknown): string {
  if (v == null) return '';
  if (typeof v === 'string') return v.trim();
  if (typeof v === 'number' || typeof v === 'boolean') return String(v).trim();
  if (typeof v !== 'object') return '';
  const o = v as Record<string, unknown>;
  const ex =
    o.__dataId ??
    o.DataId ??
    o.dataId ??
    o.keycloakUserId ??
    o.KeycloakUserId ??
    o.userId ??
    o.UserId;
  if (ex != null && (typeof ex === 'string' || typeof ex === 'number')) {
    const t = String(ex).trim();
    if (t) return t;
  }
  return '';
}

/**
 * Mesaj satırından yazar kimliği (JWT sub veya Keeper kullanıcı id’si).
 * Bazı ortamlarda yalnızca `body` dönen / `authorPersonId` eksik eski kayıtlar için alternatif alan adları ve `data` içi.
 */
function pickAuthorPersonId(raw: any, depth = 0): string {
  if (raw == null || typeof raw !== 'object' || depth > 5) return '';
  const keys = [
    'authorPersonId',
    'AuthorPersonId',
    'personId',
    'PersonId',
    'authorId',
    'AuthorId',
    'senderPersonId',
    'SenderPersonId',
    'senderId',
    'SenderId',
    'userId',
    'UserId',
    'mng_person_id',
    'MngPersonId',
    'memberPersonId',
    'MemberPersonId',
  ];
  const r = raw as Record<string, unknown>;
  for (const k of keys) {
    const v = r[k];
    const fromPerson = authorIdFromExpandedPerson(v);
    if (fromPerson) return fromPerson;
    const s = strPick(v);
    if (s && !s.startsWith('[object ')) return s;
  }
  const nested =
    (raw as Record<string, unknown>).data ??
    (raw as Record<string, unknown>).Data ??
    (raw as Record<string, unknown>).payload ??
    (raw as Record<string, unknown>).Payload;
  if (nested && typeof nested === 'object') {
    const inner = pickAuthorPersonId(nested, depth + 1);
    if (inner) return inner;
  }
  return '';
}

function mapMessage(raw: any): ChtMessageVm {
  return {
    dataId: normId(raw.__dataId ?? raw.DataId ?? raw.dataId),
    roomKind: String(raw.roomKind ?? raw.RoomKind ?? ''),
    roomRecordId: String(raw.roomRecordId ?? raw.RoomRecordId ?? ''),
    body: String(raw.body ?? raw.Body ?? ''),
    authorPersonId: pickAuthorPersonId(raw),
    createdAt: String(raw.createdAt ?? raw.CreatedAt ?? ''),
  };
}

/** DG EventPublisher: BaseDataEvent.Type → JSON `type` ("DataCreatedEvent"). Eski legacy: `eventType`. */
function eventTypeLower(message: Record<string, unknown>): string {
  return String(
    message.eventType ??
      message.EventType ??
      message.type ??
      message.Type ??
      ''
  ).toLowerCase();
}

/**
 * DG DataEventDto / Rabbit tüketiminde `data` alanı bazen JSON string; canlı hub birleştirmesi nesne bekler.
 */
function unwrapHubEventData(m: Record<string, unknown>): Record<string, unknown> | undefined {
  const raw = m.data ?? m.Data;
  if (raw == null) return undefined;
  if (typeof raw === 'string') {
    const t = raw.trim();
    if ((t.startsWith('{') && t.endsWith('}')) || (t.startsWith('[') && t.endsWith(']'))) {
      try {
        const parsed = JSON.parse(t) as unknown;
        if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) return parsed as Record<string, unknown>;
      } catch {
        return undefined;
      }
    }
    return undefined;
  }
  if (typeof raw === 'object' && !Array.isArray(raw)) return raw as Record<string, unknown>;
  return undefined;
}

function routingKeyLower(routingKey: string): string {
  return String(routingKey ?? '').toLowerCase();
}

export const useChatRoomWorkspaceStore = defineStore('chatRoomWorkspace', {
  state: () => ({
    sidebarLoading: false,
    sidebarError: null as string | null,
    directAll: [] as ChtDirectConversationVm[],
    topicsAll: [] as ChtTopicRoomVm[],
    groupsAll: [] as ChtGroupChatVm[],
    selection: null as ChatRoomSelection | null,
    messagesLoading: false,
    messagesError: null as string | null,
    messages: [] as ChtMessageVm[],
    sendBusy: false,
  }),

  getters: {
    /** JWT: DG `cht_messages.authorPersonId` ve GetCurrentUserId ile aynı olmalı. */
    myPersonId(): string {
      const auth = useAuthStore();
      return String(auth.userInfo?.sub ?? '').trim();
    },

    /**
     * DM `cht_direct_conversations.participant*`: JWT `mng_person_id` veya aynı değerin profildeki `User.id`.
     * `sub` burada kullanılmaz (mesaj `authorPersonId` için `myPersonId` ayrıdır).
     */
    myDmParticipantId(): string {
      const auth = useAuthStore();
      const fromToken = String(auth.userInfo?.mng_person_id ?? '').trim();
      if (fromToken) return fromToken;
      const sub = this.myPersonId;
      if (!sub) return '';
      try {
        const u = useUserStore().getUserById(sub);
        const id = u?.id?.trim();
        if (id) return id;
      } catch {
        /* ignore */
      }
      return '';
    },

    directForMe(state): ChtDirectConversationVm[] {
      const auth = useAuthStore();
      const me = String(auth.userInfo?.sub ?? '').trim();
      if (!me) return [];
      return state.directAll.filter(
        (r) =>
          participantMatchesAnyAlias(r.participantAId, me) ||
          participantMatchesAnyAlias(r.participantBId, me)
      );
    },

    topicRoots(state): ChtTopicRoomVm[] {
      return state.topicsAll.filter((t) => !t.parentTopicRoomId && !t.archived);
    },

    topicBranches(state): ChtTopicRoomVm[] {
      return state.topicsAll.filter((t) => !!t.parentTopicRoomId && !t.archived);
    },
  },

  actions: {
    directTitle(row: ChtDirectConversationVm): string {
      const me = this.myPersonId;
      const otherId = directConversationPeerStoredId(row, me);
      const label = displayNameForStoredPersonId(otherId);
      return label.length > 48 ? `${label.slice(0, 46)}…` : label || '—';
    },

    groupTitle(row: ChtGroupChatVm): string {
      const n = row.displayNameCache?.trim();
      if (n) return n;
      const id = row.keycloakGroupId;
      return id.length > 24 ? `${id.slice(0, 22)}…` : id || '—';
    },

    selectRoom(sel: ChatRoomSelection) {
      this.selection = { ...sel };
      if (sel.roomKind === 'direct') {
        const me = this.myPersonId;
        const row = me ? this.directForMe.find((r) => r.dataId === sel.roomRecordId) : undefined;
        if (row) {
          void this.prefetchUserProfilesForChat([directConversationPeerStoredId(row, me)]);
        }
      }
      void this.loadMessages();
    },

    clearSelection() {
      this.selection = null;
      this.messages = [];
      this.messagesError = null;
    },

    /**
     * Birebir oda: varsa mevcut kaydı döner; yoksa DG POST ile oluşturur ve listeye ekler.
     * `participant*`: her iki taraf da Keeper kişi Mongo id (`mng_person_id` ile aynı); mesajda `authorPersonId` = JWT `sub`.
     * @param otherPersonId Karşı taraf (Keeper kullanıcı id / listeden seçim; gerekirse API ile çözümlenir)
     */
    async ensureDirectConversation(otherPersonId: string): Promise<ChtDirectConversationVm> {
      const userStore = useUserStore();
      const sub = this.myPersonId;
      if (sub && !userStore.getUserById(sub)) {
        try {
          await userStore.fetchUserById(sub);
        } catch {
          /* Profil yoksa myDmParticipantId boş kalabilir */
        }
      }
      let me = this.myDmParticipantId;
      if (!me && sub) {
        try {
          await userStore.fetchUserById(sub);
          me = this.myDmParticipantId;
        } catch {
          /* ignore */
        }
      }
      const otherRaw = String(otherPersonId ?? '').trim();
      let other = normalizePeerIdForDirectChat(otherRaw);
      if (!other && otherRaw) {
        try {
          await userStore.fetchUserById(otherRaw);
          other = normalizePeerIdForDirectChat(otherRaw);
        } catch {
          /* ignore */
        }
      }
      if (!this.myPersonId) throw chatErr('NO_SESSION');
      if (!me) throw chatErr('NO_MY_MNG_PERSON_ID');
      if (!otherRaw) throw chatErr('EMPTY_PEER');
      if (!other) throw chatErr('NO_PEER_MNG_PERSON_ID');
      if (other.toLowerCase() === me.toLowerCase()) throw chatErr('SELF_DM');

      const existing = this.directForMe.find((r) => directConversationLinksPair(r, me, other));
      if (existing) return existing;

      const { canonicalKey, participantAId, participantBId } = orderedDirectParticipants(me, other);
      const payload = {
        canonicalKey,
        participantAId,
        participantBId,
        createdAt: new Date().toISOString(),
      };

      try {
        const created = await fetchFromDataGateway(`/api/v1/data/${DS_DIRECT}`, 'POST', payload);
        const row = mapDirect(unwrapDataGatewayWriteResponse(created));
        if (!row.dataId) throw new Error('Create response missing __dataId');
        if (!this.directAll.some((x) => x.dataId === row.dataId)) {
          this.directAll = [row, ...this.directAll];
        }
        return row;
      } catch (e: unknown) {
        await this.loadSidebar();
        const again = this.directForMe.find((r) => directConversationLinksPair(r, me, other));
        if (again) return again;
        throw e;
      }
    },

    async loadSidebar(options?: { silent?: boolean }) {
      const silent = Boolean(options?.silent);
      if (!silent) this.sidebarLoading = true;
      this.sidebarError = null;
      try {
        const [dRes, tRes, gRes] = await Promise.all([
          fetchFromDataGateway(`/api/v1/data/${DS_DIRECT}?limit=500&sort=-lastMessageAt`),
          fetchFromDataGateway(`/api/v1/data/${DS_TOPICS}?limit=500&sort=title`),
          fetchFromDataGateway(`/api/v1/data/${DS_GROUPS}?limit=500&sort=-createdAt`),
        ]);
        this.directAll = parseArrayResponse(dRes).map((r) => mapDirect(r));
        this.topicsAll = parseArrayResponse(tRes).map((r) => mapTopic(r));
        const dgGroups = parseArrayResponse(gRes).map((r) => mapGroup(r));
        this.groupsAll = await this.resolveSessionGroupChats(dgGroups);
        void this.prefetchDirectPeerProfiles();
      } catch (e: unknown) {
        this.sidebarError = e instanceof Error ? e.message : String(e);
        this.directAll = [];
        this.topicsAll = [];
        this.groupsAll = [];
        console.error('[chatRoomWorkspace] loadSidebar', e);
      } finally {
        if (!silent) this.sidebarLoading = false;
      }
    },

    /**
     * Oturumdaki kullanıcının üye olduğu Keycloak grupları: JWT `user_groups` + Keeper kullanıcı `groups[]`.
     * DG `cht_group_chats` ile eşleştirir; satır yoksa oluşturmayı dener (tenant’ta tek `cht_group_chats` / grup).
     */
    async resolveSessionGroupChats(dgFromApi: ChtGroupChatVm[]): Promise<ChtGroupChatVm[]> {
      const auth = useAuthStore();
      const jwtGroups = uniqueTrimmedPreserveOrder(auth.userGroups ?? []);
      const userStore = useUserStore();
      const sub = String(auth.userInfo?.sub ?? '').trim();
      const keeperUser = sub ? userStore.getUserById(sub) : undefined;
      const keeperNames = uniqueTrimmedPreserveOrder(keeperUser?.groups ?? []);

      if (!jwtGroups.length && !keeperNames.length) return [];

      const kidMap = new Map<string, ChtGroupChatVm>();
      const nameMap = new Map<string, ChtGroupChatVm>();
      for (const r of dgFromApi) {
        const kid = String(r.keycloakGroupId ?? '').trim();
        if (kid) kidMap.set(kid.toLowerCase(), r);
        const dn = String(r.displayNameCache ?? '').trim();
        if (dn) nameMap.set(dn.toLowerCase(), r);
      }

      const seen = new Set<string>();
      const out: ChtGroupChatVm[] = [];
      const push = (row: ChtGroupChatVm | null | undefined) => {
        if (!row?.dataId || seen.has(row.dataId)) return;
        seen.add(row.dataId);
        out.push(row);
      };

      const findLocal = (token: string): ChtGroupChatVm | undefined => {
        const k = token.trim().toLowerCase();
        return kidMap.get(k) ?? nameMap.get(k);
      };

      const fetchByKeycloakFilter = async (raw: string): Promise<ChtGroupChatVm | null> => {
        const t = raw.trim();
        if (!t) return null;
        try {
          const filter = `keycloakGroupId:eq:${t}`;
          const res = await fetchFromDataGateway(
            `/api/v1/data/${DS_GROUPS}?filter=${encodeURIComponent(filter)}&limit=10`
          );
          const arr = parseArrayResponse(res);
          const first = arr[0];
          if (!first || typeof first !== 'object') return null;
          const row = mapGroup(first as any);
          if (row.keycloakGroupId) kidMap.set(row.keycloakGroupId.trim().toLowerCase(), row);
          const dn = String(row.displayNameCache ?? '').trim();
          if (dn) nameMap.set(dn.toLowerCase(), row);
          return row;
        } catch {
          return null;
        }
      };

      const ensureRow = async (token: string): Promise<ChtGroupChatVm | null> => {
        const t = token.trim();
        if (!t) return null;
        const loc = findLocal(t);
        if (loc) return loc;
        const byQuery = await fetchByKeycloakFilter(t);
        if (byQuery) return byQuery;
        try {
          const created = await fetchFromDataGateway(`/api/v1/data/${DS_GROUPS}`, 'POST', {
            keycloakGroupId: t,
            displayNameCache: humanizeGroupDisplayToken(t),
            createdAt: new Date().toISOString(),
          });
          const row = mapGroup(unwrapDataGatewayWriteResponse(created));
          if (row.keycloakGroupId) kidMap.set(row.keycloakGroupId.trim().toLowerCase(), row);
          const dn = String(row.displayNameCache ?? '').trim();
          if (dn) nameMap.set(dn.toLowerCase(), row);
          return row;
        } catch {
          return null;
        }
      };

      for (const token of jwtGroups) {
        let row = findLocal(token);
        if (!row) row = await ensureRow(token);
        push(row);
      }

      for (const nm of keeperNames) {
        const k = nm.toLowerCase();
        let row =
          nameMap.get(k) ??
          dgFromApi.find((r) => (r.displayNameCache ?? '').trim().toLowerCase() === k);
        if (!row) row = kidMap.get(k);
        if (!row) row = await fetchByKeycloakFilter(nm);
        push(row);
      }

      return out;
    },

    /** Keeper `/user/{id}` ile eksik profilleri doldurur; `getUserById` / `displayNameForStoredPersonId` için `users` güncellenir. */
    async prefetchUserProfilesForChat(ids: Iterable<string>) {
      const userStore = useUserStore();
      const uniq = [...new Set(Array.from(ids, (x) => String(x ?? '').trim()).filter(Boolean))];
      const pending = uniq.filter((id) => !userStore.getUserById(id));
      await Promise.all(pending.map((id) => userStore.fetchUserById(id).catch(() => {})));
    },

    async prefetchDirectPeerProfiles() {
      const me = this.myPersonId;
      if (!me) return;
      const peers: string[] = [];
      for (const r of this.directForMe) {
        peers.push(directConversationPeerStoredId(r, me));
      }
      await this.prefetchUserProfilesForChat(peers);
    },

    /** Hub kesintisi / sekme görünür: DG ile geçmişi yenile. */
    async refreshAfterTransportGap(options?: { silent?: boolean }) {
      const silent = Boolean(options?.silent);
      await this.loadSidebar({ silent });
      if (this.selection) await this.loadMessages({ silent });
    },

    scheduleSidebarRefreshSilent() {
      if (sidebarHubRefreshTimer) clearTimeout(sidebarHubRefreshTimer);
      sidebarHubRefreshTimer = setTimeout(() => {
        sidebarHubRefreshTimer = null;
        void this.loadSidebar({ silent: true });
      }, SIDEBAR_HUB_DEBOUNCE_MS);
    },

    async loadMessages(options?: { silent?: boolean }) {
      const sel = this.selection;
      if (!sel) {
        this.messages = [];
        return;
      }
      const silent = Boolean(options?.silent);
      if (!silent) this.messagesLoading = true;
      this.messagesError = null;
      try {
        const enc = encodeURIComponent(sel.roomRecordId);
        const filter = `roomKind:eq:${sel.roomKind},roomRecordId:eq:${enc}`;
        // expand=false: şemada authorPersonId `persons` ise lookup JWT sub ile eşleşmeyebilir ve alan JSON'dan düşer.
        const url = `/api/v1/data/${DS_MESSAGES}?filter=${filter}&sort=-createdAt&limit=200&expand=false`;
        const res = await fetchFromDataGateway(url);
        const raw = parseArrayResponse(res).map((r) => mapMessage(r as any));
        this.messages = raw.reverse();
        void this.prefetchUserProfilesForChat(raw.map((m) => m.authorPersonId));
      } catch (e: unknown) {
        this.messagesError = e instanceof Error ? e.message : String(e);
        this.messages = [];
        console.error('[chatRoomWorkspace] loadMessages', e);
      } finally {
        if (!silent) this.messagesLoading = false;
      }
    },

    /**
     * DM satırı listede yoksa (ör. karşı taraf oluşturdu) DG GET ile ekler.
     * Yalnızca oturum kullanıcısı participantA/B ise eklenir.
     */
    async ensureDirectConversationVisible(roomRecordId: string) {
      const id = String(roomRecordId ?? '').trim();
      if (!id) return;
      if (this.directAll.some((x) => x.dataId === id)) return;
      const me = this.myPersonId;
      if (!me) return;
      try {
        const res = await fetchFromDataGateway(
          `/api/v1/data/${DS_DIRECT}/${encodeURIComponent(id)}`
        );
        const arr = parseArrayResponse(res);
        const raw = arr[0];
        if (!raw || typeof raw !== 'object') return;
        const row = mapDirect(raw as any);
        if (!row.dataId) return;
        const imIn =
          participantMatchesAnyAlias(row.participantAId, me) ||
          participantMatchesAnyAlias(row.participantBId, me);
        if (!imIn) return;
        if (!this.directAll.some((x) => x.dataId === row.dataId)) {
          this.directAll = [row, ...this.directAll];
        }
      } catch (e) {
        console.warn('[chatRoomWorkspace] ensureDirectConversationVisible', e);
        void this.loadSidebar({ silent: true });
      }
    },

    /**
     * Başka bir odadayken / seçim yokken gelen DM: listeyi doldur; seçim yoksa odayı aç (mesajlar DG'den yüklenir).
     */
    async handleIncomingDirectWhenNotViewing(row: ChtMessageVm, currentSel: ChatRoomSelection | null) {
      if (row.roomKind !== 'direct' || !row.roomRecordId) return;
      await this.ensureDirectConversationVisible(row.roomRecordId);
      if (messageAuthorIsCurrentUser(row.authorPersonId, [this.myDmParticipantId])) return;
      if (currentSel) {
        this.scheduleSidebarRefreshSilent();
        return;
      }
      const conv = this.directAll.find((c) => c.dataId === row.roomRecordId);
      if (!conv) return;
      this.selectRoom({
        roomKind: 'direct',
        roomRecordId: conv.dataId,
        title: this.directTitle(conv),
        subtitle: undefined,
      });
    },

    async sendMessage(body: string) {
      const sel = this.selection;
      const me = this.myPersonId;
      if (!sel || !me || !body.trim()) return;
      this.sendBusy = true;
      this.messagesError = null;
      try {
        const payload = {
          roomKind: sel.roomKind,
          roomRecordId: sel.roomRecordId,
          body: body.trim(),
          authorPersonId: me,
          createdAt: new Date().toISOString(),
        };
        const created = await fetchFromDataGateway(`/api/v1/data/${DS_MESSAGES}`, 'POST', payload);
        const row = mapMessage(unwrapDataGatewayWriteResponse(created));
        if (row.dataId && !this.messages.some((m) => m.dataId === row.dataId)) {
          this.messages = [...this.messages, row];
        } else if (row.dataId) {
          this.messages = this.messages.map((m) => (m.dataId === row.dataId ? row : m));
        } else {
          await this.loadMessages({ silent: true });
        }
      } catch (e: unknown) {
        this.messagesError = e instanceof Error ? e.message : String(e);
        throw e;
      } finally {
        this.sendBusy = false;
      }
    },

    /**
     * Hub ReceiveMessage: seçili odada anında birleştir; diğer odalarda / silmede sol listeyi debounce ile yenile.
     * Çevrimdışıyken biriken mesajlar: Hub yeniden bağlanınca veya sekme görünür olunca `refreshAfterTransportGap`.
     */
    onHubChtMessage(data: HubMessage) {
      if (!isChtMessagesHubPayload(data)) return;
      const norm = normalizeHubMessageForChat(data);
      const m = norm.message as Record<string, unknown>;
      const evt = eventTypeLower(m);
      const rk = routingKeyLower(norm.routingKey);
      const sel = this.selection;

      if (import.meta.dev) {
        const ds = hubPayloadDatasetName(m);
        console.debug('[CHAT_ROOM_HUB] cht_messages event', {
          routingKey: norm.routingKey,
          evt,
          datasetName: ds,
          dataId: m.dataId ?? m.DataId,
        });
      }

      if (evt.includes('deleted') || rk.endsWith('.datadeletedevent')) {
        const inner = (m.data ?? m.Data) as Record<string, unknown> | undefined;
        let id = inner ? normId(inner.__dataId ?? inner.DataId ?? inner.dataId) : '';
        if (!id) id = normId(m.dataId ?? m.DataId);
        if (id) {
          let innerRoomKind = '';
          let innerRoomId = '';
          if (inner && typeof inner === 'object') {
            innerRoomKind = String((inner as any).roomKind ?? (inner as any).RoomKind ?? '');
            innerRoomId = String((inner as any).roomRecordId ?? (inner as any).RoomRecordId ?? '');
          }
          const forSelection =
            sel &&
            innerRoomKind &&
            innerRoomId &&
            sel.roomKind === innerRoomKind &&
            sel.roomRecordId === innerRoomId;
          if (forSelection) this.messages = this.messages.filter((x) => x.dataId !== id);
        }
        this.scheduleSidebarRefreshSilent();
        return;
      }

      const isCreateOrUpdate =
        evt.includes('created') ||
        evt.includes('updated') ||
        rk.endsWith('.datacreatedevent') ||
        rk.endsWith('.dataupdatedevent');
      if (!isCreateOrUpdate) {
        if (import.meta.dev) {
          console.debug('[CHAT_ROOM_HUB] skip: not create/update/delete', { evt, rk });
        }
        return;
      }

      let inner = unwrapHubEventData(m);
      if (!inner || typeof inner !== 'object') {
        const rkHasCreate =
          rk.endsWith('.datacreatedevent') ||
          rk.endsWith('.dataupdatedevent') ||
          evt.includes('created') ||
          evt.includes('updated');
        if (
          rkHasCreate &&
          (typeof m.roomKind === 'string' ||
            typeof m.RoomKind === 'string' ||
            typeof m.roomRecordId === 'string' ||
            typeof m.RoomRecordId === 'string')
        ) {
          inner = m as Record<string, unknown>;
        } else return;
      }

      const row = mapMessage(inner);
      if (!row.dataId || !row.roomKind || !row.roomRecordId) return;

      if (row.roomKind === 'direct' && row.roomRecordId) {
        void this.ensureDirectConversationVisible(row.roomRecordId);
      }

      const viewing =
        !!sel && sel.roomKind === row.roomKind && sel.roomRecordId === row.roomRecordId;

      if (!viewing) {
        if (import.meta.dev) {
          console.debug('[CHAT_ROOM_HUB] other room or no selection → sidebar + DM ingest', {
            selection: sel ? { roomKind: sel.roomKind, roomRecordId: sel.roomRecordId } : null,
            eventRoom: { roomKind: row.roomKind, roomRecordId: row.roomRecordId },
          });
        }
        if (row.roomKind === 'direct') {
          void this.handleIncomingDirectWhenNotViewing(row, sel);
        } else {
          this.scheduleSidebarRefreshSilent();
        }
        return;
      }

      if (import.meta.dev) {
        console.debug('[CHAT_ROOM_HUB] merge into open thread', {
          dataId: row.dataId,
          authorPersonId: row.authorPersonId,
        });
      }

      const idx = this.messages.findIndex((x) => x.dataId === row.dataId);
      if (idx >= 0) {
        this.messages = [...this.messages.slice(0, idx), row, ...this.messages.slice(idx + 1)];
      } else {
        // Açık odada yeni satır: routing key / type varyantlarında da anında göster
        this.messages = [...this.messages, row].sort((a, b) => {
          const ta = new Date(a.createdAt).getTime();
          const tb = new Date(b.createdAt).getTime();
          return (Number.isNaN(ta) ? 0 : ta) - (Number.isNaN(tb) ? 0 : tb);
        });
      }
      void this.prefetchUserProfilesForChat([row.authorPersonId]);
      this.scheduleSidebarRefreshSilent();
    },
  },
});
