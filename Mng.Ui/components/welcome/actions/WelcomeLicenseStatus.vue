<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { fetchFromMngKeeper } from '@/services/apiService';

interface LicenseInfo {
  isValid?: boolean;
  isExpired?: boolean;
  expiresAt?: string;
}

interface UserCountInfo {
  activeUserCount?: number;
  maxUsers?: number | null;
  canCreateUser?: boolean;
}

const authStore = useAuthStore();

const loading = ref(false);
const visible = ref(false);
const messageKey = ref('');
const messageParams = ref<Record<string, unknown>>({});
const severity = ref<'warning' | 'error'>('warning');
const linkTo = '/apps/domain';

onMounted(async () => {
  if (!authStore.isAdmin || !authStore.domainName) return;

  loading.value = true;
  try {
    const domainName = authStore.domainName;
    const [licenseRaw, userCountRaw] = await Promise.allSettled([
      fetchFromMngKeeper(`license/${domainName}`),
      fetchFromMngKeeper(`license/${domainName}/user-count`),
    ]);

    const license = licenseRaw.status === 'fulfilled' ? (licenseRaw.value as LicenseInfo) : null;
    const userCount =
      userCountRaw.status === 'fulfilled' ? (userCountRaw.value as UserCountInfo) : null;

    if (license?.isExpired || license?.isValid === false) {
      visible.value = true;
      severity.value = 'error';
      messageKey.value = 'welcome.actions.license.expired';
      return;
    }

    if (userCount?.maxUsers && userCount.activeUserCount != null) {
      const ratio = userCount.activeUserCount / userCount.maxUsers;
      if (ratio >= 0.9 || userCount.canCreateUser === false) {
        visible.value = true;
        severity.value = ratio >= 1 ? 'error' : 'warning';
        messageKey.value = 'welcome.actions.license.userLimit';
        messageParams.value = {
          active: userCount.activeUserCount,
          max: userCount.maxUsers,
        };
        return;
      }
    }

    if (license?.expiresAt) {
      const expires = new Date(license.expiresAt);
      const daysLeft = Math.ceil((expires.getTime() - Date.now()) / (1000 * 60 * 60 * 24));
      if (daysLeft <= 14 && daysLeft >= 0) {
        visible.value = true;
        severity.value = daysLeft <= 7 ? 'error' : 'warning';
        messageKey.value = 'welcome.actions.license.expiring';
        messageParams.value = { days: daysLeft };
      }
    }
  } catch {
    visible.value = false;
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <v-card
    v-if="visible || loading"
    class="action-widget rounded-xl h-100"
    variant="outlined"
  >
    <v-card-text class="pa-4">
      <div class="d-flex align-center gap-2 mb-2">
        <v-icon icon="mdi-shield-key-outline" :color="severity" size="22" />
        <span class="text-subtitle-2 font-weight-bold">
          {{ $t('welcome.actions.license.title') }}
        </span>
      </div>
      <v-skeleton-loader v-if="loading" type="text" />
      <template v-else>
        <p class="text-body-2 mb-3">
          {{ $t(messageKey, messageParams) }}
        </p>
        <v-btn
          :color="severity"
          variant="tonal"
          size="small"
          rounded="lg"
          class="text-none"
          :to="linkTo"
        >
          {{ $t('welcome.actions.license.viewDomain') }}
          <v-icon icon="mdi-chevron-right" end />
        </v-btn>
      </template>
    </v-card-text>
  </v-card>
</template>
