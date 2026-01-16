<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useDomain, type Domain, type UpdateDomainRequest } from '@/composables/useDomain';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { SettingsIcon, RefreshIcon, EditIcon } from 'vue-tabler-icons';

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params);
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key, params);
  }
  return key;
};

const authStore = useAuthStore();
const router = useRouter();
const { getCurrentDomain, updateDomain } = useDomain();

// Page title and breadcrumbs
const page = computed(() => ({ title: t('domain.title') }));
const breadcrumbs = computed(() => [
  {
    text: t('domain.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('domain.breadcrumbs.domain'),
    disabled: true,
    href: '#',
  },
]);

// Domain data
const domain = ref<Domain | null>(null);
const isLoading = ref(false);
const isEditing = ref(false);
const error = ref<string | null>(null);
const successMessage = ref<string | null>(null);

// Form data
const formData = ref<UpdateDomainRequest>({
  displayName: '',
  relatedPersonPhone: '',
  logo: '',
  logoUrl: '',
  settings: {
    maxUsers: 100,
    maxAssets: 1000,
    enableMqtt: true,
  },
});

// Logo preview and file handling
const logoPreview = ref<string | null>(null);
const logoFileInput = ref<HTMLInputElement | null>(null);
const logoError = ref<string | null>(null);

// Format storage size
const formatStorageSize = (bytes: number): string => {
  if (!bytes || bytes === 0) return '0 B';
  
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  
  return `${(bytes / Math.pow(k, i)).toFixed(2)} ${sizes[i]}`;
};

// Format date
const formatDate = (dateString?: string): string => {
  if (!dateString) return '-';
  try {
    const date = new Date(dateString);
    return new Intl.DateTimeFormat('tr-TR', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  } catch {
    return dateString;
  }
};

// Get status label
const getStatusLabel = (status?: string | number): string => {
  if (status === undefined || status === null) return t('domain.status.active');
  
  // Convert to string if it's a number (enum value)
  let statusStr = '';
  if (typeof status === 'number') {
    // Map enum values to strings (DomainStatus enum: Pending=0, Active=1, Suspended=2, Expired=3, Deleted=4, Failed=5)
    const statusEnumMap: Record<number, string> = {
      0: 'pending',
      1: 'active',
      2: 'suspended',
      3: 'expired',
      4: 'deleted',
      5: 'failed',
    };
    statusStr = statusEnumMap[status] || 'active';
  } else {
    statusStr = String(status);
  }
  
  const statusKey = statusStr.toLowerCase();
  const statusMap: Record<string, string> = {
    pending: t('domain.status.pending'),
    active: t('domain.status.active'),
    suspended: t('domain.status.suspended'),
    expired: t('domain.status.expired'),
    deleted: t('domain.status.deleted'),
    failed: t('domain.status.failed'),
  };
  return statusMap[statusKey] || statusStr;
};

// Load domain data
const loadDomain = async () => {
  if (!authStore.isManager) {
    error.value = t('domain.messages.notManager');
    return;
  }

  isLoading.value = true;
  error.value = null;
  successMessage.value = null;

  try {
    const domainData = await getCurrentDomain();
    if (!domainData) {
      error.value = t('domain.messages.notFound');
      return;
    }

    domain.value = domainData;
    
    // Set logo preview if logo exists
    if (domainData.logo) {
      logoPreview.value = domainData.logo;
    } else if (domainData.logoUrl) {
      logoPreview.value = domainData.logoUrl;
    } else {
      logoPreview.value = null;
    }
    
    // Populate form with current domain data
    formData.value = {
      displayName: domainData.displayName || '',
      relatedPersonPhone: domainData.relatedPersonPhone || '',
      logo: domainData.logo || '',
      logoUrl: domainData.logoUrl || '',
      settings: {
        maxUsers: domainData.settings?.maxUsers || 100,
        maxAssets: domainData.settings?.maxAssets || 1000,
        enableMqtt: domainData.settings?.enableMqtt ?? true,
      },
    };
  } catch (err: any) {
    console.error('Error loading domain:', err);
    error.value = err.message || t('domain.messages.loadError');
  } finally {
    isLoading.value = false;
  }
};

// Save domain changes
const saveDomain = async () => {
  if (!domain.value) return;

  error.value = null;
  successMessage.value = null;

  // Validation
  if (!formData.value.displayName || formData.value.displayName.trim() === '') {
    error.value = t('domain.edit.validation.displayNameRequired');
    return;
  }

  isLoading.value = true;

  try {
    const updatedDomain = await updateDomain(domain.value.id, formData.value);
    if (updatedDomain) {
      domain.value = updatedDomain;
      
      // Update logo preview
      if (updatedDomain.logo) {
        logoPreview.value = updatedDomain.logo;
      } else if (updatedDomain.logoUrl) {
        logoPreview.value = updatedDomain.logoUrl;
      } else {
        logoPreview.value = null;
      }
      
      isEditing.value = false;
      successMessage.value = t('domain.messages.updateSuccess');
      
      // Clear success message after 3 seconds
      setTimeout(() => {
        successMessage.value = null;
      }, 3000);
    }
  } catch (err: any) {
    console.error('Error updating domain:', err);
    error.value = err.message || t('domain.messages.updateError');
  } finally {
    isLoading.value = false;
  }
};

// Handle logo file selection
const handleLogoFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files.length > 0) {
    handleLogoFile(target.files[0]);
  }
};

