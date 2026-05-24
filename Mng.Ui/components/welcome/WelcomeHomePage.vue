<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { ClockIcon } from 'vue-tabler-icons';

export interface WelcomeModuleLink {
  labelKey: string;
  to: string;
}

export interface WelcomeModuleCard {
  id: string;
  titleKey: string;
  descriptionKey: string;
  icon: string;
  color: string;
  links: WelcomeModuleLink[];
}

/** Tamamlanan modüller — yeni modül eklendikçe buraya kart ekleyin. */
const moduleCards: WelcomeModuleCard[] = [
  {
    id: 'task-manager',
    titleKey: 'welcome.modules.taskManager.title',
    descriptionKey: 'welcome.modules.taskManager.description',
    icon: 'mdi-clipboard-list-outline',
    color: 'primary',
    links: [
      {
        labelKey: 'welcome.modules.taskManager.linkWorkspace',
        to: '/apps/task-manager/workspace',
      },
      {
        labelKey: 'welcome.modules.taskManager.linkHub',
        to: '/apps/task-manager',
      },
    ],
  },
];

const authStore = useAuthStore();
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

const domainLabel = computed(() => authStore.domainName || authStore.userInfo?.domain_name || '');

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

onMounted(() => {
  clockTimer = setInterval(() => {
    currentTime.value = new Date();
  }, 60_000);
});

onUnmounted(() => {
  if (clockTimer) clearInterval(clockTimer);
});
</script>

<template>
  <div class="welcome-page">
    <v-container fluid class="hero-section pa-4 pa-md-6">
      <v-card class="hero-card pa-6 pa-md-8" elevation="0" rounded="xl">
        <v-row align="center">
          <v-col cols="12" md="8">
            <p class="text-overline text-medium-emphasis mb-2">
              MonitraNG
            </p>
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
              <v-chip v-if="domainLabel" color="primary" variant="tonal" size="small">
                <v-icon icon="mdi-domain" start size="16" />
                {{ $t('welcome.banner.domain') }}: {{ domainLabel }}
              </v-chip>
              <v-chip v-if="userLogin" variant="outlined" size="small">
                <v-icon icon="mdi-account-outline" start size="16" />
                {{ userLogin }}
              </v-chip>
              <v-chip :color="authStore.isAdmin ? 'error' : authStore.isManager ? 'warning' : 'secondary'" variant="tonal" size="small">
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
            </div>
          </v-col>
          <v-col cols="12" md="4" class="d-none d-md-flex justify-end align-center">
            <v-icon icon="mdi-view-dashboard-outline" size="96" class="hero-watermark" />
          </v-col>
        </v-row>
      </v-card>
    </v-container>

    <v-container fluid class="px-4 px-md-6 pb-8">
      <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-4">
        <h2 class="text-h5 font-weight-bold mb-0">
          {{ $t('welcome.modules.title') }}
        </h2>
        <p class="text-body-2 text-medium-emphasis mb-0">
          {{ $t('welcome.modules.hint') }}
        </p>
      </div>

      <v-row>
        <v-col
          v-for="mod in moduleCards"
          :key="mod.id"
          cols="12"
          sm="6"
          lg="4"
        >
          <v-card class="module-card h-100 rounded-xl" elevation="2">
            <v-card-text class="pa-6">
              <v-avatar :color="mod.color" variant="tonal" size="52" rounded="lg" class="mb-4">
                <v-icon :icon="mod.icon" size="28" />
              </v-avatar>
              <div class="text-h6 font-weight-bold mb-2">
                {{ $t(mod.titleKey) }}
              </div>
              <p class="text-body-2 text-medium-emphasis mb-0">
                {{ $t(mod.descriptionKey) }}
              </p>
            </v-card-text>
            <v-card-actions class="px-6 pb-6 pt-0 flex-wrap gap-2">
              <v-btn
                v-for="(link, idx) in mod.links"
                :key="`${mod.id}-${idx}`"
                :color="idx === 0 ? mod.color : undefined"
                :variant="idx === 0 ? 'flat' : 'tonal'"
                rounded="lg"
                class="text-none"
                :to="link.to"
              >
                {{ $t(link.labelKey) }}
                <v-icon icon="mdi-chevron-right" end />
              </v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
      </v-row>

      <v-alert
        type="info"
        variant="tonal"
        class="mt-6 rounded-lg"
        density="comfortable"
      >
        {{ $t('welcome.modules.comingSoon') }}
      </v-alert>
    </v-container>
  </div>
</template>

<style scoped>
.welcome-page {
  min-height: 100%;
  background: rgb(var(--v-theme-background));
}

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

.module-card {
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.module-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08) !important;
}
</style>
