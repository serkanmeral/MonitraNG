<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useLocaleStore } from '@/stores/locale';
import { useHubStore } from '@/stores/hub';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { BellIcon, CheckIcon, XIcon } from 'vue-tabler-icons';

const authStore = useAuthStore();
const localeStore = useLocaleStore();
const hubStore = useHubStore();

const messages = ref<Array<{
  id: string;
  routingKey: string;
  message: any;
  timestamp: Date;
  type: 'user' | 'group' | 'system' | 'data' | 'unknown';
}>>([]);

// Hub store'dan connection state'lerini al
const isConnected = computed(() => hubStore.connected);
const isConnecting = computed(() => hubStore.connecting);
const connectionError = computed(() => hubStore.error);

// Subscription ID
const subscriptionId = 'events-page';

const getEventType = (routingKey: string): 'user' | 'group' | 'system' | 'data' | 'unknown' => {
  if (routingKey.includes('user')) return 'user';
  if (routingKey.includes('group')) return 'group';
  if (routingKey.includes('system') || routingKey.includes('global')) return 'system';
  if (routingKey.includes('data') || routingKey.includes('datacreated') || routingKey.includes('dataupdated') || routingKey.includes('datadeleted') || routingKey.includes('datarestored')) return 'data';
  return 'unknown';
};

const getEventTypeColor = (type: string) => {
  switch (type) {
    case 'user':
      return 'primary';
    case 'group':
      return 'success';
    case 'system':
      return 'info';
    case 'data':
      return 'warning';
    default:
      return 'secondary';
  }
};

// getEventTypeLabel function removed - use $t('events.types.' + type) directly in template

