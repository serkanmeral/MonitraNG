<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAuthStore } from '@/stores/auth';
import { fetchFromMngKeeper } from '@/services/apiService';
import { LanguageIcon, DeviceFloppyIcon, RefreshIcon, AlertCircleIcon } from 'vue-tabler-icons';

definePageMeta({
  layout: 'default',
});

const authStore = useAuthStore();
const router = useRouter();

const page = ref({ title: 'Locale Editor' });
const breadcrumbs = ref([
  {
    text: 'Dashboard',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Locale Editor',
    disabled: true,
    href: '#',
  },
]);

// Available locales
const availableLocales = ref([
  { value: 'tr', title: 'Türkçe' },
  { value: 'en', title: 'English' },
  { value: 'fr', title: 'français' },
  { value: 'ar', title: 'عربي' },
  { value: 'zh', title: '中国人' },
]);

// Selected locale
const selectedLocale = ref('tr');

// Locale data (JSON string)
const localeJsonString = ref('{}');

// Loading states
const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const successMessage = ref('');

// JSON validation
const jsonError = ref('');

// Check admin access
onMounted(() => {
  if (!authStore.isAdmin) {
    router.push('/unauthorized');
  }
});

// Watch locale JSON string for validation
watch(localeJsonString, (newValue) => {
  jsonError.value = '';
  try {
    JSON.parse(newValue);
  } catch (error: any) {
    jsonError.value = `JSON Syntax Error: ${error.message}`;
  }
});

// Load locale file
const loadLocale = async (locale: string) => {
  loading.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  
  try {
    const localeData = await fetchFromMngKeeper(`/system/locales/${locale}`, 'GET');
    
    // Format JSON with indentation
    localeJsonString.value = JSON.stringify(localeData, null, 2);
    
    successMessage.value = `${locale.toUpperCase()} locale file loaded successfully`;
    setTimeout(() => {
      successMessage.value = '';
    }, 3000);
  } catch (error: any) {
    // 404 means file doesn't exist, which is OK
    if (error.message?.includes('404') || error.statusCode === 404) {
      localeJsonString.value = '{}';
      errorMessage.value = `Locale file ${locale}.json not found in MinIO. You can create it by saving.`;
    } else {
      errorMessage.value = `Failed to load locale: ${error.message || error}`;
    }
  } finally {
    loading.value = false;
  }
};

// Save locale file
const saveLocale = async () => {
  // Validate JSON
  if (jsonError.value) {
    errorMessage.value = 'Please fix JSON syntax errors before saving.';
    return;
  }
  
  let localeData: any;
  try {
    localeData = JSON.parse(localeJsonString.value);
  } catch (error: any) {
    errorMessage.value = `Invalid JSON: ${error.message}`;
    return;
  }
  
  saving.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  
  try {
    await fetchFromMngKeeper(`/system/locales/${selectedLocale.value}`, 'PUT', localeData);
    
    // Invalidate cache for this locale
    if (process.client) {
      try {
        const nuxtApp = useNuxtApp();
        const invalidateCache = (nuxtApp as any).$invalidateLocaleCache;
        if (invalidateCache) {
          invalidateCache(selectedLocale.value);
        }
      } catch (cacheError) {
        // Failed to invalidate cache, silently continue
      }
    }
    
    successMessage.value = `${selectedLocale.value.toUpperCase()} locale file saved successfully!`;
    setTimeout(() => {
      successMessage.value = '';
    }, 5000);
  } catch (error: any) {
    errorMessage.value = `Failed to save locale: ${error.message || error}`;
  } finally {
    saving.value = false;
  }
};

// Format JSON
const formatJson = () => {
  try {
    const parsed = JSON.parse(localeJsonString.value);
    localeJsonString.value = JSON.stringify(parsed, null, 2);
    jsonError.value = '';
  } catch (error: any) {
    jsonError.value = `JSON Syntax Error: ${error.message}`;
  }
};

