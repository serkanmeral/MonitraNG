<script setup lang="ts">
import { computed } from 'vue';
import { useAuthStore } from '@/stores/auth';

definePageMeta({
  layout: 'default',
});

const authStore = useAuthStore();

// Get user display name
const userDisplayName = computed(() => {
  if (!authStore.userInfo) return 'Kullanıcı';
  
  // Try to get full name from firstName + lastName
  if (authStore.userInfo.given_name && authStore.userInfo.family_name) {
    return `${authStore.userInfo.given_name} ${authStore.userInfo.family_name}`;
  }
  
  // Try to get name from token (if available)
  const name = authStore.userInfo.name || authStore.userInfo.given_name || authStore.userInfo.preferred_username;
  if (name) return name;
  
  // Fallback to username
  return authStore.userInfo.username || 'Kullanıcı';
});
</script>

<template>
  <div class="welcome-page">
    <v-container class="fill-height">
      <v-row align="center" justify="center" class="fill-height">
        <v-col cols="12" md="8" lg="6" xl="5">
          <v-card class="pa-8 text-center" elevation="2">
            <v-card-title class="text-h3 font-weight-bold mb-4">
              Hoş Geldiniz!
            </v-card-title>
            
            <v-card-subtitle class="text-h6 mb-6">
              {{ userDisplayName }}
            </v-card-subtitle>
            
            <v-card-text class="text-body-1 mb-6">
              MonitraNG sistemine başarıyla giriş yaptınız.
              <br />
              Menüden erişebileceğiniz sayfalara göz atabilirsiniz.
            </v-card-text>
            
            <v-card-actions class="justify-center">
              <v-btn
                color="primary"
                size="large"
                variant="flat"
                @click="$router.push('/')"
              >
                Ana Sayfaya Dön
              </v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
      </v-row>
    </v-container>
  </div>
</template>

<style scoped>
.welcome-page {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.v-card {
  border-radius: 16px;
}
</style>
