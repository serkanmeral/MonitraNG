import { diGetTemplateCategoryTree, diListTemplates } from '@/services/documentIntelligenceService';
import type { DiTemplateSummary, DiTreeNode } from '@/types/apps/documentIntelligence';
import {
  ODAK_LINE_CONTEXT_TYPE,
  ODAK_LINE_DOCUMENT_PROFILE_CODES,
  profileCodeForTemplate,
} from '@/utils/odakSiparisLineDocumentService';

export const ODAK_LINE_DOCUMENT_CATEGORY_PATHS = [
  ['Kalite Belgeleri', 'CoC / Uygunluk Sertifikaları'],
  ['Operasyon Belgeleri', 'Kalem Activity / Yaşam Döngüsü'],
] as const;

function findCategoryIdByPath(nodes: DiTreeNode[], path: readonly string[], depth = 0): string | null {
  if (depth >= path.length) return null;
  const targetName = path[depth]!;
  for (const node of nodes) {
    if (node.name !== targetName) continue;
    if (depth === path.length - 1) return node.id;
    const nested = findCategoryIdByPath(node.children ?? [], path, depth + 1);
    if (nested) return nested;
  }
  return null;
}

export function isEligibleOdakLineDocumentTemplate(template: DiTemplateSummary): boolean {
  if (template.status !== 'published') return false;
  if (template.primaryContextType && template.primaryContextType !== ODAK_LINE_CONTEXT_TYPE) return false;
  const profile = profileCodeForTemplate(template.generationProfile);
  if (template.generationProfile && !profile) return false;
  if (!template.generationProfile) return false;
  return Boolean(template.code?.trim());
}

export async function fetchOdakLineDocumentTemplates(): Promise<DiTemplateSummary[]> {
  const tree = await diGetTemplateCategoryTree();
  const byId = new Map<string, DiTemplateSummary>();

  for (const path of ODAK_LINE_DOCUMENT_CATEGORY_PATHS) {
    const categoryId = findCategoryIdByPath(tree, path);
    if (!categoryId) continue;
    const { items } = await diListTemplates(categoryId);
    for (const tpl of items) {
      if (!isEligibleOdakLineDocumentTemplate(tpl)) continue;
      if (tpl.id) byId.set(tpl.id, tpl);
      else if (tpl.code) byId.set(tpl.code, tpl);
    }
  }

  return [...byId.values()].sort((a, b) => {
    const pa = profileCodeForTemplate(a.generationProfile) ?? '';
    const pb = profileCodeForTemplate(b.generationProfile) ?? '';
    const pc = pa.localeCompare(pb);
    if (pc !== 0) return pc;
    return (a.name ?? '').localeCompare(b.name ?? '', 'tr');
  });
}

export function isKnownLineDocumentProfile(profile?: string | null): boolean {
  const code = profile?.trim();
  if (!code) return false;
  return (ODAK_LINE_DOCUMENT_PROFILE_CODES as readonly string[]).includes(code);
}
