/**
 * Menu Permission Middleware (Global)
 * Route bazlı menü permission kontrolü yapar
 * Her sayfa için menu item'dan permission kontrolü yapar
 * 
 * NOT: Bu middleware, auth.global.js'den SONRA çalışmalı
 * Auth middleware login kontrolü yapar, bu middleware permission kontrolü yapar
 */
export default defineNuxtRouteMiddleware(async (to) => {
  // Public routes - permission kontrolü yok
  const publicRoutes = ['/auth/login', '/auth/register', '/unauthorized', '/error', '/welcome'];
  if (publicRoutes.some(route => to.path.startsWith(route))) {
    return;
  }

  // Import auth store (once, at the beginning)
  const { useAuthStore } = await import('@/stores/auth');
  const authStore = useAuthStore();

  // Root path (/) - redirect to welcome page for non-admin users
  if (to.path === '/') {
    // Not authenticated - auth middleware will handle redirect to login
    if (!authStore.isAuthenticated || !authStore.userInfo) {
      return;
    }
    
    // Admin bypass - admins can access root path (will be redirected by app/router)
    if (authStore.isAdmin) {
      return;
    }
    
    // For non-admin users, redirect to welcome page
    // This prevents unauthorized redirect when user has no accessible pages
    return navigateTo('/welcome');
  }
  
  // Not authenticated - auth middleware will handle redirect to login
  if (!authStore.isAuthenticated || !authStore.userInfo) {
    return;
  }

  // Admin bypass - admins can access all routes (CHECK THIS FIRST!)
  if (authStore.isAdmin) {
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
