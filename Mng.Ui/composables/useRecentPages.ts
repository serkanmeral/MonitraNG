import { ref } from 'vue';
import {
  readWelcomeRecentPages,
  trackWelcomeRecentPage,
  type WelcomeRecentPageEntry,
} from '@/utils/welcomeRecentPagesStorage';
import { resolveWelcomePageTitle } from '@/composables/useWelcomePageTitle';

export type RecentPageEntry = WelcomeRecentPageEntry;

/** Plugin ve composable arasında paylaşımlı reaktif liste */
export const welcomeRecentPagesRef = ref<WelcomeRecentPageEntry[]>([]);

export function useRecentPages() {
  function refreshRecentPages() {
    const fromStorage = readWelcomeRecentPages();
    welcomeRecentPagesRef.value = fromStorage;
    return welcomeRecentPagesRef.value;
  }

  function trackVisit(path: string, title: string) {
    recordWelcomeRecentNavigation(path, title);
  }

  return {
    recentPages: welcomeRecentPagesRef,
    trackVisit,
    refreshRecentPages,
  };
}

/** Menü tıklaması / router hook — tek giriş noktası */
export function recordWelcomeRecentNavigation(path: string | undefined, title?: string) {
  if (!path?.startsWith('/apps/')) return;
  const resolvedTitle = resolveWelcomePageTitle(path) || title;
  const next = trackWelcomeRecentPage(path, resolvedTitle);
  if (next) {
    welcomeRecentPagesRef.value = next;
  }
}
