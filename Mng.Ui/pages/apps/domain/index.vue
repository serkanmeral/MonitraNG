<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useDomain, type Domain, type UpdateDomainRequest, type DirectoryLdapSettings, type DirectoryPrivilegeSettings } from '@/composables/useDomain';
import { useGroupStore } from '@/stores/apps/group';
import { fetchFromMngKeeper } from '@/services/apiService';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DomainBackupCard from '@/components/apps/domain/DomainBackupCard.vue';
import DomainDirectorySyncCard from '@/components/apps/domain/DomainDirectorySyncCard.vue';
import { SettingsIcon, RefreshIcon, EditIcon, ShieldIcon, ServerIcon } from 'vue-tabler-icons';

const emptyDirectoryLdap = (): DirectoryLdapSettings => ({
  enabled: false,
  host: '',
  port: 389,
  useSsl: false,
  baseDn: '',
  bindUsername: '',
  bindPassword: '',
});

const mapDirectoryLdap = (ldap?: DirectoryLdapSettings | null): DirectoryLdapSettings => ({
  enabled: ldap?.enabled ?? false,
  host: ldap?.host || '',
  port: ldap?.port && ldap.port > 0 ? ldap.port : 389,
  useSsl: ldap?.useSsl ?? false,
  baseDn: ldap?.baseDn || '',
  bindUsername: ldap?.bindUsername || '',
  // Write-only in the form: empty on save keeps the existing password on the server.
  bindPassword: '',
});

const mapManagerPrivileges = (
  privileges?: DirectoryPrivilegeSettings | null
): DirectoryPrivilegeSettings => ({
  managerGroupNames: [...(privileges?.managerGroupNames || [])],
});

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
const groupStore = useGroupStore();
const router = useRouter();
const { getCurrentDomain, updateDomain } = useDomain();

const managerGroupOptions = computed<string[]>(() =>
  [...new Set<string>(
    groupStore.groups
      .filter((group) => group.isActive)
      .map((group) => group.name.trim())
      .filter(Boolean),
  )].sort((a, b) => a.localeCompare(b)),
);

const managerGroupNames = computed<string[]>({
  get: () => formData.value.settings?.directoryPrivileges?.managerGroupNames ?? [],
  set: (names) => {
    formData.value.settings ??= {};
    formData.value.settings.directoryPrivileges ??= {};
    formData.value.settings.directoryPrivileges.managerGroupNames = names;
  },
});

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

// License data
interface LicenseInfo {
  domainName: string;
  licenseType: number; // 0 = Trial, 1 = Real
  isValid: boolean;
  isExpired: boolean;
  expiresAt: string;
  issuedAt: string;
  issuedBy: string;
  expirationBehavior?: {
    blockTokenGeneration: boolean;
    blockCrudOperations: boolean;
    blockGetOperations: boolean;
    allowReadOnly: boolean;
    customMessage?: string;
  };
  licenseFeatures?: {
    maxUsers: number;
    maxDomains: number;
    maxStorageGB: number;
    enableAdvancedFeatures: boolean;
    supportLevel?: string;
    countActiveUsersOnly: boolean;
    activeUserDefinition?: {
      isActive: boolean;
      lastLoginDays?: number;
    };
  };
  customerInfo?: any;
  metadata?: any;
}

interface UserCountInfo {
  domainName: string;
  activeUserCount: number;
  maxUsers?: number;
  canCreateUser: boolean;
}

const licenseInfo = ref<LicenseInfo | null>(null);
const userCountInfo = ref<UserCountInfo | null>(null);
const isLoadingLicense = ref(false);
const licenseError = ref<string | null>(null);

