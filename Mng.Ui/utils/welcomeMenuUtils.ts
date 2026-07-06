import type { SideMenuItem } from '@/stores/apps/sideMenu';

export function flattenMenuItems(items: SideMenuItem[]): SideMenuItem[] {
  const result: SideMenuItem[] = [];
  for (const item of items) {
    if (item.itemType !== 'header' && item.to && item.to !== '#') {
      result.push(item);
    }
    if (item.children?.length) {
      result.push(...flattenMenuItems(item.children));
    }
  }
  return result;
}

export function hasMenuAccessToPath(flatItems: SideMenuItem[], targetPath: string): boolean {
  return flatItems.some((item) => {
    const to = item.to || '';
    return to === targetPath || targetPath.startsWith(`${to}/`);
  });
}

export function hasMenuAccessToPrefix(flatItems: SideMenuItem[], prefix: string): boolean {
  return flatItems.some((item) => {
    const to = item.to || '';
    return to === prefix || to.startsWith(`${prefix}/`);
  });
}
