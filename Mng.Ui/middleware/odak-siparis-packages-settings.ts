/**
 * Odak Sipariş — paket ayarları sayfası: yalnızca isManager.
 */
export default defineNuxtRouteMiddleware(() => {
  if (import.meta.server) return;
  const auth = useAuthStore();
  if (!auth.isManager) {
    return navigateTo('/apps/odak-siparis/packages');
  }
});