const formatTimestamp = (date: Date) => {
  const localeMap: Record<string, string> = {
    tr: 'tr-TR',
    en: 'en-US',
    fr: 'fr-FR',
    ar: 'ar-SA',
    zh: 'zh-CN',
  };
  const locale = localeMap[localeStore.locale] || 'tr-TR';
  
  return new Intl.DateTimeFormat(locale, {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(date);
};

const connectToHub = async () => {
  console.log('[Events Page] connectToHub called', {
    hasSubscription: hubStore.hasSubscription(subscriptionId),
    subscriptionCount: hubStore.subscriptionCount
  });
  
  // Hub store'dan bağlantıyı başlat (eğer bağlı değilse)
  await hubStore.connectToHub();

  // Subscription zaten varsa, önce kaldır
  if (hubStore.hasSubscription(subscriptionId)) {
    console.log('[Events Page] Removing existing subscription');
    hubStore.unsubscribe(subscriptionId);
  }
  
  // Filter: Tüm mesajları kabul et (filtreleme yok)
  const filter = (data: { routingKey: string; message: any; timestamp: string }) => {
    return true; // Tüm mesajları kabul et
  };

  // Handler: Mesajları ekle
  const handler = (data: { routingKey: string; message: any; timestamp: string }) => {
    console.log('[Events Page] Handler called', {
      routingKey: data.routingKey,
      currentMessageCount: messages.value.length
    });
    
    const eventType = getEventType(data.routingKey);
    messages.value.unshift({
      id: `${Date.now()}-${Math.random()}`,
      routingKey: data.routingKey,
      message: data.message,
      timestamp: new Date(data.timestamp),
      type: eventType,
    });

    // Keep only last 100 messages
    if (messages.value.length > 100) {
      messages.value = messages.value.slice(0, 100);
    }
  };

  // Subscription'ı ekle
  const subscribed = hubStore.subscribe(subscriptionId, { filter, handler });
  console.log('[Events Page] Subscription added', {
    subscribed,
    subscriptionCount: hubStore.subscriptionCount
  });
};

const disconnectFromHub = async () => {
  // Subscription'ı kaldır
  hubStore.unsubscribe(subscriptionId);
};

const clearMessages = () => {
  messages.value = [];
};

onMounted(async () => {
  await connectToHub();
});

onUnmounted(async () => {
  await disconnectFromHub();
});
</script>

<template>
  <BaseBreadcrumb 
    :title="$t('events.title')" 
    :breadcrumbs="[
      {
        text: $t('events.breadcrumbs.home'),
        disabled: false,
        href: '/dashboards/analytical',
      },
      {
        text: $t('events.title'),
        disabled: true,
        href: '#',
      },
    ]"
  />

  <v-card elevation="10">
    <v-card-item>
      <div class="d-flex justify-space-between align-center mb-4">
        <div>
          <h3 class="text-h5 mb-1">{{ $t('events.title') }}</h3>
          <p class="text-subtitle-1 text-medium-emphasis">
            {{ $t('events.description') }}
          </p>
        </div>
        <div class="d-flex ga-2">
          <v-btn
            v-if="!isConnected && !isConnecting"
            color="primary"
            variant="flat"
            @click="connectToHub"
          >
            <BellIcon class="mr-2" size="20" />
            {{ $t('events.buttons.connect') }}
          </v-btn>
          <v-btn
            v-if="isConnected"
            color="error"
            variant="flat"
            @click="disconnectFromHub"
          >
            <XIcon class="mr-2" size="20" />
            {{ $t('events.buttons.disconnect') }}
          </v-btn>
          <v-btn
            color="secondary"
            variant="outlined"
            @click="clearMessages"
            :disabled="messages.length === 0"
          >
            {{ $t('events.buttons.clearMessages') }}
          </v-btn>
        </div>
      </div>

      <!-- Connection Status -->
      <v-alert
        :type="isConnected ? 'success' : connectionError ? 'error' : 'warning'"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        <div class="d-flex align-center">
          <BellIcon class="mr-2" size="20" />
          <div>
            <strong v-if="isConnected">{{ $t('events.status.connected') }}</strong>
            <strong v-else-if="isConnecting">{{ $t('events.status.connecting') }}</strong>
            <strong v-else-if="connectionError">{{ $t('events.status.error') }}</strong>
            <strong v-else>{{ $t('events.status.disconnected') }}</strong>
            <span v-if="connectionError" class="ml-2">{{ connectionError }}</span>
          </div>
        </div>
      </v-alert>

      <!-- Messages List -->
      <div v-if="messages.length === 0" class="text-center py-8">
        <BellIcon size="48" class="mb-4 text-medium-emphasis" />
        <p class="text-h6 text-medium-emphasis">{{ $t('events.empty.title') }}</p>
        <p class="text-body-2 text-medium-emphasis">
          {{ $t('events.empty.description') }}
        </p>
      </div>

      <v-list v-else class="border rounded-md">
        <v-list-item
          v-for="msg in messages"
          :key="msg.id"
          class="border-b"
        >
          <template v-slot:prepend>
            <v-chip
              :color="getEventTypeColor(msg.type)"
              size="small"
              class="mr-3"
            >
              {{ $t(`events.types.${msg.type}`) }}
            </v-chip>
          </template>

          <v-list-item-title class="text-body-1 font-weight-medium">
            {{ msg.routingKey }}
          </v-list-item-title>
          <v-list-item-subtitle class="text-caption text-medium-emphasis mt-1">
            {{ formatTimestamp(msg.timestamp) }}
          </v-list-item-subtitle>

          <template v-slot:append>
            <v-expansion-panels variant="accordion" class="mt-2">
              <v-expansion-panel>
                <v-expansion-panel-title>
                  <span class="text-caption">{{ $t('events.details.view') }}</span>
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <pre class="text-caption bg-grey-lighten-4 pa-3 rounded">{{ JSON.stringify(msg.message, null, 2) }}</pre>
                </v-expansion-panel-text>
              </v-expansion-panel>
            </v-expansion-panels>
          </template>
        </v-list-item>
      </v-list>
    </v-card-item>
  </v-card>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.border-b {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.border-b:last-child {
  border-bottom: none;
}
</style>

