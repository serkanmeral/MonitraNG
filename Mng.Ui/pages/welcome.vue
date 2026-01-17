<script setup lang="ts">
import { computed, ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { 
  ChartBarIcon, 
  DatabaseIcon, 
  UsersIcon, 
  AlertCircleIcon,
  ActivityIcon,
  ShieldCheckIcon,
  LayoutDashboardIcon,
  SettingsIcon,
  UserCircleIcon,
  FileTextIcon,
  TrendingUpIcon,
  ClockIcon
} from 'vue-tabler-icons';

definePageMeta({
  layout: 'default',
});

const authStore = useAuthStore();
const currentTime = ref(new Date());
const greetingMessage = computed(() => {
  const hour = currentTime.value.getHours();
  if (hour < 12) return 'welcome.greeting.morning';
  if (hour < 17) return 'welcome.greeting.afternoon';
  if (hour < 21) return 'welcome.greeting.evening';
  return 'welcome.greeting.night';
});

// Get user display name
const userDisplayName = computed(() => {
  if (!authStore.userInfo) return 'Kullanıcı';
  
  if (authStore.userInfo.given_name && authStore.userInfo.family_name) {
    return `${authStore.userInfo.given_name} ${authStore.userInfo.family_name}`;
  }
  
  const name = authStore.userInfo.name || authStore.userInfo.given_name || authStore.userInfo.preferred_username;
  if (name) return name;
  
  return authStore.userInfo.username || 'Kullanıcı';
});

// Format date and time
const formattedDateTime = computed(() => {
  const date = currentTime.value;
  const options: Intl.DateTimeFormatOptions = { 
    weekday: 'long', 
    year: 'numeric', 
    month: 'long', 
    day: 'numeric' 
  };
  return date.toLocaleDateString('tr-TR', options);
});

const formattedTime = computed(() => {
  return currentTime.value.toLocaleTimeString('tr-TR', { 
    hour: '2-digit', 
    minute: '2-digit' 
  });
});

// Quick stats data (mock data for now - can be replaced with real API calls)
const quickStats = ref([
  {
    title: 'welcome.quickStats.totalDataPoints',
    value: '12,458',
    icon: DatabaseIcon,
    color: 'primary',
    trend: '+12%',
    trendUp: true
  },
  {
    title: 'welcome.quickStats.activeDevices',
    value: '342',
    icon: ActivityIcon,
    color: 'success',
    trend: '+5%',
    trendUp: true
  },
  {
    title: 'welcome.quickStats.alertsToday',
    value: '8',
    icon: AlertCircleIcon,
    color: 'warning',
    trend: '-3',
    trendUp: false
  },
  {
    title: 'welcome.quickStats.systemHealth',
    value: '99.8%',
    icon: ShieldCheckIcon,
    color: 'info',
    trend: 'Excellent',
    trendUp: true
  }
]);

// Quick access links
const quickAccessLinks = ref([
  { 
    title: 'welcome.quickAccess.dashboard', 
    icon: LayoutDashboardIcon, 
    color: 'primary',
    route: '/dashboards/analytical'
  },
  { 
    title: 'welcome.quickAccess.datasets', 
    icon: DatabaseIcon, 
    color: 'info',
    route: '/apps/datasets'
  },
  { 
    title: 'welcome.quickAccess.users', 
    icon: UsersIcon, 
    color: 'success',
    route: '/apps/users'
  },
  { 
    title: 'welcome.quickAccess.reports', 
    icon: FileTextIcon, 
    color: 'warning',
    route: '#'
  },
  { 
    title: 'welcome.quickAccess.settings', 
    icon: SettingsIcon, 
    color: 'secondary',
    route: '/apps/profile'
  },
  { 
    title: 'welcome.quickAccess.profile', 
    icon: UserCircleIcon, 
    color: 'primary',
    route: '/apps/profile'
  }
]);

// Recent activity (mock data)
const recentActivities = ref([
  { id: 1, action: 'Yeni dataset oluşturuldu', time: '2 saat önce', type: 'create' },
  { id: 2, action: 'Kullanıcı profili güncellendi', time: '5 saat önce', type: 'update' },
  { id: 3, action: 'Rapor oluşturuldu', time: '1 gün önce', type: 'report' }
]);

// Update time every minute
onMounted(() => {
  setInterval(() => {
    currentTime.value = new Date();
  }, 60000);
});
</script>

<template>
  <div class="welcome-page">
    <!-- Hero Section -->
    <v-container fluid class="hero-section">
      <v-row>
        <v-col cols="12">
          <v-card 
            class="hero-card pa-8" 
            elevation="0"
            :style="{ background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' }"
          >
            <v-row align="center">
              <v-col cols="12" md="8">
                <div class="text-white">
                  <div class="text-h6 mb-2 font-weight-medium opacity-90">
                    {{ $t(greetingMessage) }}, {{ userDisplayName }}! 👋
                  </div>
                  <h1 class="text-h3 font-weight-bold mb-2">
                    {{ $t('welcome.title') }}
                  </h1>
                  <p class="text-h6 opacity-90 mb-4">
                    {{ $t('welcome.subtitle') }}
                  </p>
                  <div class="d-flex align-center gap-4 flex-wrap">
                    <div class="d-flex align-center">
                      <ClockIcon size="20" class="mr-2" />
                      <span class="text-body-1">{{ formattedDateTime }}</span>
                    </div>
                    <div class="text-body-1 font-weight-bold">
                      {{ formattedTime }}
                    </div>
                  </div>
                </div>
              </v-col>
              <v-col cols="12" md="4" class="text-right d-none d-md-flex justify-end">
                <div class="hero-decoration">
                  <TrendingUpIcon size="120" class="opacity-20" />
                </div>
              </v-col>
            </v-row>
          </v-card>
        </v-col>
      </v-row>
    </v-container>

    <!-- Main Content -->
    <v-container fluid class="mt-4">
      <!-- Quick Stats -->
      <v-row>
        <v-col cols="12">
          <h2 class="text-h5 font-weight-bold mb-4">{{ $t('welcome.quickStats.title') }}</h2>
        </v-col>
      </v-row>
      <v-row>
        <v-col 
          v-for="(stat, index) in quickStats" 
          :key="index"
          cols="12" 
          sm="6" 
          md="3"
        >
          <v-card 
            class="stat-card pa-6" 
            elevation="2"
            :class="`stat-card-${stat.color}`"
            hover
          >
            <div class="d-flex justify-space-between align-start mb-3">
              <v-avatar 
                :color="stat.color" 
                size="56" 
                variant="flat"
                class="stat-icon"
              >
                <component :is="stat.icon" size="28" />
              </v-avatar>
              <div class="text-end">
                <div 
                  class="text-caption font-weight-medium d-flex align-center justify-end"
                  :class="stat.trendUp ? 'text-success' : 'text-warning'"
                >
                  <TrendingUpIcon 
                    :size="16" 
                    :class="stat.trendUp ? '' : 'rotate-180'"
                    class="mr-1"
                  />
                  {{ stat.trend }}
                </div>
              </div>
            </div>
            <div class="text-h4 font-weight-bold mb-1">{{ stat.value }}</div>
            <div class="text-body-2 text-medium-emphasis">{{ $t(stat.title) }}</div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Quick Access & Recent Activity -->
      <v-row class="mt-4">
        <!-- Quick Access -->
        <v-col cols="12" md="8">
          <v-card class="pa-6" elevation="2">
            <h2 class="text-h5 font-weight-bold mb-4">{{ $t('welcome.quickAccess.title') }}</h2>
            <v-row>
              <v-col 
                v-for="(link, index) in quickAccessLinks" 
                :key="index"
                cols="6" 
                sm="4" 
                md="4"
              >
                <v-card
                  class="quick-access-card pa-4 text-center"
                  :class="`quick-access-${link.color}`"
                  hover
                  @click="$router.push(link.route)"
                  style="cursor: pointer;"
                >
                  <v-avatar 
                    :color="link.color" 
                    size="48" 
                    variant="flat"
                    class="mb-3"
                  >
                    <component :is="link.icon" size="24" />
                  </v-avatar>
                  <div class="text-body-2 font-weight-medium">{{ $t(link.title) }}</div>
                </v-card>
              </v-col>
            </v-row>
          </v-card>
        </v-col>

        <!-- Recent Activity -->
        <v-col cols="12" md="4">
          <v-card class="pa-6" elevation="2">
            <h2 class="text-h5 font-weight-bold mb-4">{{ $t('welcome.recentActivity.title') }}</h2>
            <div v-if="recentActivities.length > 0">
              <v-timeline 
                density="compact" 
                align="start"
                class="recent-activity-timeline"
              >
                <v-timeline-item
                  v-for="activity in recentActivities"
                  :key="activity.id"
                  size="small"
                  dot-color="primary"
                >
                  <div class="text-body-2 font-weight-medium mb-1">
                    {{ activity.action }}
                  </div>
                  <div class="text-caption text-medium-emphasis">
                    {{ activity.time }}
                  </div>
                </v-timeline-item>
              </v-timeline>
              <v-btn 
                variant="text" 
                color="primary" 
                class="mt-4"
                block
              >
                {{ $t('welcome.recentActivity.viewAll') }}
              </v-btn>
            </div>
            <div v-else class="text-center py-8">
              <AlertCircleIcon size="48" class="text-medium-emphasis mb-2" />
              <div class="text-body-2 text-medium-emphasis">
                {{ $t('welcome.recentActivity.noActivity') }}
              </div>
            </div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Action Buttons -->
      <v-row class="mt-4 mb-6">
        <v-col cols="12" class="text-center">
          <v-btn
            color="primary"
            size="large"
            variant="flat"
            prepend-icon="mdi-view-dashboard"
            class="mr-4 mb-2"
            @click="$router.push('/dashboards/analytical')"
          >
            {{ $t('welcome.actions.goToDashboard') }}
          </v-btn>
          <v-btn
            color="secondary"
            size="large"
            variant="outlined"
            prepend-icon="mdi-compass"
            class="mb-2"
            @click="$router.push('/')"
          >
            {{ $t('welcome.actions.exploreFeatures') }}
          </v-btn>
        </v-col>
      </v-row>
    </v-container>
  </div>
</template>

<style scoped>
.welcome-page {
  min-height: 100vh;
  background: #f5f7fa;
}

.hero-section {
  padding: 24px;
}

.hero-card {
  border-radius: 16px;
  overflow: hidden;
  position: relative;
}

.hero-decoration {
  opacity: 0.1;
}

.stat-card {
  border-radius: 12px;
  transition: all 0.3s ease;
  border-left: 4px solid transparent;
}

.stat-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 16px rgba(0, 0, 0, 0.1) !important;
}

