import { resolveWelcomePageTitle } from '@/composables/useWelcomePageTitle';
import { trackWelcomeRecentPage } from '@/utils/welcomeRecentPagesStorage';

export function useWelcomeRouteTracking() {
  function trackPath(path: string) {
    if (!path.startsWith('/apps/')) return;
    trackWelcomeRecentPage(path, resolveWelcomePageTitle(path));
  }

  return { trackPath };
}
