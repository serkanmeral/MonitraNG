import { fetchFromDataGateway } from '@/services/apiService';
import { ocCreate, ocDelete, ocListDatasetPage, ocUpdate } from '@/services/operationCoreService';
import { ODAK_SIPARIS_CONFIG, type OdakCustomerContactRow } from '@/utils/odakSiparisConfig';
import { packageDataId } from '@/utils/odakSiparisService';

export type OdakCustomerContactDialogMode = 'create' | 'edit';

function resolveRelationId(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return String(o.__dataId ?? o.dataId ?? o.id ?? '').trim();
  }
  return String(raw).trim();
}

export function contactDataId(row: OdakCustomerContactRow | Record<string, unknown>): string {
  return packageDataId(row);
}

export function buildContactsByParentCustomerFilter(parentCustomerId: string): string {
  return `parentCustomerId:eq:${parentCustomerId}`;
}

export function contactBelongsToCustomer(row: Record<string, unknown>, customerId: string): boolean {
  if (!customerId) return false;
  return resolveRelationId(row.parentCustomerId) === customerId;
}

export function formatContactSelectLabel(row: OdakCustomerContactRow | Record<string, unknown>): string {
  const c = row as OdakCustomerContactRow;
  const ad = c.ad?.trim() || '—';
  const email = c.email?.trim();
  const gorev = c.gorevUnvani?.trim();
  const parts: string[] = [ad];
  if (gorev) parts.push(gorev);
  if (email) parts.push(email);
  let label = parts.join(' · ');
  if (c.birincilKisi) label = `★ ${label}`;
  return label;
}

export function customerContactIdFromRow(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object') return contactDataId(raw as Record<string, unknown>);
  return String(raw).trim();
}

export function customerContactLabelFromRow(raw: unknown): string {
  if (raw == null) return '—';
  if (typeof raw === 'object') return formatContactSelectLabel(raw as OdakCustomerContactRow);
  return String(raw) || '—';
}

export interface OdakCustomerContactFormModel {
  ad: string;
  email: string;
  telefon: string;
  gorevUnvani: string;
  birincilKisi: boolean;
  aktif: boolean;
}

export function emptyCustomerContactFormModel(
  partial?: Partial<OdakCustomerContactFormModel>
): OdakCustomerContactFormModel {
  return {
    ad: partial?.ad ?? '',
    email: partial?.email ?? '',
    telefon: partial?.telefon ?? '',
    gorevUnvani: partial?.gorevUnvani ?? '',
    birincilKisi: partial?.birincilKisi ?? false,
    aktif: partial?.aktif ?? true,
  };
}

export function contactRowToFormModel(row: OdakCustomerContactRow): OdakCustomerContactFormModel {
  return emptyCustomerContactFormModel({
    ad: row.ad ?? '',
    email: row.email ?? '',
    telefon: row.telefon ?? '',
    gorevUnvani: row.gorevUnvani ?? '',
    birincilKisi: Boolean(row.birincilKisi),
    aktif: row.aktif !== false,
  });
}

export function formModelToContactPayload(
  form: OdakCustomerContactFormModel,
  customerId: string
): Record<string, unknown> {
  return {
    parentCustomerId: customerId,
    ad: form.ad.trim(),
    email: form.email.trim(),
    telefon: form.telefon.trim() || null,
    gorevUnvani: form.gorevUnvani.trim() || null,
    birincilKisi: form.birincilKisi,
    aktif: form.aktif,
  };
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function validateCustomerContactForm(form: OdakCustomerContactFormModel): string | null {
  if (!form.ad.trim()) return 'adRequired';
  if (!form.email.trim()) return 'emailRequired';
  if (!EMAIL_PATTERN.test(form.email.trim())) return 'emailInvalid';
  return null;
}

export async function fetchOdakCustomerContactById(contactId: string): Promise<OdakCustomerContactRow | null> {
  if (!contactId) return null;
  const url = `/api/v1/data/${encodeURIComponent(ODAK_SIPARIS_CONFIG.customerContactsDataset)}/${encodeURIComponent(contactId)}`;
  const raw = await fetchFromDataGateway(url, 'GET');
  const item = Array.isArray(raw) ? raw[0] : raw;
  if (!item || typeof item !== 'object') return null;
  return item as OdakCustomerContactRow;
}

export async function listContactsForCustomer(customerId: string): Promise<OdakCustomerContactRow[]> {
  if (!customerId) return [];
  const filter = buildContactsByParentCustomerFilter(customerId);
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.customerContactsDataset, {
    filter,
    sort: 'birincilKisi:desc,ad:asc',
    limit: 200,
  });
  return ((resp.items ?? []) as OdakCustomerContactRow[]).filter((row) =>
    contactBelongsToCustomer(row as Record<string, unknown>, customerId)
  );
}

