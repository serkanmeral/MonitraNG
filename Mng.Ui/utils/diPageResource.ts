import { DI_RESOURCE_TYPE, type DiResource } from '@/types/apps/documentIntelligence';

/** Kök içerik alanı klasörleri (seed: Sayfalar, Dökümanlar). */
export const DI_TOP_AREA_FOLDER_NAMES = ['Sayfalar', 'Dökümanlar'] as const;

export type DiTopAreaFolderName = (typeof DI_TOP_AREA_FOLDER_NAMES)[number];

export function isDiTopAreaFolder(name: string | null | undefined): boolean {
  if (!name) return false;
  return (DI_TOP_AREA_FOLDER_NAMES as readonly string[]).includes(name);
}

/** Alan giriş sayfası: başlık «Giriş» veya `*-giris` dosya adı. */
export function findDiAreaIndexPage(resources: DiResource[]): DiResource | null {
  const pages = resources.filter((r) => r.type === DI_RESOURCE_TYPE.markdown);
  const byTitle = pages.find((p) => (p.title || p.name).trim().toLowerCase() === 'giriş');
  if (byTitle) return byTitle;
  const bySlug = pages.find((p) => {
    const n = (p.name || '').toLowerCase();
    return n.endsWith('-giris.md') || n === 'index.md' || n === 'readme.md';
  });
  return bySlug ?? null;
}

/** Sayfa (markdown) kaynağı için tutarlı ikon. */
export function diPageResourceIcon(resource: Pick<DiResource, 'type' | 'status'>): string {
  if (resource.type !== DI_RESOURCE_TYPE.markdown) return 'mdi-file-outline';
  if (resource.status === 'draft') return 'mdi-book-edit-outline';
  return 'mdi-book-open-page-variant-outline';
}

export function diPageResourceLabel(resource: DiResource): string {
  return resource.type === DI_RESOURCE_TYPE.markdown ? resource.title || resource.name : resource.name;
}
