import { computed, type MaybeRefOrGetter, toValue } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

export interface OcBreadcrumbSegment {
  id: string;
  name: string;
}

export interface OcBreadcrumbTail {
  text: string;
  href?: string;
  disabled?: boolean;
}

export interface OcBreadcrumbItem {
  text: string;
  href?: string;
  disabled?: boolean;
}

export interface UseOperationCoreBreadcrumbsContext {
  workspace?: MaybeRefOrGetter<OcBreadcrumbSegment | null | undefined>;
  board?: MaybeRefOrGetter<OcBreadcrumbSegment | null | undefined>;
  workItem?: MaybeRefOrGetter<{ id: string; key: string } | null | undefined>;
  tail?: MaybeRefOrGetter<OcBreadcrumbTail | null | undefined>;
  /** Explorer gibi sabit ara segment (örn. Çalışma alanı) */
  showWorkspaceExplorer?: MaybeRefOrGetter<boolean>;
}

function shortId(id: string): string {
  if (!id) return '…';
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

function segmentLabel(name: string | undefined, id: string | undefined): string {
  if (name && name.trim()) return name.trim();
  if (id) return shortId(id);
  return '…';
}

function translateOr(key: string, fallback: string, t: (key: string) => string): string {
  const value = t(key);
  return value && value !== key ? value : fallback;
}

export function useOperationCoreBreadcrumbs(ctx: UseOperationCoreBreadcrumbsContext = {}) {
  const { t } = useAppI18n();

  const breadcrumbs = computed((): OcBreadcrumbItem[] => {
    const items: OcBreadcrumbItem[] = [
      {
        text: translateOr('welcome.breadcrumbs.home', 'Ana sayfa', t),
        href: '/',
        disabled: false,
      },
      {
        text: t('operationCore.breadcrumbRoot'),
        href: '/apps/operation-core/workspace',
        disabled: false,
      },
    ];

    const workspace = toValue(ctx.workspace);
    const board = toValue(ctx.board);
    const workItem = toValue(ctx.workItem);
    const tail = toValue(ctx.tail);
    const showExplorer = toValue(ctx.showWorkspaceExplorer) ?? false;

    if (showExplorer && !workspace && !board && !workItem && !tail) {
      items.push({
        text: t('operationCore.breadcrumbWorkspace'),
        disabled: true,
        href: '#',
      });
      return items;
    }

    if (workspace?.id) {
      items.push({
        text: segmentLabel(workspace.name, workspace.id),
        href: `/apps/operation-core/workspace?workspaceId=${encodeURIComponent(workspace.id)}`,
        disabled: false,
      });
    }

    if (board?.id) {
      items.push({
        text: segmentLabel(board.name, board.id),
        href: `/apps/operation-core/boards/${encodeURIComponent(board.id)}`,
        disabled: false,
      });
    }

    if (workItem?.key) {
      items.push({
        text: workItem.key,
        href: `/apps/operation-core/work-items/${encodeURIComponent(workItem.id)}/profile`,
        disabled: true,
      });
    }

    if (tail?.text) {
      items.push({
        text: tail.text,
        href: tail.href ?? '#',
        disabled: tail.disabled ?? true,
      });
    }

    if (items.length > 1) {
      items[items.length - 1].disabled = true;
    }

    return items;
  });

  return { breadcrumbs };
}
