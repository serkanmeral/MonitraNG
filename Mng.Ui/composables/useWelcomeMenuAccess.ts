import { computed } from 'vue';
import { useSideMenuStore } from '@/stores/apps/sideMenu';
import {
  flattenMenuItems,
  hasMenuAccessToPath,
  hasMenuAccessToPrefix,
} from '@/utils/welcomeMenuUtils';

export function useWelcomeMenuAccess() {
  const menuStore = useSideMenuStore();

  const flatVisibleItems = computed(() => flattenMenuItems(menuStore.visibleMenuItems));

  function hasPrefix(prefix: string): boolean {
    return hasMenuAccessToPrefix(flatVisibleItems.value, prefix);
  }

  function hasPath(path: string): boolean {
    return hasMenuAccessToPath(flatVisibleItems.value, path);
  }

  return {
    flatVisibleItems,
    hasPrefix,
    hasPath,
  };
}
