import {
  OC_DATASETS,
  ocCreateRecordId,
  ocDelete,
  ocListDataset,
  ocUpdate,
  resolveRelationId,
} from '@/services/operationCoreService';
import type { OpTag } from '@/types/apps/operationCore';

export function mapOpTag(raw: Record<string, unknown>): OpTag {
  return {
    __dataId: String(raw.__dataId ?? raw.dataId ?? ''),
    name: String(raw.name ?? raw.Name ?? ''),
    color:
      raw.color != null
        ? String(raw.color)
        : raw.Color != null
          ? String(raw.Color)
          : null,
    description:
      raw.description != null
        ? String(raw.description)
        : raw.Description != null
          ? String(raw.Description)
          : null,
    workspaceId: resolveRelationId(raw.workspaceId ?? raw.WorkspaceId) ?? '',
  };
}

/** Bir workspace'e ait etiketleri (ada göre) listeler. */
export async function ocListTagsForWorkspace(workspaceId: string): Promise<OpTag[]> {
  if (!workspaceId) return [];
  const rows = await ocListDataset(OC_DATASETS.tags, {
    filter: `workspaceId:eq:${workspaceId}`,
    sort: 'name:asc',
    limit: 500,
  });
  return rows
    .map((r) => mapOpTag(r as Record<string, unknown>))
    .filter((tag) => tag.__dataId && tag.name && tag.workspaceId === workspaceId);
}

export async function ocCreateTag(payload: Record<string, unknown>): Promise<string | null> {
  return ocCreateRecordId(OC_DATASETS.tags, payload);
}

export async function ocUpdateTag(tagId: string, payload: Record<string, unknown>) {
  await ocUpdate(OC_DATASETS.tags, tagId, payload);
}

export async function ocDeleteTag(tagId: string) {
  await ocDelete(OC_DATASETS.tags, tagId);
}
