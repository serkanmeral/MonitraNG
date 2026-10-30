<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { useDomain, type Domain, type UpdateDomainRequest, type DirectoryLdapSettings, type DirectoryPrivilegeSettings, type DomainMailSettings, type DomainPasswordSettings } from '@/composables/useDomain';
import { useGroupStore } from '@/stores/apps/group';
import { fetchFromMngKeeper } from '@/services/apiService';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DomainBackupCard from '@/components/apps/domain/DomainBackupCard.vue';
import DomainDirectorySyncCard from '@/components/apps/domain/DomainDirectorySyncCard.vue';
import { SettingsIcon, RefreshIcon, EditIcon, ShieldIcon, ServerIcon, MailIcon, LockIcon } from 'vue-tabler-icons';

const emptyDirectoryLdap = (): DirectoryLdapSettings => ({
  enabled: false,
  host: '',
  port: 389,
  useSsl: false,
  baseDn: '',
  bindUsername: '',
  bindPassword: '',
});

const mailSecureModes = ['Auto', 'StartTls', 'SslOnConnect', 'None'] as const;

const emptyDomainMail = (): DomainMailSettings => ({
  enabled: false,
  host: '',
  port: 587,
  username: '',
  password: '',
  enableSsl: true,
  secureSocketMode: 'Auto',
  fromEmail: '',
  fromName: '',
  passwordConfigured: false,
});

const mapDomainMail = (mail?: DomainMailSettings | null): DomainMailSettings => ({
  enabled: mail?.enabled ?? false,
  host: mail?.host || '',
  port: mail?.port && mail.port > 0 ? mail.port : 587,
  username: mail?.username || '',
  password: '',
  enableSsl: mail?.enableSsl ?? true,
  secureSocketMode: mail?.secureSocketMode || 'Auto',
  fromEmail: mail?.fromEmail || '',
  fromName: mail?.fromName || '',
  passwordConfigured: mail?.passwordConfigured ?? false,
});

const emptyDomainPassword = (): DomainPasswordSettings => ({
  mode: 'keycloak',
  personnelDn: '',
  resetBindUsername: '',
  resetBindPassword: '',
  serverCertificateThumbprint: '',
  resetBindPasswordConfigured: false,
});

const mapDomainPassword = (password?: DomainPasswordSettings | null): DomainPasswordSettings => ({
  mode: password?.mode === 'keycloak-and-ad' ? 'keycloak-and-ad' : 'keycloak',
  personnelDn: password?.personnelDn || '',
  resetBindUsername: password?.resetBindUsername || '',
  resetBindPassword: '',
  serverCertificateThumbprint: password?.serverCertificateThumbprint || '',
  resetBindPasswordConfigured: password?.resetBindPasswordConfigured ?? false,
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
  logoDark: '',
  logoLight: '',
  logoUrl: '',
  settings: {
    maxUsers: 100,
    maxAssets: 1000,
    maxFileSizeBytes: 0,
    enableMqtt: true,
    directoryPrivileges: mapManagerPrivileges(null),
    directoryLdap: emptyDirectoryLdap(),
    password: emptyDomainPassword(),
    mail: emptyDomainMail(),
  },
});

const maxFileSizeMb = computed({
  get(): number | null {
    const bytes = formData.value.settings?.maxFileSizeBytes ?? 0;
    return bytes > 0 ? Math.round(bytes / (1024 * 1024)) : null;
  },
  set(mb: number | null) {
    if (!formData.value.settings) return;
    const value = Number(mb);
    formData.value.settings.maxFileSizeBytes = Number.isFinite(value) && value > 0
      ? Math.round(value * 1024 * 1024)
      : 0;
  },
});

const logoDarkPreview = ref<string | null>(null);
const logoLightPreview = ref<string | null>(null);
const logoDarkFileInput = ref<HTMLInputElement | null>(null);
const logoLightFileInput = ref<HTMLInputElement | null>(null);
const logoDarkError = ref<string | null>(null);
const logoLightError = ref<string | null>(null);

