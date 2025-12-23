export default defineNuxtPlugin(() => {
  const authStore = useAuthStore();
  
  // Initialize auth state from cookies/localStorage
  authStore.initializeAuth();
});

