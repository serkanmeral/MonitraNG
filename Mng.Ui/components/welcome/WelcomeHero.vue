<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import { useAppI18n } from '@/composables/useAppI18n';
import { resolveDomainLogoSrc } from '@/composables/useDomain';
import { ClockIcon } from 'vue-tabler-icons';

const authStore = useAuthStore();
const userStore = useUserStore();
const { locale } = useAppI18n();
const config = useRuntimeConfig();
const currentTime = ref(new Date());
let clockTimer: ReturnType<typeof setInterval> | null = null;

const greetingMessage = computed(() => {
  const hour = currentTime.value.getHours();
  if (hour < 12) return 'welcome.greeting.morning';
  if (hour < 17) return 'welcome.greeting.afternoon';
  if (hour < 21) return 'welcome.greeting.evening';
  return 'welcome.greeting.night';
});

const userDisplayName = computed(() => {
  if (!authStore.userInfo) return '';
  const u = authStore.userInfo;
  if (u.given_name && u.family_name) {
    return `${u.given_name} ${u.family_name}`;
  }
  return u.name || u.given_name || u.preferred_username || u.username || '';
});

const userLogin = computed(() => authStore.userInfo?.username || authStore.userInfo?.preferred_username || '');

const userEmail = computed(() => authStore.userInfo?.email || '');

const domainLabel = computed(
  () => authStore.domainInfo?.displayName || authStore.domainName || authStore.userInfo?.domain_name || '',
);

const domainLogoUrl = computed(() => resolveDomainLogoSrc(authStore.domainInfo));

const roleLabelKey = computed(() => {
  if (authStore.isAdmin) return 'welcome.banner.roleAdmin';
  if (authStore.isManager) return 'welcome.banner.roleManager';
  return 'welcome.banner.roleUser';
});

const groupsSummary = computed(() => {
  const groups = authStore.userGroups;
  if (!groups.length) return '';
  if (groups.length <= 3) return groups.join(', ');
  return `${groups.slice(0, 3).join(', ')} +${groups.length - 3}`;
});

const formattedDate = computed(() =>
  currentTime.value.toLocaleDateString('tr-TR', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  }),
);

const formattedTime = computed(() =>
  currentTime.value.toLocaleTimeString('tr-TR', {
    hour: '2-digit',
    minute: '2-digit',
  }),
);

const appVersion = computed(() => config.public.appVersion || '');

const lastLoginLabel = computed(() => {
  const uid = authStore.userInfo?.sub ?? '';
  const profile = uid ? userStore.getUserById(uid) : undefined;
  const raw = profile?.lastLoginAt;
  if (!raw) return '';
  try {
    const d = new Date(raw);
    if (Number.isNaN(d.getTime())) return '';
    return new Intl.DateTimeFormat(locale() === 'tr' ? 'tr-TR' : 'en-GB', {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(d);
  } catch {
    return '';
  }
});

onMounted(async () => {
  clockTimer = setInterval(() => {
    currentTime.value = new Date();
  }, 60_000);

  const uid = authStore.userInfo?.sub;
  if (uid && !userStore.getUserById(uid)?.lastLoginAt) {
    try {
      await userStore.fetchUserById(uid);
    } catch {
      /* optional profile field */
    }
  }
});

onUnmounted(() => {
  if (clockTimer) clearInterval(clockTimer);
});
</script>

<template>
  <v-container fluid class="hero-section pa-4 pa-md-6">
    <v-card class="hero-card pa-6 pa-md-8" elevation="0" rounded="xl">
      <v-row align="center">
        <v-col cols="12" md="8">
          <div class="d-flex align-center gap-3 mb-2">
            <v-avatar v-if="domainLogoUrl" size="40" rounded="lg">
              <v-img :src="domainLogoUrl" :alt="domainLabel" cover />
            </v-avatar>
            <p class="text-overline text-medium-emphasis mb-0">
              {{ domainLabel || 'MonitraNG' }}
            </p>
          </div>
          <p class="text-h6 font-weight-medium mb-1">
            {{ $t(greetingMessage) }}, {{ userDisplayName || $t('welcome.banner.guest') }}!
          </p>
          <h1 class="text-h4 text-md-h3 font-weight-bold mb-2">
            {{ $t('welcome.title') }}
          </h1>
          <p class="text-body-1 text-medium-emphasis mb-4">
            {{ $t('welcome.subtitle') }}
          </p>

          <div class="d-flex flex-wrap gap-2 mb-4">
            <v-chip v-if="authStore.domainName" color="primary" variant="tonal" size="small">
              <v-icon icon="mdi-domain" start size="16" />
              {{ $t('welcome.banner.domain') }}: {{ authStore.domainName }}
            </v-chip>
            <v-chip v-if="userLogin" variant="outlined" size="small">
              <v-icon icon="mdi-account-outline" start size="16" />
              {{ userLogin }}
            </v-chip>
            <v-chip
              :color="authStore.isAdmin ? 'error' : authStore.isManager ? 'warning' : 'secondary'"
              variant="tonal"
              size="small"
            >
              {{ $t(roleLabelKey) }}
            </v-chip>
            <v-chip v-if="appVersion" variant="outlined" size="small">
              v{{ appVersion }}
            </v-chip>
          </div>

          <div v-if="userEmail || groupsSummary" class="text-body-2 text-medium-emphasis mb-3">
            <span v-if="userEmail">{{ userEmail }}</span>
            <span v-if="userEmail && groupsSummary"> · </span>
            <span v-if="groupsSummary">
              {{ $t('welcome.banner.groups') }}: {{ groupsSummary }}
            </span>
          </div>

          <div class="d-flex align-center flex-wrap gap-3 text-body-2">
            <span class="d-flex align-center">
              <ClockIcon size="18" class="mr-1" />
              {{ formattedDate }}
            </span>
            <span class="font-weight-bold">{{ formattedTime }}</span>
            <span v-if="lastLoginLabel" class="text-medium-emphasis">
              · {{ $t('welcome.banner.lastLogin') }}: {{ lastLoginLabel }}
            </span>
          </div>
        </v-col>
        <v-col cols="12" md="4" class="d-none d-md-flex justify-end align-center">
          <v-icon icon="mdi-view-dashboard-outline" size="96" class="hero-watermark" />
        </v-col>
      </v-row>
    </v-card>
  </v-container>
</template>

<style scoped>
.hero-card {
  background: linear-gradient(
    135deg,
    rgba(var(--v-theme-primary), 0.12) 0%,
    rgba(var(--v-theme-surface), 1) 55%,
    rgba(var(--v-theme-secondary), 0.06) 100%
  );
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.hero-watermark {
  opacity: 0.12;
  color: rgb(var(--v-theme-primary));
}
</style>
