import { recordWelcomeRecentNavigation } from '@/composables/useRecentPages';
import { resolveWelcomePageTitle } from '@/composables/useWelcomePageTitle';

/**
 * SPA (ssr:false) — her /apps/* gezintisinde localStorage günceller.
 */
export default defineNuxtPlugin(() => {
  const router = useRouter();

  router.afterEach((to) => {
    if (!to.path.startsWith('/apps/')) return;
    recordWelcomeRecentNavigation(to.path, resolveWelcomePageTitle(to.path));
  });
});
