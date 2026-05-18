/**
 * Tablo sütunları ayarı: yalnızca admin veya manager (JWT: isAdmin / is_manager).
 * Auth store’da isManager, admin + manager birleşimidir.
 */
export default defineNuxtRouteMiddleware(() => {
  if (import.meta.server) return;
  const auth = useAuthStore();
  if (!auth.isManager) {
    return navigateTo('/apps/task-manager/workspace');
  }
});