export async function fetchContactSelectOptions(
  customerId: string,
  activeOnly = true
): Promise<{ value: string; title: string }[]> {
  const rows = await listContactsForCustomer(customerId);
  return rows
    .filter((row) => !activeOnly || row.aktif !== false)
    .map((row) => {
      const id = contactDataId(row);
      if (!id) return null;
      return { value: id, title: formatContactSelectLabel(row) };
    })
    .filter((x): x is { value: string; title: string } => !!x);
}

async function clearOtherPrimaryContacts(customerId: string, exceptContactId?: string): Promise<void> {
  const rows = await listContactsForCustomer(customerId);
  await Promise.all(
    rows
      .filter((row) => row.birincilKisi && contactDataId(row) !== exceptContactId)
      .map(async (row) => {
        const id = contactDataId(row);
        if (!id) return;
        await ocUpdate(ODAK_SIPARIS_CONFIG.customerContactsDataset, id, { birincilKisi: false });
      })
  );
}

export async function createOdakCustomerContact(
  customerId: string,
  form: OdakCustomerContactFormModel
): Promise<string | null> {
  if (form.birincilKisi) {
    await clearOtherPrimaryContacts(customerId);
  }
  const body = formModelToContactPayload(form, customerId);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.customerContactsDataset, body);
  return contactDataId(created as Record<string, unknown>) || null;
}

export async function updateOdakCustomerContact(
  contactId: string,
  customerId: string,
  form: OdakCustomerContactFormModel
): Promise<void> {
  if (form.birincilKisi) {
    await clearOtherPrimaryContacts(customerId, contactId);
  }
  const body = formModelToContactPayload(form, customerId);
  await ocUpdate(ODAK_SIPARIS_CONFIG.customerContactsDataset, contactId, body);
}

export async function deleteOdakCustomerContact(contactId: string): Promise<void> {
  if (!contactId) return;
  await ocDelete(ODAK_SIPARIS_CONFIG.customerContactsDataset, contactId);
}

let legacyContactLabelCache: Record<string, string> | null = null;
let legacyContactLabelLoadPromise: Promise<Record<string, string>> | null = null;

/** legacyContactId -> goruntulenecek etiket (odak_musteri_kisileri uzerinden). */
export async function fetchLegacyCustomerContactLabelMap(
  legacyContactIds: string[] = []
): Promise<Record<string, string>> {
  const requested = new Set(legacyContactIds.map((id) => String(id ?? '').trim()).filter(Boolean));

  if (!legacyContactLabelLoadPromise) {
    legacyContactLabelLoadPromise = (async () => {
      const map: Record<string, string> = {};
      let skip = 0;
      const limit = 500;
      while (true) {
        const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.customerContactsDataset, {
          skip,
          limit,
        });
        const items = (resp.items ?? []) as OdakCustomerContactRow[];
        for (const row of items) {
          const legacyId = String(row.legacyContactId ?? '').trim();
          if (legacyId) map[legacyId] = formatContactSelectLabel(row);
        }
        if (items.length < limit) break;
        skip += limit;
      }
      legacyContactLabelCache = map;
      return map;
    })();
  }

  const fullMap = await legacyContactLabelLoadPromise;
  if (!requested.size) return { ...fullMap };

  const filtered: Record<string, string> = {};
  for (const id of requested) {
    if (fullMap[id]) filtered[id] = fullMap[id];
  }
  return filtered;
}

export function invalidateLegacyCustomerContactLabelCache(): void {
  legacyContactLabelCache = null;
  legacyContactLabelLoadPromise = null;
}