.stat-card-primary {
  border-left-color: rgb(var(--v-theme-primary));
}

.stat-card-success {
  border-left-color: rgb(var(--v-theme-success));
}

.stat-card-warning {
  border-left-color: rgb(var(--v-theme-warning));
}

.stat-card-info {
  border-left-color: rgb(var(--v-theme-info));
}

.stat-icon {
  background: rgba(var(--v-theme-on-surface), 0.05) !important;
}

.quick-access-card {
  border-radius: 12px;
  transition: all 0.3s ease;
  border: 2px solid transparent;
}

.quick-access-card:hover {
  transform: translateY(-4px);
  border-color: rgba(var(--v-theme-primary), 0.3);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1) !important;
}

.quick-access-primary {
  border-color: rgba(var(--v-theme-primary), 0.1);
}

.quick-access-info {
  border-color: rgba(var(--v-theme-info), 0.1);
}

.quick-access-success {
  border-color: rgba(var(--v-theme-success), 0.1);
}

.quick-access-warning {
  border-color: rgba(var(--v-theme-warning), 0.1);
}

.quick-access-secondary {
  border-color: rgba(var(--v-theme-secondary), 0.1);
}

.recent-activity-timeline {
  padding: 0;
}

.rotate-180 {
  transform: rotate(180deg);
}

.opacity-20 {
  opacity: 0.2;
}

.opacity-90 {
  opacity: 0.9;
}

@media (max-width: 960px) {
  .hero-card {
    padding: 24px !important;
  }
  
  .hero-decoration {
    display: none;
  }
}

@media (max-width: 600px) {
  .welcome-page :deep(.v-container) {
    padding: 16px;
  }
  
  .stat-card,
  .quick-access-card {
    margin-bottom: 16px;
  }
}
</style>