// Handle logo file
const handleLogoFile = (file: File) => {
  logoError.value = null;
  
  // Validate file type
  const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp', 'image/gif'];
  if (!allowedTypes.includes(file.type)) {
    logoError.value = t('domain.logo.errors.invalidFormat');
    return;
  }
  
  // Validate file size (max 2MB)
  const maxSize = 2 * 1024 * 1024; // 2MB
  if (file.size > maxSize) {
    logoError.value = t('domain.logo.errors.maxSize');
    return;
  }
  
  // Read file as base64
  const reader = new FileReader();
  reader.onload = (e) => {
    const result = e.target?.result as string;
    if (result) {
      formData.value.logo = result;
      logoPreview.value = result;
      logoError.value = null;
    }
  };
  reader.onerror = () => {
    logoError.value = t('domain.logo.errors.readError');
  };
  reader.readAsDataURL(file);
};

// Clear logo
const clearLogo = () => {
  formData.value.logo = '';
  formData.value.logoUrl = '';
  logoPreview.value = null;
  logoError.value = null;
  if (logoFileInput.value) {
    logoFileInput.value.value = '';
  }
};

// Trigger logo file input
const triggerLogoFileInput = () => {
  logoFileInput.value?.click();
};

// Cancel editing
const cancelEdit = () => {
  if (!domain.value) return;
  
  // Reset logo preview
  if (domain.value.logo) {
    logoPreview.value = domain.value.logo;
  } else if (domain.value.logoUrl) {
    logoPreview.value = domain.value.logoUrl;
  } else {
    logoPreview.value = null;
  }
  
  // Reset form to original values
  formData.value = {
    displayName: domain.value.displayName || '',
    relatedPersonPhone: domain.value.relatedPersonPhone || '',
    logo: domain.value.logo || '',
    logoUrl: domain.value.logoUrl || '',
    settings: {
      maxUsers: domain.value.settings?.maxUsers || 100,
      maxAssets: domain.value.settings?.maxAssets || 1000,
      enableMqtt: domain.value.settings?.enableMqtt ?? true,
    },
  };
  
  logoError.value = null;
  isEditing.value = false;
  error.value = null;
};

// Start editing
const startEdit = () => {
  isEditing.value = true;
  error.value = null;
  successMessage.value = null;
};

// Refresh domain data
const refreshDomain = async () => {
  await loadDomain();
};

