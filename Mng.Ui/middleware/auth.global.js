export default defineNuxtRouteMiddleware(async (to, from) => {
    // DEBUG: Log route information
    console.log('[auth.global.js] Route check:', {
      path: to.path,
      name: to.name,
      fullPath: to.fullPath,
      matched: to.matched?.map(m => ({ path: m.path, name: m.name, components: Object.keys(m.components || {}) })) || [],
      params: to.params,
      query: to.query
    });
    
    // Public routes - authentication gerektirmeyen sayfalar
    const publicRoutes = [
      '/auth/login',
      '/auth/register',
      '/auth/reset-password',
      '/auth/forgot-password',
      '/auth/forgot-password2',
      '/unauthorized',
      '/error',
      '/welcome'
    ];
    
    // Public route kontrolü
    const isKapi = to.path === '/kapi' || to.path.startsWith('/kapi/');
    const isGecis = to.path === '/gecis' || to.path.startsWith('/gecis/');
    const isPublicRoute = isKapi || isGecis || publicRoutes.some(route => to.path.startsWith(route)) || 
        to.name === 'auth-login' || 
        to.name === 'auth-register' || 
        to.name === 'auth-reset-password' ||
        to.name === 'auth-forgot-password';
    
    console.log('[auth.global.js] Is public route:', isPublicRoute, {
      pathMatch: publicRoutes.some(route => to.path.startsWith(route)),
      nameMatch: to.name === 'auth-login' || to.name === 'auth-register' || to.name === 'auth-reset-password' || to.name === 'auth-forgot-password'
    });
    
    if (isPublicRoute) {
      console.log('[auth.global.js] Allowing access to public route');
      return;
    }
    
    const authStore = useAuthStore();
    const loginTarget = '/auth/login';
    
    // Token yoksa login sayfasına yönlendir
    if (!authStore.accessToken) {
      return navigateTo(loginTarget);
    }
    
    // Token expire olmuş mu kontrol et ve gerekirse refresh et
    const isValid = await authStore.ensureValidToken();
    
    if (!isValid) {
      // Token refresh başarısız, login sayfasına yönlendir
      return navigateTo(loginTarget);
    }
  })