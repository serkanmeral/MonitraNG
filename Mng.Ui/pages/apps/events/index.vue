<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { HubConnection, HubConnectionBuilder, LogLevel, HttpTransportType } from '@microsoft/signalr';
import { useAuthStore } from '@/stores/auth';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { BellIcon, CheckIcon, XIcon } from 'vue-tabler-icons';

const authStore = useAuthStore();
const config = useRuntimeConfig();

const page = {
  title: 'Event Mesajları',
};

const breadcrumbs = [
  {
    text: 'Ana Sayfa',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Event Mesajları',
    disabled: true,
    href: '#',
  },
];

const connection = ref<HubConnection | null>(null);
const isConnected = ref(false);
const isConnecting = ref(false);
const connectionError = ref<string | null>(null);
const messages = ref<Array<{
  id: string;
  routingKey: string;
  message: any;
  timestamp: Date;
  type: 'user' | 'group' | 'system' | 'data' | 'unknown';
}>>([]);

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

const getEventTypeLabel = (type: string) => {
  switch (type) {
    case 'user':
      return 'Kullanıcı';
    case 'group':
      return 'Grup';
    case 'system':
      return 'Sistem';
    case 'data':
      return 'Veri';
    default:
      return 'Bilinmeyen';
  }
};

const formatTimestamp = (date: Date) => {
  return new Intl.DateTimeFormat('tr-TR', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(date);
};

const connectToHub = async () => {
  if (connection.value?.state === 'Connected') {
    return;
  }

  isConnecting.value = true;
  connectionError.value = null;

  try {
    const token = authStore.accessToken;
    if (!token) {
      throw new Error('Access token bulunamadı. Lütfen tekrar giriş yapın.');
    }

    // Hub URL belirleme
    // Development'ta direkt Hub URL'ini HTTP olarak kullan (SSL sertifika hatası önlemek için)
    // Production'da gateway URL üzerinden HTTPS kullanılacak
    let hubBaseUrl: string;
    
    if (process.env.NODE_ENV === 'development') {
      // Development: Direkt Hub URL'ini HTTP olarak kullan (gateway bypass)
      // Bu, SSL sertifika hatasını önler
      hubBaseUrl = config.public.hubUrl || 'http://localhost:5020';
    } else {
      // Production: Gateway URL varsa onu kullan, yoksa direkt Hub URL'i
      hubBaseUrl = config.public.gatewayUrl 
        ? `${config.public.gatewayUrl}/hub`
        : (config.public.hubUrl || 'http://localhost:5020');
    }
    
    // Use query string for token (more compatible with SignalR negotiation)
    const connectionUrl = `${hubBaseUrl}/ws?access_token=${encodeURIComponent(token)}`;

    const hubConnection = new HubConnectionBuilder()
      .withUrl(connectionUrl, {
        skipNegotiation: false, // Use negotiation endpoint
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling // Fallback transport
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.previousRetryCount < 3) {
            return 2000; // 2 saniye
          }
          return 5000; // 5 saniye
        },
      })
      .configureLogging(process.env.NODE_ENV === 'development' ? LogLevel.Warning : LogLevel.Error)
      .build();

    // Message handler
    hubConnection.on('ReceiveMessage', (data: { routingKey: string; message: any; timestamp: string }) => {
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
    });

    // Connection state handlers
    hubConnection.onclose((error) => {
      isConnected.value = false;
      if (error) {
        connectionError.value = `Bağlantı kapatıldı: ${error.message}`;
      }
    });

    await hubConnection.start();
    connection.value = hubConnection;
    isConnected.value = true;
    connectionError.value = null;
  } catch (error: any) {
    connectionError.value = error.message || 'Bağlantı hatası oluştu.';
    isConnected.value = false;
  } finally {
    isConnecting.value = false;
  }
};

const disconnectFromHub = async () => {
  if (connection.value) {
    try {
      await connection.value.stop();
      connection.value = null;
      isConnected.value = false;
    } catch (error) {
      // Hata önemli değil, sessizce devam et
    }
  }
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
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

  <v-card elevation="10">
    <v-card-item>
      <div class="d-flex justify-space-between align-center mb-4">
        <div>
          <h3 class="text-h5 mb-1">Event Mesajları</h3>
          <p class="text-subtitle-1 text-medium-emphasis">
            RabbitMQ üzerinden gelen grup, kullanıcı ve veri CRUD işlemlerinin gerçek zamanlı bildirimleri
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
            Bağlan
          </v-btn>
          <v-btn
            v-if="isConnected"
            color="error"
            variant="flat"
            @click="disconnectFromHub"
          >
            <XIcon class="mr-2" size="20" />
            Bağlantıyı Kes
          </v-btn>
          <v-btn
            color="secondary"
            variant="outlined"
            @click="clearMessages"
            :disabled="messages.length === 0"
          >
            Mesajları Temizle
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
            <strong v-if="isConnected">Bağlı</strong>
            <strong v-else-if="isConnecting">Bağlanıyor...</strong>
            <strong v-else-if="connectionError">Bağlantı Hatası</strong>
            <strong v-else>Bağlantı Yok</strong>
            <span v-if="connectionError" class="ml-2">{{ connectionError }}</span>
          </div>
        </div>
      </v-alert>

      <!-- Messages List -->
      <div v-if="messages.length === 0" class="text-center py-8">
        <BellIcon size="48" class="mb-4 text-medium-emphasis" />
        <p class="text-h6 text-medium-emphasis">Henüz mesaj yok</p>
        <p class="text-body-2 text-medium-emphasis">
          Grup, kullanıcı veya veri işlemleri yapıldığında burada görünecek
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
              {{ getEventTypeLabel(msg.type) }}
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
                  <span class="text-caption">Detayları Gör</span>
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

