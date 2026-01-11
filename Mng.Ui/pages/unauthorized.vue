<script setup lang="ts">
import { useAuthStore } from '@/stores/auth';
import { useSideMenuStore } from '@/stores/apps/sideMenu';
import { computed } from 'vue';

definePageMeta({
  layout: 'blank',
});

const authStore = useAuthStore();
const menuStore = useSideMenuStore();

const pageTitle = 'Yetkisiz Erişim';
const pageDescription = computed(() => {
  if (!authStore.isAuthenticated) {
    return 'Bu sayfaya erişmek için giriş yapmanız gerekiyor.';
  }
  
  if (authStore.isAdmin) {
    return 'Beklenmeyen bir hata oluştu. Lütfen sistem yöneticisi ile iletişime geçin.';
  }

  return 'Bu sayfaya erişim yetkiniz bulunmamaktadır. Gerekli yetkilere sahip olmak için sistem yöneticiniz ile iletişime geçin.';
});

// Go back or go to home
const goBack = () => {
  if (window.history.length > 1) {
    window.history.back();
  } else {
    navigateTo('/');
  }
};

const goHome = () => {
  navigateTo('/');
};
</script>

<template>
  <div class="unauthorized-page">
    <v-container class="fill-height" fluid>
      <v-row align="center" justify="center">
        <v-col cols="12" sm="8" md="6" lg="4">
          <v-card class="elevation-0">
            <v-card-text class="text-center pa-12">
              <v-icon
                size="120"
                color="error"
                class="mb-6"
              >
                mdi-lock-alert
              </v-icon>
              
              <h1 class="text-h4 font-weight-bold mb-4">
                {{ pageTitle }}
              </h1>
              
              <p class="text-body-1 text-medium-emphasis mb-8">
                {{ pageDescription }}
              </p>

              <v-btn
                color="primary"
                variant="flat"
                size="large"
                class="mb-4"
                @click="goHome"
                block
              >
                Ana Sayfaya Dön
              </v-btn>

              <v-btn
                color="grey"
                variant="outlined"
                size="large"
                @click="goBack"
                block
              >
                Geri Git
              </v-btn>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </v-container>
  </div>
</template>

<style scoped>
.unauthorized-page {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.unauthorized-page :deep(.v-card) {
  border-radius: 16px;
}
</style>
