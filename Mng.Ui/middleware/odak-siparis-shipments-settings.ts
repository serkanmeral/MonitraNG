/**
 * Odak Sipariş — global sevkiyat listesi ayarları: yalnızca manager.
 */
import { ensureAuthUserReady, userHasManagerRole } from '@/utils/authRoles';

export default defineNuxtRouteMiddleware(async () => {
  if (import.meta.server) return;

  const auth = useAuthStore();
  await ensureAuthUserReady(auth);

  if (!userHasManagerRole(auth.userInfo)) {
    return navigateTo('/apps/odak-siparis/shipments');
  }
});