// Watch selected locale and load it
watch(selectedLocale, (newLocale) => {
  loadLocale(newLocale);
}, { immediate: false });

// Load initial locale on mount
onMounted(async () => {
  if (authStore.isAdmin) {
    await loadLocale(selectedLocale.value);
  }
});

// Reload current locale
const reloadLocale = () => {
  loadLocale(selectedLocale.value);
};
</script>

<template>
  <div class="locale-editor">
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs"></BaseBreadcrumb>
    
    <v-container fluid>
      <v-card elevation="10">
        <v-card-title class="pa-4 bg-primary text-white d-flex align-center">
          <LanguageIcon class="mr-2" size="24" />
          <span>{{ page.title }}</span>
        </v-card-title>
        
        <v-divider></v-divider>
        
        <v-card-text class="pa-6">
          <!-- Admin Warning -->
          <v-alert
            v-if="!authStore.isAdmin"
            type="warning"
            variant="tonal"
            density="compact"
            class="mb-4"
          >
            <strong>Uyarı:</strong> Bu sayfaya erişim için admin yetkisi gereklidir.
          </v-alert>

          <!-- Error Message -->
          <v-alert
            v-if="errorMessage"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-4"
            closable
            @click:close="errorMessage = ''"
          >
            {{ errorMessage }}
          </v-alert>
          
          <!-- Success Message -->
          <v-alert
            v-if="successMessage"
            type="success"
            variant="tonal"
            density="compact"
            class="mb-4"
            closable
            @click:close="successMessage = ''"
          >
            {{ successMessage }}
          </v-alert>

          <!-- Locale Selector and Actions -->
          <div class="d-flex justify-space-between align-center mb-4 flex-wrap ga-3">
            <v-select
              v-model="selectedLocale"
              :items="availableLocales"
              label="Select Locale"
              variant="outlined"
              density="comfortable"
              style="min-width: 200px;"
              :disabled="loading || saving"
            ></v-select>
            
            <div class="d-flex ga-2">
              <v-btn
                color="secondary"
                variant="outlined"
                :loading="loading"
                :disabled="saving"
                @click="reloadLocale"
              >
                <template #prepend>
                  <RefreshIcon size="18" />
                </template>
                Reload
              </v-btn>
              
              <v-btn
                color="info"
                variant="outlined"
                :disabled="loading || saving || !!jsonError"
                @click="formatJson"
              >
                Format JSON
              </v-btn>
              
              <v-btn
                color="primary"
                variant="flat"
                :loading="saving"
                :disabled="loading || !!jsonError"
                @click="saveLocale"
              >
                <template #prepend>
                  <DeviceFloppyIcon size="18" />
                </template>
                Save
              </v-btn>
            </div>
          </div>

          <!-- JSON Syntax Error -->
          <v-alert
            v-if="jsonError"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-4"
          >
            <AlertCircleIcon class="mr-2" size="18" />
            {{ jsonError }}
          </v-alert>

          <!-- JSON Editor (Textarea) -->
          <v-textarea
            v-model="localeJsonString"
            label="Locale JSON Data"
            variant="outlined"
            :rows="25"
            :disabled="loading || saving"
            :error="!!jsonError"
            :error-messages="jsonError"
            class="font-mono"
            style="font-family: 'Courier New', monospace;"
          ></v-textarea>

          <!-- Loading Overlay -->
          <div v-if="loading" class="text-center py-8">
            <v-progress-circular
              indeterminate
              color="primary"
              size="64"
            ></v-progress-circular>
            <p class="text-subtitle-1 mt-4 text-medium-emphasis">Loading locale file...</p>
          </div>
        </v-card-text>
      </v-card>
    </v-container>
  </div>
</template>

<style scoped>
.locale-editor {
  min-height: 100vh;
}

.font-mono {
  font-family: 'Courier New', Courier, monospace;
  font-size: 14px;
}
</style>