// Form data
const formData = ref<UpdateDomainRequest>({
  displayName: '',
  discoveryRootLabel: '',
  relatedPersonPhone: '',
  logo: '',
  logoUrl: '',
  settings: {
    maxUsers: 100,
    maxAssets: 1000,
    enableMqtt: true,
    directoryPrivileges: mapManagerPrivileges(null),
    directoryLdap: emptyDirectoryLdap(),
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

// Get license type label
const getLicenseTypeLabel = (licenseType: number): string => {
  return licenseType === 0 ? 'Trial' : 'Real';
};

// Load license data
const loadLicense = async () => {
  if (!domain.value) return;

  isLoadingLicense.value = true;
  licenseError.value = null;

  try {
    const domainName = domain.value.name;
    
    // Load license info
    try {
      const license = await fetchFromMngKeeper(`license/${domainName}`);
      licenseInfo.value = license as LicenseInfo;
    } catch (err: any) {
      console.warn('License info not available:', err);
      licenseInfo.value = null;
    }

    // Load user count info
    try {
      const userCount = await fetchFromMngKeeper(`license/${domainName}/user-count`);
      userCountInfo.value = userCount as UserCountInfo;
    } catch (err: any) {
      console.warn('User count info not available:', err);
      userCountInfo.value = null;
    }
  } catch (err: any) {
    console.error('Error loading license:', err);
    licenseError.value = err.message || t('domain.messages.licenseLoadError');
  } finally {
    isLoadingLicense.value = false;
  }
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
      discoveryRootLabel: domainData.discoveryRootLabel || '',
      relatedPersonPhone: domainData.relatedPersonPhone || '',
      logo: domainData.logo || '',
      logoUrl: domainData.logoUrl || '',
      settings: {
        maxUsers: domainData.settings?.maxUsers || 100,
        maxAssets: domainData.settings?.maxAssets || 1000,
        enableMqtt: domainData.settings?.enableMqtt ?? true,
        directoryPrivileges: mapManagerPrivileges(domainData.settings?.directoryPrivileges),
        directoryLdap: mapDirectoryLdap(domainData.settings?.directoryLdap),
      },
    };

    // Load license info after domain is loaded
    await loadLicense();
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
    discoveryRootLabel: domain.value.discoveryRootLabel || '',
    relatedPersonPhone: domain.value.relatedPersonPhone || '',
    logo: domain.value.logo || '',
    logoUrl: domain.value.logoUrl || '',
    settings: {
      maxUsers: domain.value.settings?.maxUsers || 100,
      maxAssets: domain.value.settings?.maxAssets || 1000,
      enableMqtt: domain.value.settings?.enableMqtt ?? true,
      directoryPrivileges: mapManagerPrivileges(domain.value.settings?.directoryPrivileges),
      directoryLdap: mapDirectoryLdap(domain.value.settings?.directoryLdap),
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

// Refresh license data
const refreshLicense = async () => {
  await loadLicense();
};

// Check manager permission on mount
onMounted(async () => {
  if (!authStore.isManager) {
    // Optionally redirect to unauthorized page or dashboard
    // router.push('/unauthorized');
  } else {
    await Promise.all([
      loadDomain(),
      groupStore.fetchAllGroups({ isActive: true }),
    ]);
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
                  :label="t('domain.fields.discoveryRootLabel')"
                  :model-value="domain.discoveryRootLabel || domain.displayName || domain.name"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.discoveryRootLabel"
                  :label="t('domain.edit.discoveryRootLabel')"
                  :placeholder="t('domain.edit.discoveryRootLabelPlaceholder')"
                  :hint="t('domain.edit.discoveryRootLabelHint')"
                  persistent-hint
                  maxlength="120"
                  variant="outlined"
                  density="comfortable"
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

        <!-- License Info Card -->
        <v-card elevation="10" class="mb-4">
          <v-card-title class="d-flex align-center justify-space-between">
            <div class="d-flex align-center">
              <ShieldIcon size="20" class="mr-2" />
              {{ t('domain.cards.licenseInfo') }}
            </div>
            <v-btn
              icon=""
              size="small"
              variant="text"
              :loading="isLoadingLicense"
              @click="refreshLicense"
            >
              <RefreshIcon size="20" />
            </v-btn>
          </v-card-title>

          <v-divider />

          <v-card-text>
            <v-alert
              v-if="licenseError"
              type="warning"
              variant="tonal"
              density="compact"
              class="mb-4"
            >
              {{ licenseError }}
            </v-alert>

            <v-alert
              v-else-if="!licenseInfo && !isLoadingLicense"
              type="info"
              variant="tonal"
              density="compact"
              class="mb-4"
            >
              {{ t('domain.messages.licenseNotFound') }}
            </v-alert>

            <div v-if="licenseInfo">
              <!-- License Status Badges -->
              <div class="d-flex align-center mb-4" style="gap: 12px;">
                <v-chip
                  :color="licenseInfo.isValid ? 'success' : 'error'"
                  variant="tonal"
                  size="large"
                >
                  {{ licenseInfo.isValid ? t('domain.license.status.valid') : t('domain.license.status.expired') }}
                </v-chip>
                <v-chip
                  :color="getLicenseTypeLabel(licenseInfo.licenseType) === 'Real' ? 'primary' : 'warning'"
                  variant="tonal"
                >
                  {{ getLicenseTypeLabel(licenseInfo.licenseType) === 'Real' ? t('domain.license.type.real') : t('domain.license.type.trial') }}
                </v-chip>
              </div>

              <!-- Expiration Behavior Status -->
              <div v-if="licenseInfo.expirationBehavior" class="mb-4">
                <h4 class="text-subtitle-1 font-weight-bold mb-3">{{ t('domain.license.expirationBehavior.title') }}</h4>
                <v-row>
                  <v-col cols="12" md="6">
                    <v-card
                      :color="licenseInfo.expirationBehavior.blockTokenGeneration ? 'error' : 'success'"
                      variant="tonal"
                      class="mb-2"
                    >
                      <v-card-text class="d-flex align-center justify-space-between">
                        <div class="d-flex align-center">
                          <v-icon
                            :color="licenseInfo.expirationBehavior.blockTokenGeneration ? 'error' : 'success'"
                            class="mr-2"
                          >
                            {{ licenseInfo.expirationBehavior.blockTokenGeneration ? 'mdi-close-circle' : 'mdi-check-circle' }}
                          </v-icon>
                          <div>
                            <div class="font-weight-medium">{{ t('domain.license.expirationBehavior.tokenGeneration') }}</div>
                            <div class="text-caption text-medium-emphasis">
                              {{ t('domain.license.expirationBehavior.tokenGeneration') }}: {{ licenseInfo.expirationBehavior.blockTokenGeneration }}
                            </div>
                          </div>
                        </div>
                        <v-chip
                          :color="licenseInfo.expirationBehavior.blockTokenGeneration ? 'error' : 'success'"
                          size="small"
                          variant="flat"
                        >
                          {{ licenseInfo.expirationBehavior.blockTokenGeneration ? t('domain.license.expirationBehavior.blocked') : t('domain.license.expirationBehavior.allowed') }}
                        </v-chip>
                      </v-card-text>
                    </v-card>
                  </v-col>

                  <v-col cols="12" md="6">
                    <v-card
                      :color="licenseInfo.expirationBehavior.blockGetOperations ? 'error' : 'success'"
                      variant="tonal"
                      class="mb-2"
                    >
                      <v-card-text class="d-flex align-center justify-space-between">
                        <div class="d-flex align-center">
                          <v-icon
                            :color="licenseInfo.expirationBehavior.blockGetOperations ? 'error' : 'success'"
                            class="mr-2"
                          >
                            {{ licenseInfo.expirationBehavior.blockGetOperations ? 'mdi-close-circle' : 'mdi-check-circle' }}
                          </v-icon>
                          <div>
                            <div class="font-weight-medium">{{ t('domain.license.expirationBehavior.getOperations') }}</div>
                            <div class="text-caption text-medium-emphasis">
                              {{ t('domain.license.expirationBehavior.getOperations') }}: {{ licenseInfo.expirationBehavior.blockGetOperations }}
                            </div>
                          </div>
                        </div>
                        <v-chip
                          :color="licenseInfo.expirationBehavior.blockGetOperations ? 'error' : 'success'"
                          size="small"
                          variant="flat"
                        >
                          {{ licenseInfo.expirationBehavior.blockGetOperations ? t('domain.license.expirationBehavior.blocked') : t('domain.license.expirationBehavior.allowed') }}
                        </v-chip>
                      </v-card-text>
                    </v-card>
                  </v-col>

                  <v-col cols="12" md="6">
                    <v-card
                      :color="licenseInfo.expirationBehavior.blockCrudOperations ? 'error' : 'success'"
                      variant="tonal"
                      class="mb-2"
                    >
                      <v-card-text class="d-flex align-center justify-space-between">
                        <div class="d-flex align-center">
                          <v-icon
                            :color="licenseInfo.expirationBehavior.blockCrudOperations ? 'error' : 'success'"
                            class="mr-2"
                          >
                            {{ licenseInfo.expirationBehavior.blockCrudOperations ? 'mdi-close-circle' : 'mdi-check-circle' }}
                          </v-icon>
                          <div>
                            <div class="font-weight-medium">{{ t('domain.license.expirationBehavior.crudOperations') }}</div>
                            <div class="text-caption text-medium-emphasis">
                              {{ t('domain.license.expirationBehavior.crudOperations') }}: {{ licenseInfo.expirationBehavior.blockCrudOperations }}
                            </div>
                          </div>
                        </div>
                        <v-chip
                          :color="licenseInfo.expirationBehavior.blockCrudOperations ? 'error' : 'success'"
                          size="small"
                          variant="flat"
                        >
                          {{ licenseInfo.expirationBehavior.blockCrudOperations ? t('domain.license.expirationBehavior.blocked') : t('domain.license.expirationBehavior.allowed') }}
                        </v-chip>
                      </v-card-text>
                    </v-card>
                  </v-col>

                  <v-col cols="12" md="6">
                    <v-card
                      :color="licenseInfo.expirationBehavior.allowReadOnly ? 'info' : 'grey'"
                      variant="tonal"
                      class="mb-2"
                    >
                      <v-card-text class="d-flex align-center justify-space-between">
                        <div class="d-flex align-center">
                          <v-icon
                            :color="licenseInfo.expirationBehavior.allowReadOnly ? 'info' : 'grey'"
                            class="mr-2"
                          >
                            {{ licenseInfo.expirationBehavior.allowReadOnly ? 'mdi-check-circle' : 'mdi-minus-circle' }}
                          </v-icon>
                          <div>
                            <div class="font-weight-medium">{{ t('domain.license.expirationBehavior.readOnly') }}</div>
                            <div class="text-caption text-medium-emphasis">
                              {{ t('domain.license.expirationBehavior.readOnly') }}: {{ licenseInfo.expirationBehavior.allowReadOnly }}
                            </div>
                          </div>
                        </div>
                        <v-chip
                          :color="licenseInfo.expirationBehavior.allowReadOnly ? 'info' : 'grey'"
                          size="small"
                          variant="flat"
                        >
                          {{ licenseInfo.expirationBehavior.allowReadOnly ? t('domain.license.expirationBehavior.active') : t('domain.license.expirationBehavior.passive') }}
                        </v-chip>
                      </v-card-text>
                    </v-card>
                  </v-col>
                </v-row>

                <v-alert
                  v-if="licenseInfo.isExpired && licenseInfo.expirationBehavior"
                  type="warning"
                  variant="tonal"
                  density="compact"
                  class="mt-3"
                >
                  {{ licenseInfo.expirationBehavior.customMessage || t('domain.license.expirationBehavior.defaultTrialMessage') }}
                </v-alert>
              </div>

              <!-- User Count Info -->
              <v-divider v-if="userCount" class="my-4" />
              <div v-if="userCount" class="mb-4">
                <h4 class="text-subtitle-1 font-weight-bold mb-3">{{ t('domain.license.userStatus.title') }}</h4>
                <v-row class="align-center">
                  <v-col cols="12" md="4">
                    <div class="text-center">
                      <p class="text-caption text-medium-emphasis mb-1">{{ t('domain.license.userStatus.activeUserCount') }}</p>
                      <p class="text-h4 font-weight-bold text-primary">{{ userCount.activeUserCount }}</p>
                    </div>
                  </v-col>
                  <v-col cols="12" md="4" v-if="userCount.maxUsers">
                    <div class="text-center">
                      <p class="text-caption text-medium-emphasis mb-1">{{ t('domain.license.userStatus.maxUsers') }}</p>
                      <p class="text-h4 font-weight-bold">{{ userCount.maxUsers }}</p>
                    </div>
                  </v-col>
                  <v-col cols="12" md="4">
                    <div class="text-center">
                      <p class="text-caption text-medium-emphasis mb-1">{{ t('domain.license.userStatus.canCreateUser') }}</p>
                      <v-chip
                        :color="userCount.canCreateUser ? 'success' : 'error'"
                        size="large"
                        variant="flat"
                      >
                        {{ userCount.canCreateUser ? t('common.yes') : t('common.no') }}
                      </v-chip>
                    </div>
                  </v-col>
                </v-row>
                <div v-if="userCount.maxUsers" class="mt-4">
                  <v-progress-linear
                    :model-value="Math.min((userCount.activeUserCount / userCount.maxUsers) * 100, 100)"
                    :color="userCount.canCreateUser ? 'success' : 'error'"
                    height="8"
                    rounded
                  />
                  <p class="text-caption text-center text-medium-emphasis mt-1">
                    {{ userCount.activeUserCount }} / {{ userCount.maxUsers }} {{ t('domain.license.userStatus.userCount') }}
                  </p>
                </div>
              </div>

              <!-- License Details -->
              <v-divider class="my-4" />
              <div>
                <h4 class="text-subtitle-1 font-weight-bold mb-3">{{ t('domain.license.details.title') }}</h4>
                <v-row>
                  <v-col cols="12" md="6">
                    <div>
                      <p class="text-caption text-medium-emphasis">{{ t('domain.license.details.issuedAt') }}</p>
                      <p class="font-weight-medium">{{ formatDate(licenseInfo.issuedAt) }}</p>
                    </div>
                  </v-col>
                  <v-col cols="12" md="6">
                    <div>
                      <p class="text-caption text-medium-emphasis">{{ t('domain.license.details.expiresAt') }}</p>
                      <p class="font-weight-medium">{{ formatDate(licenseInfo.expiresAt) }}</p>
                    </div>
                  </v-col>
                  <v-col cols="12" md="6">
                    <div>
                      <p class="text-caption text-medium-emphasis">{{ t('domain.license.details.issuedBy') }}</p>
                      <p class="font-weight-medium">{{ licenseInfo.issuedBy }}</p>
                    </div>
                  </v-col>
                  <v-col cols="12" md="6" v-if="licenseInfo.licenseFeatures">
                    <div>
                      <p class="text-caption text-medium-emphasis">{{ t('domain.license.details.maxUsers') }}</p>
                      <p class="font-weight-medium">{{ licenseInfo.licenseFeatures.maxUsers }}</p>
                    </div>
                  </v-col>
                </v-row>
              </div>

              <!-- Customer Info (Real License) -->
              <v-divider v-if="licenseInfo.customerInfo" class="my-4" />
              <div v-if="licenseInfo.customerInfo">
                <h4 class="text-subtitle-1 font-weight-bold mb-2">{{ t('domain.license.customerInfo.title') }}</h4>
                <v-row>
                  <v-col cols="12" md="6">
                    <div>
                      <p class="text-caption text-medium-emphasis">{{ t('domain.license.customerInfo.customerName') }}</p>
                      <p class="font-weight-medium">{{ licenseInfo.customerInfo.customerName }}</p>
                    </div>
                  </v-col>
                  <v-col cols="12" md="6">
                    <div>
                      <p class="text-caption text-medium-emphasis">{{ t('domain.license.customerInfo.contactEmail') }}</p>
                      <p class="font-weight-medium">{{ licenseInfo.customerInfo.contactEmail }}</p>
                    </div>
                  </v-col>
                </v-row>
              </div>
            </div>

            <div v-if="isLoadingLicense" class="text-center py-8">
              <v-progress-circular indeterminate color="primary" size="32" />
              <p class="mt-2">{{ t('domain.messages.loading') }}</p>
            </div>
          </v-card-text>
        </v-card>

        <!-- Domain Settings Card -->
        <v-card elevation="10" class="mb-4">
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

        <!-- Manager privilege groups -->
        <v-card elevation="10" class="mb-4">
          <v-card-title class="d-flex align-center">
            <ShieldIcon size="20" class="mr-2" />
            {{ t('domain.cards.managerGroups') }}
          </v-card-title>

          <v-divider />

          <v-card-text>
            <p class="text-body-2 text-medium-emphasis mb-4">
              {{ t('domain.managerGroups.description') }}
            </p>

            <div v-if="!isEditing" class="d-flex flex-wrap ga-2">
              <v-chip
                v-for="groupName in domain.settings?.directoryPrivileges?.managerGroupNames || []"
                :key="groupName"
                color="primary"
                variant="tonal"
              >
                {{ groupName }}
              </v-chip>
              <span
                v-if="!(domain.settings?.directoryPrivileges?.managerGroupNames?.length)"
                class="text-body-2 text-medium-emphasis"
              >
                {{ t('domain.managerGroups.empty') }}
              </span>
            </div>

            <div v-else>
              <v-autocomplete
                v-model="managerGroupNames"
                :items="managerGroupOptions"
                :label="t('domain.managerGroups.label')"
                :hint="t('domain.managerGroups.hint')"
                :loading="groupStore.loading"
                :disabled="groupStore.loading"
                multiple
                chips
                closable-chips
                clearable
                persistent-hint
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
              <v-alert
                v-if="groupStore.error"
                type="warning"
                variant="tonal"
                density="compact"
                class="mt-3"
              >
                {{ groupStore.error }}
              </v-alert>
            </div>
          </v-card-text>
        </v-card>

        <!-- Directory LDAP (AD bind) -->
        <v-card elevation="10" class="mb-4">
          <v-card-title class="d-flex align-center">
            <ServerIcon size="20" class="mr-2" />
            {{ t('domain.cards.directoryLdap') }}
          </v-card-title>

          <v-divider />

          <v-card-text>
            <p class="text-body-2 text-medium-emphasis mb-4">
              {{ t('domain.directoryLdap.description') }}
            </p>

            <v-row>
              <v-col cols="12" md="4">
                <v-switch
                  v-if="!isEditing"
                  :label="t('domain.directoryLdap.enabled')"
                  :model-value="domain.settings?.directoryLdap?.enabled ?? false"
                  readonly
                  disabled
                  color="primary"
                />
                <v-switch
                  v-else
                  v-model="formData.settings!.directoryLdap!.enabled"
                  :label="t('domain.directoryLdap.enabled')"
                  color="primary"
                />
              </v-col>

              <v-col cols="12" md="4">
                <v-switch
                  v-if="!isEditing"
                  :label="t('domain.directoryLdap.useSsl')"
                  :model-value="domain.settings?.directoryLdap?.useSsl ?? false"
                  readonly
                  disabled
                  color="primary"
                />
                <v-switch
                  v-else
                  v-model="formData.settings!.directoryLdap!.useSsl"
                  :label="t('domain.directoryLdap.useSsl')"
                  color="primary"
                />
              </v-col>

              <v-col cols="12" md="4">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.directoryLdap.port')"
                  :model-value="domain.settings?.directoryLdap?.port || 389"
                  readonly
                  variant="outlined"
                  density="comfortable"
                  type="number"
                />
                <v-text-field
                  v-else
                  v-model.number="formData.settings!.directoryLdap!.port"
                  :label="t('domain.directoryLdap.port')"
                  variant="outlined"
                  density="comfortable"
                  type="number"
                  min="1"
                  max="65535"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.directoryLdap.host')"
                  :model-value="domain.settings?.directoryLdap?.host || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.directoryLdap!.host"
                  :label="t('domain.directoryLdap.host')"
                  :placeholder="t('domain.directoryLdap.hostPlaceholder')"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.directoryLdap.baseDn')"
                  :model-value="domain.settings?.directoryLdap?.baseDn || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.directoryLdap!.baseDn"
                  :label="t('domain.directoryLdap.baseDn')"
                  :placeholder="t('domain.directoryLdap.baseDnPlaceholder')"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.directoryLdap.bindUsername')"
                  :model-value="domain.settings?.directoryLdap?.bindUsername || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.directoryLdap!.bindUsername"
                  :label="t('domain.directoryLdap.bindUsername')"
                  :placeholder="t('domain.directoryLdap.bindUsernamePlaceholder')"
                  variant="outlined"
                  density="comfortable"
                  autocomplete="off"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.directoryLdap.bindPassword')"
                  :model-value="domain.settings?.directoryLdap?.bindPassword ? '••••••••' : '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                  type="password"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.directoryLdap!.bindPassword"
                  :label="t('domain.directoryLdap.bindPassword')"
                  :hint="t('domain.directoryLdap.bindPasswordHint')"
                  persistent-hint
                  variant="outlined"
                  density="comfortable"
                  type="password"
                  autocomplete="new-password"
                />
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>

        <!-- Backup Info Card -->
        <DomainDirectorySyncCard v-if="domain" class="mb-4" />
        <DomainBackupCard v-if="domain" :domain-name="domain.name" />
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
