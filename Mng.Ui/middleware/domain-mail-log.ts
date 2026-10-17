/**
 * Gönderilen e-posta kaydı: yalnızca admin veya manager.
 */
import { ensureAuthUserReady, userHasManagerRole } from '@/utils/authRoles';

export default defineNuxtRouteMiddleware(async () => {
  if (import.meta.server) return;

  const auth = useAuthStore();
  await ensureAuthUserReady(auth);

  if (!userHasManagerRole(auth.userInfo)) {
    return navigateTo('/dashboards/analytical');
  }
});
