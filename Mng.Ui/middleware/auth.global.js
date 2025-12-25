export default defineNuxtRouteMiddleware(async (to, from) => {
    // Login sayfasına erişim serbest
    if (to.name === 'auth-login' || to.path === '/auth/login') {
      return;
    }
    
    const authStore = useAuthStore();
    
    // Token yoksa login sayfasına yönlendir
    if (!authStore.accessToken) {
      return navigateTo('/auth/login');
    }
    
    // Token expire olmuş mu kontrol et ve gerekirse refresh et
    const isValid = await authStore.ensureValidToken();
    
    if (!isValid) {
      // Token refresh başarısız, login sayfasına yönlendir
      return navigateTo('/auth/login');
    }
  })