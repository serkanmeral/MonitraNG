import type { HubMessage } from '@/stores/hub';

/**
 * SignalR / JSON bazen PascalCase döner (RoutingKey, Message, Timestamp).
 */
export function normalizeHubMessageForChat(data: HubMessage | Record<string, unknown>): HubMessage {
  const raw = data as Record<string, unknown>;
  const routingKey = String(raw.routingKey ?? raw.RoutingKey ?? '').trim();
  let message: unknown = raw.message ?? raw.Message;
  if (typeof message === 'string') {
    const t = message.trim();
    if (t.startsWith('{') || t.startsWith('[')) {
      try {
        message = JSON.parse(t) as unknown;
      } catch {
        /* ham string */
      }
    }
  }
  const ts = raw.timestamp ?? raw.Timestamp;
  let timestamp = new Date().toISOString();
  if (typeof ts === 'string' && ts.length > 0) timestamp = ts;
  else if (ts instanceof Date) timestamp = ts.toISOString();
  else if (typeof ts === 'number' && !Number.isNaN(ts)) timestamp = new Date(ts).toISOString();
  return { routingKey, message, timestamp };
}

/**
 * Hub `message` gövdesinden dataset adı (düz `datasetName` veya `DataEventDto.dataset.name`).
 */
export function hubPayloadDatasetName(message: unknown): string | null {
  if (message == null || typeof message !== 'object') return null;
  const m = message as Record<string, unknown>;
  const flat = m.datasetName ?? m.DatasetName;
  if (typeof flat === 'string' && flat.length > 0) return flat;
  const d = m.dataset ?? m.Dataset;
  if (d != null && typeof d === 'object') {
    const dr = d as Record<string, unknown>;
    const n = dr.name ?? dr.Name;
    if (typeof n === 'string' && n.length > 0) return n;
  }
  return null;
}

/** DG şema adı bazen `@cht_messages` olabilir; hub filtresi ile uyum için. */
export function isChtMessagesDatasetName(ds: string): boolean {
  const n = ds.trim().toLowerCase().replace(/^@/, '');
  return n === 'cht_messages';
}

/**
 * Per-tenant exchange routing key: `dataset.{datasetName}.{created|updated|deleted|restored}`
 * (örn. `dataset.cht_messages.created`). Gövde parse edilemese bile dataset adı buradan alınır.
 */
export function datasetNameFromDgRoutingKey(routingKey: string): string | null {
  const rk = routingKey.trim().toLowerCase();
  const m = /^dataset\.([^.]+)\.(created|updated|deleted|restored|datacreatedevent|dataupdatedevent|datadeletedevent)$/i.exec(
    rk
  );
  return m ? m[1] : null;
}

/**
 * MngDataGateway unified events → MngHub ReceiveMessage (docs: CHAT_ROOM_ROADMAP §3.2b).
 * Routing key ends with .datacreatedevent | .dataupdatedevent | .datadeletedevent (lowercase).
 */
const DgDataEventSuffixes = ['.datacreatedevent', '.dataupdatedevent', '.datadeletedevent'] as const;

function routingKeyIsDgDataEvent(routingKey: string): boolean {
  const rk = routingKey.trim().toLowerCase();
  if (DgDataEventSuffixes.some((s) => rk.endsWith(s))) return true;
  // Birleşik DG yayımları: dataset.{dataset}.{created|updated|deleted|…}
  if (
    rk.startsWith('dataset.') &&
    /\.(created|updated|deleted|restored|datacreatedevent|dataupdatedevent|datadeletedevent)$/i.test(rk)
  )
    return true;
  return false;
}

/**
 * Hub subscription filter: only DataGateway payload for dataset cht_messages.
 */
export function isChtMessagesHubPayload(data: HubMessage): boolean {
  const norm = normalizeHubMessageForChat(data);
  if (!norm.routingKey) return false;
  const m = norm.message;
  if (m == null || typeof m !== 'object') return false;
  const fromBody = hubPayloadDatasetName(m);
  const fromRk = datasetNameFromDgRoutingKey(norm.routingKey);
  const ds = (typeof fromBody === 'string' && fromBody.length > 0 ? fromBody : null) ?? fromRk;
  if (typeof ds !== 'string' || !isChtMessagesDatasetName(ds)) return false;
  return routingKeyIsDgDataEvent(norm.routingKey);
}

export type ChtMessageLiveKind = 'created' | 'updated' | 'deleted';

export interface ChtMessageLiveEntry {
  id: string;
  routingKey: string;
  kind: ChtMessageLiveKind;
  dataId: string;
  roomKind?: string;
  roomRecordId?: string;
  bodyPreview?: string;
  rawTimestamp: string;
}

/**
 * Map ReceiveMessage body to a small row for the chat-room live strip (create/update/delete).
 */
export function hubMessageToChtLiveEntry(data: HubMessage): ChtMessageLiveEntry | null {
  if (!isChtMessagesHubPayload(data)) return null;
  const norm = normalizeHubMessageForChat(data);
  const rk = norm.routingKey.toLowerCase();
  const m = norm.message as Record<string, unknown>;
  const id =
    (typeof m.id === 'string' && m.id.length > 0 ? m.id : null) ??
    `${norm.routingKey}_${norm.timestamp}`;

  const dataId = String(m.dataId ?? m.DataId ?? '');

  let kind: ChtMessageLiveKind = 'created';
  if (rk.endsWith('.dataupdatedevent') || rk.endsWith('.updated')) kind = 'updated';
  else if (rk.endsWith('.datadeletedevent') || rk.endsWith('.deleted')) kind = 'deleted';

  const payload = m.data ?? m.Data;
  let roomKind: string | undefined;
  let roomRecordId: string | undefined;
  let bodyPreview: string | undefined;

  if (payload != null && typeof payload === 'object') {
    const p = payload as Record<string, unknown>;
    if (typeof p.roomKind === 'string') roomKind = p.roomKind;
    else if (typeof p.RoomKind === 'string') roomKind = p.RoomKind;
    if (typeof p.roomRecordId === 'string') roomRecordId = p.roomRecordId;
    else if (typeof p.RoomRecordId === 'string') roomRecordId = p.RoomRecordId;
    const b = p.body ?? p.Body;
    if (typeof b === 'string' && b.length > 0) {
      bodyPreview = b.length > 120 ? `${b.slice(0, 117)}…` : b;
    }
  }

  return {
    id,
    routingKey: norm.routingKey,
    kind,
    dataId,
    roomKind,
    roomRecordId,
    bodyPreview,
    rawTimestamp: norm.timestamp,
  };
}
