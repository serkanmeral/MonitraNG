<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import {
  fetchBlobFromDataGateway,
  fetchFromDataGateway,
  fetchFromMngKeeper,
} from '@/services/apiService';
import { consumeLoginWelcomePending } from '@/utils/loginWelcomeFlag';
import {
  LOGIN_WELCOME_DATASET,
  isLoginWelcomeImageExtension,
  messageNeedsGroupLookup,
  selectLoginWelcomeMessages,
  type LoginWelcomeMessage,
} from '@/utils/loginWelcome';

const auth = useAuthStore();
const queue = ref<LoginWelcomeMessage[]>([]);
const index = ref(0);
const imageUrl = ref<string | null>(null);
const imageLoading = ref(false);
const started = ref(false);
let objectUrl: string | null = null;
let imageTicket = 0;

const current = computed(() => queue.value[index.value] ?? null);
const hasNext = computed(() => index.value < queue.value.length - 1);

function revokeImage() {
  if (objectUrl) {
    URL.revokeObjectURL(objectUrl);
    objectUrl = null;
  }
  imageUrl.value = null;
}

function advance() {
  if (hasNext.value) {
    index.value += 1;
    return;
  }
  queue.value = [];
  index.value = 0;
  revokeImage();
}

async function loadGroupNames(): Promise<Map<string, string>> {
  const map = new Map<string, string>();
  try {
    let page = 1;
    let totalPages = 1;
    while (page <= totalPages && page <= 20) {
      const response = await fetchFromMngKeeper(`/group?page=${page}&pageSize=100`, 'GET');
      if (response?.IsSuccess === false) break;
      const groups = response?.groups || response?.Groups || [];
      totalPages = Number(response?.totalPages ?? response?.TotalPages ?? 1);
      if (Array.isArray(groups)) {
        for (const group of groups) {
          const id = String(group?.groupId || group?.GroupId || group?.id || '').trim();
          const name = String(group?.name || group?.Name || '').trim();
          if (id && name) map.set(id, name);
        }
      }
      page += 1;
    }
  } catch {
    // Messages that store group names still match the token.
  }
  return map;
}

async function loadImage(message: LoginWelcomeMessage | null) {
  const ticket = ++imageTicket;
  revokeImage();
  if (!message?.imagePath) return;
  if (message.imageExtension && !isLoginWelcomeImageExtension(message.imageExtension)) {
    if (!message.html) advance();
    return;
  }

  imageLoading.value = true;
  try {
    const meta = await fetchFromDataGateway(
      `/api/v1/files/metadata?filePath=${encodeURIComponent(message.imagePath)}`,
      'GET',
    );
    if (ticket !== imageTicket) return;
    const mime = String(meta?.mimeType || meta?.data?.mimeType || meta?.MimeType || '');
    if (mime && !mime.toLowerCase().startsWith('image/')) {
      if (!message.html) advance();
      return;
    }
    const blob = await fetchBlobFromDataGateway(
      `/api/v1/files/download?filePath=${encodeURIComponent(message.imagePath)}`,
    );
    if (ticket !== imageTicket) return;
    const type = (blob.type || mime).toLowerCase();
    if (type && !type.startsWith('image/')) {
      if (!message.html) advance();
      return;
    }
    objectUrl = URL.createObjectURL(blob);
    imageUrl.value = objectUrl;
  } catch {
    if (ticket === imageTicket && !message.html) advance();
  } finally {
    if (ticket === imageTicket) imageLoading.value = false;
  }
}

watch(current, (message) => {
  void loadImage(message);
});

async function maybeStart() {
  if (!import.meta.client || started.value || !auth.isAuthenticated) return;
  if (!consumeLoginWelcomePending()) return;
  started.value = true;
  try {
    const raw = await fetchFromDataGateway(
      `/api/v1/data/${LOGIN_WELCOME_DATASET}?limit=200&expand=false`,
      'GET',
    );
    const groups = auth.userGroups;
    const lookup = messageNeedsGroupLookup(raw, groups)
      ? { nameById: await loadGroupNames() }
      : null;
    queue.value = selectLoginWelcomeMessages(raw, groups, undefined, lookup).filter((message) => {
      if (message.html) return true;
      if (!message.imagePath) return false;
      return !message.imageExtension || isLoginWelcomeImageExtension(message.imageExtension);
    });
    index.value = 0;
  } catch {
    queue.value = [];
  }
}

onMounted(() => {
  void maybeStart();
});

watch(
  () => auth.isAuthenticated,
  () => {
    void maybeStart();
  },
);

onBeforeUnmount(() => {
  imageTicket += 1;
  revokeImage();
});
</script>

<template>
  <v-dialog
    :model-value="current != null"
    persistent
    max-width="720"
  >
    <v-card v-if="current" class="login-welcome-card" rounded="xl">
      <div class="login-welcome-band" />
      <div class="login-welcome-body">
        <div class="d-flex align-start justify-space-between ga-3">
          <div class="d-flex align-start ga-3">
            <v-avatar color="primary" variant="tonal" rounded="lg" size="44">
              <v-icon icon="mdi-hand-wave" />
            </v-avatar>
            <div>
              <div class="login-welcome-kicker">Karşılama</div>
              <h2 class="login-welcome-title">{{ current.title }}</h2>
            </div>
          </div>
          <span v-if="queue.length > 1" class="text-caption text-medium-emphasis">
            {{ index + 1 }} / {{ queue.length }}
          </span>
        </div>
        <div v-if="imageLoading" class="d-flex justify-center py-6">
          <v-progress-circular indeterminate color="primary" />
        </div>
        <v-img
          v-else-if="imageUrl"
          :src="imageUrl"
          max-height="420"
          class="mt-4 rounded-lg"
          contain
        />
        <div v-if="current.html" class="login-welcome-html mt-4" v-html="current.html" />
      </div>
      <v-card-actions class="px-6 pb-5 pt-0">
        <v-spacer />
        <v-btn color="primary" variant="flat" rounded="lg" @click="advance">
          {{ hasNext ? 'Devam' : 'Kapat' }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.login-welcome-card {
  overflow: hidden;
}
.login-welcome-band {
  height: 6px;
  background: linear-gradient(90deg, rgb(var(--v-theme-primary)), rgb(var(--v-theme-secondary)));
}
.login-welcome-body {
  padding: 24px 24px 8px;
}
.login-welcome-kicker {
  font-size: 0.75rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: rgb(var(--v-theme-primary));
  font-weight: 600;
}
.login-welcome-title {
  margin: 2px 0 0;
  font-size: 1.35rem;
  line-height: 1.3;
  font-weight: 650;
}
.login-welcome-html :deep(p) {
  margin: 0 0 0.9rem;
  line-height: 1.55;
}
.login-welcome-html :deep(p:last-child) {
  margin-bottom: 0;
}
.login-welcome-html :deep(a) {
  color: rgb(var(--v-theme-primary));
}
.login-welcome-html :deep(.welcome-path) {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-top: 4px;
}
.login-welcome-html :deep(.welcome-step) {
  display: inline-flex;
  align-items: center;
  padding: 6px 12px;
  border-radius: 999px;
  background: rgba(var(--v-theme-primary), 0.12);
  color: rgb(var(--v-theme-primary));
  font-weight: 600;
  font-size: 0.92rem;
}
.login-welcome-html :deep(.welcome-sep) {
  color: rgba(var(--v-theme-on-surface), 0.45);
  font-weight: 600;
}
</style>