// Check manager permission on mount
onMounted(async () => {
  if (!authStore.isManager) {
    // Optionally redirect to unauthorized page or dashboard
    // router.push('/unauthorized');
  } else {
    await loadDomain();
  }
});
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

  <v-row>
    <v-col cols="12">
      <!-- Warning message for non-manager users -->
      <v-alert
        v-if="!authStore.isManager"
        type="warning"
        variant="tonal"
        class="mb-4"
      >
        <v-alert-title>{{ t('domain.warning.title') }}</v-alert-title>
        {{ t('domain.warning.message') }}
      </v-alert>

      <!-- Error message -->
      <v-alert
        v-if="error"
        type="error"
        variant="tonal"
        class="mb-4"
        closable
        @click:close="error = null"
      >
        {{ error }}
      </v-alert>

      <!-- Success message -->
      <v-alert
        v-if="successMessage"
        type="success"
        variant="tonal"
        class="mb-4"
        closable
        @click:close="successMessage = null"
      >
        {{ successMessage }}
      </v-alert>

      <!-- Loading state -->
      <v-card v-if="isLoading && !domain" elevation="10">
        <v-card-text class="text-center py-10">
          <v-progress-circular indeterminate color="primary" size="64" />
          <p class="mt-4">{{ t('domain.messages.loading') }}</p>
        </v-card-text>
      </v-card>

      <!-- Domain information -->
      <template v-else-if="domain">
        <!-- Domain Info Card -->
        <v-card elevation="10" class="mb-4">
          <v-card-title class="d-flex align-center justify-space-between">
            <div class="d-flex align-center">
              <SettingsIcon size="20" class="mr-2" />
              {{ t('domain.cards.domainInfo') }}
            </div>
            <div>
              <v-btn
                v-if="!isEditing"
                icon=""
                size="small"
                variant="text"
                class="mr-2"
                @click="refreshDomain"
              >
                <RefreshIcon size="20" />
              </v-btn>
              <v-btn
                v-if="!isEditing && authStore.isManager"
                prepend-icon="mdi-pencil"
                variant="outlined"
                size="small"
                @click="startEdit"
              >
                {{ t('domain.buttons.edit') }}
              </v-btn>
              <template v-else-if="isEditing">
                <v-btn
                  variant="text"
                  size="small"
                  class="mr-2"
                  @click="cancelEdit"
                >
                  {{ t('domain.buttons.cancel') }}
                </v-btn>
                <v-btn
                  color="primary"
                  size="small"
                  :loading="isLoading"
                  @click="saveDomain"
                >
                  {{ t('domain.buttons.save') }}
                </v-btn>
              </template>
            </div>
          </v-card-title>

          <v-divider />

          <v-card-text>
            <v-row>
              <!-- Read-only fields -->
              <v-col cols="12" md="6">
                <v-text-field
                  :label="t('domain.fields.name')"
                  :model-value="domain.name"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  :label="t('domain.fields.databaseName')"
                  :model-value="domain.databaseName || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  :label="t('domain.fields.realmName')"
                  :model-value="domain.realmName || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  :label="t('domain.fields.status')"
                  :model-value="getStatusLabel(domain.status)"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <!-- Editable fields -->
              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.fields.displayName')"
                  :model-value="domain.displayName"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.displayName"
                  :label="t('domain.edit.displayName')"
                  :placeholder="t('domain.edit.displayNamePlaceholder')"
                  variant="outlined"
                  density="comfortable"
                  required
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.fields.relatedPersonPhone')"
                  :model-value="domain.relatedPersonPhone || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.relatedPersonPhone"
                  :label="t('domain.edit.relatedPersonPhone')"
                  :placeholder="t('domain.edit.relatedPersonPhonePlaceholder')"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <!-- Logo Field -->
              <v-col cols="12" md="6">
                <div v-if="!isEditing">
                  <v-text-field
                    :label="t('domain.fields.logo')"
                    :model-value="domain.logo ? t('domain.logo.uploaded') : (domain.logoUrl || '-')"
                    readonly
                    variant="outlined"
                    density="comfortable"
                  />
                  <div v-if="logoPreview || domain.logo || domain.logoUrl" class="mt-2">
                    <v-img
                      :src="logoPreview || domain.logo || domain.logoUrl"
                      max-width="150"
                      max-height="150"
                      contain
                      class="border rounded"
                    />
                  </div>
                </div>
                <div v-else>
                  <input
                    ref="logoFileInput"
                    type="file"
                    accept="image/jpeg,image/jpg,image/png,image/webp,image/gif"
                    class="d-none"
                    @change="handleLogoFileSelect"
                  />
                  <v-text-field
                    :label="t('domain.fields.logo')"
                    :model-value="logoPreview ? t('domain.logo.uploaded') : (formData.logoUrl || t('domain.logo.noImage'))"
                    readonly
                    variant="outlined"
                    density="comfortable"
                    append-inner-icon="mdi-upload"
                    @click:append-inner="triggerLogoFileInput"
                  />
                  <div class="d-flex align-center gap-2 mt-2">
                    <v-btn
                      variant="outlined"
                      color="primary"
                      size="small"
                      @click="triggerLogoFileInput"
                    >
                      {{ t('domain.buttons.uploadLogo') }}
                    </v-btn>
                    <v-btn
                      v-if="logoPreview || formData.logo"
                      variant="outlined"
                      color="error"
                      size="small"
                      @click="clearLogo"
                    >
                      {{ t('domain.buttons.removeLogo') }}
                    </v-btn>
                  </div>
                  <div v-if="logoPreview" class="mt-2">
                    <v-img
                      :src="logoPreview"
                      max-width="150"
                      max-height="150"
                      contain
                      class="border rounded"
                    />
                  </div>
                  <v-alert
                    v-if="logoError"
                    type="error"
                    variant="tonal"
                    density="compact"
                    class="mt-2"
                  >
                    {{ logoError }}
                  </v-alert>
                </div>
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.fields.logoUrl')"
                  :model-value="domain.logoUrl || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.logoUrl"
                  :label="t('domain.edit.logoUrl')"
                  :placeholder="t('domain.edit.logoUrlPlaceholder')"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  :label="t('domain.fields.storageQuota')"
                  :model-value="formatStorageSize(domain.storageQuota || 0)"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  :label="t('domain.fields.storageUsed')"
                  :model-value="formatStorageSize(domain.storageUsed || 0)"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  :label="t('domain.fields.createdAt')"
                  :model-value="formatDate(domain.createdAt)"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  :label="t('domain.fields.updatedAt')"
                  :model-value="formatDate(domain.updatedAt)"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6" v-if="domain.expiresAt">
                <v-text-field
                  :label="t('domain.fields.expiresAt')"
                  :model-value="formatDate(domain.expiresAt)"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>

        <!-- Domain Settings Card -->
        <v-card elevation="10">
          <v-card-title class="d-flex align-center">
            <SettingsIcon size="20" class="mr-2" />
            {{ t('domain.cards.domainSettings') }}
          </v-card-title>

          <v-divider />

          <v-card-text>
            <v-row>
              <v-col cols="12" md="4">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.fields.maxUsers')"
                  :model-value="domain.settings?.maxUsers || 100"
                  readonly
                  variant="outlined"
                  density="comfortable"
                  type="number"
                />
                <v-text-field
                  v-else
                  v-model.number="formData.settings!.maxUsers"
                  :label="t('domain.fields.maxUsers')"
                  variant="outlined"
                  density="comfortable"
                  type="number"
                  min="1"
                />
              </v-col>

              <v-col cols="12" md="4">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.fields.maxAssets')"
                  :model-value="domain.settings?.maxAssets || 1000"
                  readonly
                  variant="outlined"
                  density="comfortable"
                  type="number"
                />
                <v-text-field
                  v-else
                  v-model.number="formData.settings!.maxAssets"
                  :label="t('domain.fields.maxAssets')"
                  variant="outlined"
                  density="comfortable"
                  type="number"
                  min="1"
                />
              </v-col>

              <v-col cols="12" md="4">
                <v-switch
                  v-if="!isEditing"
                  :label="t('domain.fields.enableMqtt')"
                  :model-value="domain.settings?.enableMqtt ?? true"
                  readonly
                  disabled
                  color="primary"
                />
                <v-switch
                  v-else
                  v-model="formData.settings!.enableMqtt"
                  :label="t('domain.fields.enableMqtt')"
                  color="primary"
                />
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>
      </template>

      <!-- No domain found -->
      <v-card v-else-if="!isLoading" elevation="10">
        <v-card-text class="text-center py-10">
          <p class="text-h6">{{ t('domain.messages.notFound') }}</p>
        </v-card-text>
      </v-card>
    </v-col>
  </v-row>
</template>
