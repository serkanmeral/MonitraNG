/**
 * Menu Permission Middleware (Global)
 * Route bazlı menü permission kontrolü yapar
 * Her sayfa için menu item'dan permission kontrolü yapar
 * 
 * NOT: Bu middleware, auth.global.js'den SONRA çalışmalı
 * Auth middleware login kontrolü yapar, bu middleware permission kontrolü yapar
 */
export default defineNuxtRouteMiddleware(async (to) => {
  // Auth / hata sayfaları ve ana sayfa — menü izni kontrolü yok
  if (to.path === '/' || to.path === '/welcome') {
    return;
  }
  const publicRoutes = ['/auth/login', '/auth/register', '/unauthorized', '/error'];
  if (publicRoutes.some((route) => to.path.startsWith(route))) {
    return;
  }

  const { useAuthStore } = await import('@/stores/auth');
  const authStore = useAuthStore();

  if (!authStore.isAuthenticated || !authStore.userInfo) {
    return;
  }

  // Admin bypass - admins can access all routes (CHECK THIS FIRST!)
  if (authStore.isAdmin) {
    return;
  }

  // Header kullanıcı menüsünden açılan sayfalar: Profil ve Notlar side menu'den değil,
  // header'daki profil dropdown'dan açıldığı için tüm giriş yapmış kullanıcılara izin ver
  const headerProfileRoutes = ['/apps/profile', '/apps/notes'];
  if (headerProfileRoutes.includes(to.path)) {
    return;
  }

  // Get menu store
  const { useSideMenuStore } = await import('@/stores/apps/sideMenu');
  const menuStore = useSideMenuStore();

  // Ensure menu items are loaded
  try {
    await menuStore.loadMenuItems(false);
  } catch (error) {
    // If menu can't be loaded, allow access (fallback behavior) - especially for root path
    return;
  }

  // Find menu item for current route
  const menuItem = menuStore.getMenuItemByRoute(to.path);

  // If no menu item found, allow access (page might not be in menu, e.g., root path '/')
  if (!menuItem) {
    return;
  }

  // Check page type access
  const pageType = menuItem.pageType || 'user';

  if (pageType === 'admin' && !authStore.isAdmin) {
    return navigateTo('/unauthorized');
  }

  if (pageType === 'manager' && !authStore.isManager && !authStore.isAdmin) {
    return navigateTo('/unauthorized');
  }

  // Manager sayfaları: Manager kullanıcılar kısıtlamasız erişebilir (permission kontrolü yok)
  if (pageType === 'manager' && authStore.isManager) {
    return; // Bypass permission kontrolü
  }

  // Check view permission
  if (menuItem.permissions?.groups) {
    const userGroups = authStore.userGroups;
    const hasViewPermission = userGroups.some(groupName => {
      return menuItem.permissions?.groups[groupName]?.view === true;
    });

    if (!hasViewPermission) {
      return navigateTo('/unauthorized');
    }
  }
});
