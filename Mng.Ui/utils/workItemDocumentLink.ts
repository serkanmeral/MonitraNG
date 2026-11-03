import { fetchFromDataGateway } from '@/services/apiService';
import { diCreateResourceLink } from '@/services/documentIntelligenceService';

export async function findWorkItemIdByKey(key: string): Promise<string | null> {
  const trimmed = key.trim();
  if (!trimmed) return null;
  const res = await fetchFromDataGateway(
    `/api/v1/data/op_work_items?filter=key:eq:${encodeURIComponent(trimmed)}&limit=1&expand=false`
  );
  const rows = Array.isArray(res) ? res : res?.items ?? res?.data ?? [];
  const id = rows[0]?.__dataId ?? rows[0]?.dataId;
  return id != null && String(id).trim() ? String(id) : null;
}

/** Üretilen tutanağı, o turun iş kalemine çıktı olarak bağlar. Bağ zaten varsa da tamam sayılır. */
export async function linkResourceToWorkItemKey(resourceId: string, workItemKey: string): Promise<boolean> {
  const targetId = await findWorkItemIdByKey(workItemKey);
  const id = resourceId.trim();
  if (!targetId || !id) return false;
  try {
    await diCreateResourceLink({
      resourceId: id,
      targetModule: 'operationCore',
      targetType: 'workItem',
      targetId,
      relationType: 'output',
    });
    return true;
  } catch {
    return false;
  }
}
