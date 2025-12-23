export default defineNuxtRouteMiddleware((to, from) => {
    const token = useCookie('access_token');
    
    // Login sayfasına erişim serbest
    if (to.name === 'auth-login' || to.path === '/auth/login') {
      return;
    }
    
    // Token yoksa login sayfasına yönlendir
    if (!token.value) {
      return navigateTo('/auth/login');
    }
  })