const darkLogoOf = (value?: { logoDark?: string; logo?: string } | null) =>
  value?.logoDark?.trim() || value?.logo?.trim() || '';

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
    
    const darkLogo = darkLogoOf(domainData);
    logoDarkPreview.value = darkLogo || domainData.logoUrl || null;
    logoLightPreview.value = domainData.logoLight || null;
    
    // Populate form with current domain data
    formData.value = {
      displayName: domainData.displayName || '',
      discoveryRootLabel: domainData.discoveryRootLabel || '',
      relatedPersonPhone: domainData.relatedPersonPhone || '',
      logo: darkLogo,
      logoDark: darkLogo,
      logoLight: domainData.logoLight || '',
      logoUrl: domainData.logoUrl || '',
      settings: {
        maxUsers: domainData.settings?.maxUsers || 100,
        maxAssets: domainData.settings?.maxAssets || 1000,
        enableMqtt: domainData.settings?.enableMqtt ?? true,
        directoryPrivileges: mapManagerPrivileges(domainData.settings?.directoryPrivileges),
        directoryLdap: mapDirectoryLdap(domainData.settings?.directoryLdap),
        password: mapDomainPassword(domainData.settings?.password),
        mail: mapDomainMail(domainData.settings?.mail),
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
      
      const darkLogo = darkLogoOf(updatedDomain);
      logoDarkPreview.value = darkLogo || updatedDomain.logoUrl || null;
      logoLightPreview.value = updatedDomain.logoLight || null;
      
      isEditing.value = false;
      authStore.domainInfo = updatedDomain;
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

const onLogoFile = (event: Event, slot: 'dark' | 'light') => {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  if (file) readLogoFile(file, slot);
};

const readLogoFile = (file: File, slot: 'dark' | 'light') => {
  const errorRef = slot === 'dark' ? logoDarkError : logoLightError;
  errorRef.value = null;

  const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp', 'image/gif'];
  if (!allowedTypes.includes(file.type)) {
    errorRef.value = t('domain.logo.errors.invalidFormat');
    return;
  }

  const maxSize = 2 * 1024 * 1024;
  if (file.size > maxSize) {
    errorRef.value = t('domain.logo.errors.maxSize');
    return;
  }

  const reader = new FileReader();
  reader.onload = (e) => {
    const result = e.target?.result as string;
    if (!result) return;
    if (slot === 'dark') {
      formData.value.logoDark = result;
      formData.value.logo = result;
      logoDarkPreview.value = result;
    } else {
      formData.value.logoLight = result;
      logoLightPreview.value = result;
    }
    errorRef.value = null;
  };
  reader.onerror = () => {
    errorRef.value = t('domain.logo.errors.readError');
  };
  reader.readAsDataURL(file);
};

const clearLogoSlot = (slot: 'dark' | 'light') => {
  if (slot === 'dark') {
    formData.value.logoDark = '';
    formData.value.logo = '';
    logoDarkPreview.value = null;
    logoDarkError.value = null;
    if (logoDarkFileInput.value) logoDarkFileInput.value.value = '';
  } else {
    formData.value.logoLight = '';
    logoLightPreview.value = null;
    logoLightError.value = null;
    if (logoLightFileInput.value) logoLightFileInput.value.value = '';
  }
};

// Cancel editing
const cancelEdit = () => {
  if (!domain.value) return;
  
  const darkLogo = darkLogoOf(domain.value);
  logoDarkPreview.value = darkLogo || domain.value.logoUrl || null;
  logoLightPreview.value = domain.value.logoLight || null;
  
  // Reset form to original values
  formData.value = {
    displayName: domain.value.displayName || '',
    discoveryRootLabel: domain.value.discoveryRootLabel || '',
    relatedPersonPhone: domain.value.relatedPersonPhone || '',
    logo: darkLogo,
    logoDark: darkLogo,
    logoLight: domain.value.logoLight || '',
    logoUrl: domain.value.logoUrl || '',
    settings: {
      maxUsers: domain.value.settings?.maxUsers || 100,
      maxAssets: domain.value.settings?.maxAssets || 1000,
      enableMqtt: domain.value.settings?.enableMqtt ?? true,
      maxFileSizeBytes: domain.value.settings?.maxFileSizeBytes || 0,
      directoryPrivileges: mapManagerPrivileges(domain.value.settings?.directoryPrivileges),
      directoryLdap: mapDirectoryLdap(domain.value.settings?.directoryLdap),
      password: mapDomainPassword(domain.value.settings?.password),
      mail: mapDomainMail(domain.value.settings?.mail),
    },
  };
  
  logoDarkError.value = null;
  logoLightError.value = null;
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
                class="mr-2"
                prepend-icon="mdi-email-outline"
                variant="outlined"
                size="small"
                to="/apps/domain/mail-log"
              >
                {{ t('domain.buttons.mailLog') }}
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

              <v-col cols="12" md="6">
                <div class="text-subtitle-2 mb-1">{{ t('domain.fields.logoDark') }}</div>
                <p class="text-caption text-medium-emphasis mb-2">{{ t('domain.logo.darkHint') }}</p>
                <div v-if="logoDarkPreview" class="domain-logo-well domain-logo-well--dark">
                  <v-img :src="logoDarkPreview" max-height="88" contain />
                </div>
                <span v-else class="text-medium-emphasis">{{ t('domain.logo.noImage') }}</span>
                <div v-if="isEditing" class="d-flex align-center ga-2 mt-2">
                  <input
                    ref="logoDarkFileInput"
                    type="file"
                    accept="image/jpeg,image/jpg,image/png,image/webp,image/gif"
                    class="d-none"
                    @change="onLogoFile($event, 'dark')"
                  />
                  <v-btn variant="outlined" color="primary" size="small" @click="logoDarkFileInput?.click()">
                    {{ t('domain.buttons.uploadLogo') }}
                  </v-btn>
                  <v-btn
                    v-if="formData.logoDark"
                    variant="outlined"
                    color="error"
                    size="small"
                    @click="clearLogoSlot('dark')"
                  >
                    {{ t('domain.buttons.removeLogo') }}
                  </v-btn>
                </div>
                <v-alert v-if="logoDarkError" type="error" variant="tonal" density="compact" class="mt-2">
                  {{ logoDarkError }}
                </v-alert>
              </v-col>

              <v-col cols="12" md="6">
                <div class="text-subtitle-2 mb-1">{{ t('domain.fields.logoLight') }}</div>
                <p class="text-caption text-medium-emphasis mb-2">{{ t('domain.logo.lightHint') }}</p>
                <div v-if="logoLightPreview" class="domain-logo-well domain-logo-well--light">
                  <v-img :src="logoLightPreview" max-height="88" contain />
                </div>
                <span v-else class="text-medium-emphasis">{{ t('domain.logo.noImage') }}</span>
                <div v-if="isEditing" class="d-flex align-center ga-2 mt-2">
                  <input
                    ref="logoLightFileInput"
                    type="file"
                    accept="image/jpeg,image/jpg,image/png,image/webp,image/gif"
                    class="d-none"
                    @change="onLogoFile($event, 'light')"
                  />
                  <v-btn variant="outlined" color="primary" size="small" @click="logoLightFileInput?.click()">
                    {{ t('domain.buttons.uploadLogo') }}
                  </v-btn>
                  <v-btn
                    v-if="formData.logoLight"
                    variant="outlined"
                    color="error"
                    size="small"
                    @click="clearLogoSlot('light')"
                  >
                    {{ t('domain.buttons.removeLogo') }}
                  </v-btn>
                </div>
                <v-alert v-if="logoLightError" type="error" variant="tonal" density="compact" class="mt-2">
                  {{ logoLightError }}
                </v-alert>
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
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.fields.maxFileSize')"
                  :model-value="domain.settings?.maxFileSizeBytes ? Math.round(domain.settings.maxFileSizeBytes / (1024 * 1024)) : ''"
                  :hint="t('domain.fields.maxFileSizeHint')"
                  persistent-hint
                  readonly
                  variant="outlined"
                  density="comfortable"
                  type="number"
                />
                <v-text-field
                  v-else
                  v-model.number="maxFileSizeMb"
                  :label="t('domain.fields.maxFileSize')"
                  :hint="t('domain.fields.maxFileSizeHint')"
                  persistent-hint
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

        <!-- Directory password store -->
        <v-card elevation="10" class="mb-4">
          <v-card-title class="d-flex align-center">
            <LockIcon size="20" class="mr-2" />
            {{ t('domain.cards.directoryPassword') }}
          </v-card-title>
          <v-divider />
          <v-card-text>
            <p class="text-body-2 text-medium-emphasis mb-4">
              {{ t('domain.directoryPassword.description') }}
            </p>
            <v-row>
              <v-col cols="12" md="6">
                <v-select
                  v-if="!isEditing"
                  :label="t('domain.directoryPassword.mode')"
                  :model-value="domain.settings?.password?.mode || 'keycloak'"
                  :items="[
                    { title: t('domain.directoryPassword.modeKeycloak'), value: 'keycloak' },
                    { title: t('domain.directoryPassword.modeKeycloakAndAd'), value: 'keycloak-and-ad' },
                  ]"
                  readonly
                  disabled
                  variant="outlined"
                  density="comfortable"
                />
                <v-select
                  v-else
                  v-model="formData.settings!.password!.mode"
                  :label="t('domain.directoryPassword.mode')"
                  :items="[
                    { title: t('domain.directoryPassword.modeKeycloak'), value: 'keycloak' },
                    { title: t('domain.directoryPassword.modeKeycloakAndAd'), value: 'keycloak-and-ad' },
                  ]"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.directoryPassword.personnelDn')"
                  :model-value="domain.settings?.password?.personnelDn || ''"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.password!.personnelDn"
                  :label="t('domain.directoryPassword.personnelDn')"
                  :placeholder="t('domain.directoryPassword.personnelDnPlaceholder')"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.directoryPassword.resetBindUsername')"
                  :model-value="domain.settings?.password?.resetBindUsername || ''"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.password!.resetBindUsername"
                  :label="t('domain.directoryPassword.resetBindUsername')"
                  :placeholder="t('domain.directoryPassword.resetBindUsernamePlaceholder')"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.directoryPassword.resetBindPassword')"
                  :model-value="domain.settings?.password?.resetBindPasswordConfigured ? '********' : ''"
                  readonly
                  type="password"
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.password!.resetBindPassword"
                  :label="t('domain.directoryPassword.resetBindPassword')"
                  :hint="t('domain.directoryPassword.resetBindPasswordHint')"
                  persistent-hint
                  type="password"
                  autocomplete="new-password"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>
              <v-col cols="12">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.directoryPassword.thumbprint')"
                  :model-value="domain.settings?.password?.serverCertificateThumbprint || ''"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.password!.serverCertificateThumbprint"
                  :label="t('domain.directoryPassword.thumbprint')"
                  :hint="t('domain.directoryPassword.thumbprintHint')"
                  persistent-hint
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>

        <!-- Domain SMTP -->
        <v-card elevation="10" class="mb-4">
          <v-card-title class="d-flex align-center">
            <MailIcon size="20" class="mr-2" />
            {{ t('domain.cards.mail') }}
          </v-card-title>

          <v-divider />

          <v-card-text>
            <p class="text-body-2 text-medium-emphasis mb-4">
              {{ t('domain.mail.description') }}
            </p>

            <v-row>
              <v-col cols="12" md="4">
                <v-switch
                  v-if="!isEditing"
                  :label="t('domain.mail.enabled')"
                  :model-value="domain.settings?.mail?.enabled ?? false"
                  readonly
                  disabled
                  color="primary"
                />
                <v-switch
                  v-else
                  v-model="formData.settings!.mail!.enabled"
                  :label="t('domain.mail.enabled')"
                  color="primary"
                />
              </v-col>

              <v-col cols="12" md="4">
                <v-switch
                  v-if="!isEditing"
                  :label="t('domain.mail.enableSsl')"
                  :model-value="domain.settings?.mail?.enableSsl ?? true"
                  readonly
                  disabled
                  color="primary"
                />
                <v-switch
                  v-else
                  v-model="formData.settings!.mail!.enableSsl"
                  :label="t('domain.mail.enableSsl')"
                  color="primary"
                />
              </v-col>

              <v-col cols="12" md="4">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.mail.port')"
                  :model-value="domain.settings?.mail?.port || 587"
                  readonly
                  variant="outlined"
                  density="comfortable"
                  type="number"
                />
                <v-text-field
                  v-else
                  v-model.number="formData.settings!.mail!.port"
                  :label="t('domain.mail.port')"
                  variant="outlined"
                  density="comfortable"
                  type="number"
                  min="1"
                  max="65535"
                />
              </v-col>

              <v-col cols="12" md="8">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.mail.host')"
                  :model-value="domain.settings?.mail?.host || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.mail!.host"
                  :label="t('domain.mail.host')"
                  :placeholder="t('domain.mail.hostPlaceholder')"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="4">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.mail.secureSocketMode')"
                  :model-value="domain.settings?.mail?.secureSocketMode || 'Auto'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-select
                  v-else
                  v-model="formData.settings!.mail!.secureSocketMode"
                  :items="mailSecureModes"
                  :label="t('domain.mail.secureSocketMode')"
                  variant="outlined"
                  density="comfortable"
                  hide-details="auto"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.mail.username')"
                  :model-value="domain.settings?.mail?.username || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.mail!.username"
                  :label="t('domain.mail.username')"
                  variant="outlined"
                  density="comfortable"
                  autocomplete="off"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.mail.password')"
                  :model-value="domain.settings?.mail?.passwordConfigured ? '••••••••' : '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                  type="password"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.mail!.password"
                  :label="t('domain.mail.password')"
                  :hint="t('domain.mail.passwordHint')"
                  persistent-hint
                  variant="outlined"
                  density="comfortable"
                  type="password"
                  autocomplete="new-password"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.mail.fromEmail')"
                  :model-value="domain.settings?.mail?.fromEmail || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.mail!.fromEmail"
                  :label="t('domain.mail.fromEmail')"
                  :placeholder="t('domain.mail.fromEmailPlaceholder')"
                  variant="outlined"
                  density="comfortable"
                />
              </v-col>

              <v-col cols="12" md="6">
                <v-text-field
                  v-if="!isEditing"
                  :label="t('domain.mail.fromName')"
                  :model-value="domain.settings?.mail?.fromName || '-'"
                  readonly
                  variant="outlined"
                  density="comfortable"
                />
                <v-text-field
                  v-else
                  v-model="formData.settings!.mail!.fromName"
                  :label="t('domain.mail.fromName')"
                  variant="outlined"
                  density="comfortable"
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

<style scoped>
.domain-logo-well {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 104px;
  padding: 12px;
  border-radius: 8px;
  border: 1px solid rgba(0, 0, 0, 0.08);
}
.domain-logo-well--dark {
  background: #2a3447;
}
.domain-logo-well--light {
  background: #ffffff;
}
</style>
