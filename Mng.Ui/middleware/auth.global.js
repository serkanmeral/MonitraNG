export default defineNuxtRouteMiddleware((to, from) => {
    
    const token = useCookie('access_token') // Veya token'ı aldığınız yöntem
    
    if (!token.value && to.name !== 'auth-Login') {
      
      return navigateTo('../auth/login')
    }
  })