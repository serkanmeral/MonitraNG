<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { useLocaleStore } from '@/stores/locale';
import {
  ocGetNotifications,
  ocMarkAllNotificationsRead,
  ocMarkNotificationRead,
} from '@/services/operationCoreService';
import type { OcNotification } from '@/types/apps/operationCore';

const localeStore = useLocaleStore();

// i18n (legacy global instance — global header bileşenleriyle aynı desen).
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: Record<string, unknown>): string => {
  if (i18n?.t) return i18n.t(key, params ?? {});
  if (i18n?.global?.t) return i18n.global.t(key, params ?? {});
  return key;
};

const REFRESH_MS = 60_000;

const items = ref<OcNotification[]>([]);
const unreadCount = ref(0);
const loading = ref(false);
const errored = ref(false);
let timer: ReturnType<typeof setInterval> | null = null;

const hasUnread = computed(() => unreadCount.value > 0);

async function load() {
  loading.value = true;
  errored.value = false;
  try {
    const res = await ocGetNotifications({ take: 20 });
    items.value = res.items;
    unreadCount.value = res.unreadCount;
  } catch {
    errored.value = true;
  } finally {
    loading.value = false;
  }
}

function onMenuToggle(open: boolean) {
  if (open) load();
}

async function onItemClick(item: OcNotification) {
  if (!item.isRead) {
    try {
      await ocMarkNotificationRead(item.id);
      item.isRead = true;
      unreadCount.value = Math.max(0, unreadCount.value - 1);
    } catch {
      // best-effort
    }
  }
  if (item.workItemId) {
    navigateTo(`/apps/operation-core/work-items/${encodeURIComponent(item.workItemId)}/profile`);
  }
}

async function markAllRead() {
  try {
    await ocMarkAllNotificationsRead();
    items.value = items.value.map((n) => ({ ...n, isRead: true }));
    unreadCount.value = 0;
  } catch {
    // best-effort
  }
}

const KNOWN_TYPES = new Set(['CommentMention', 'WorkItemAssigned']);

function typeLabel(type?: string | null): string {
  const key = type && KNOWN_TYPES.has(type)
    ? `header.notifications.types.${type}`
    : 'header.notifications.types.default';
  return t(key);
}

function typeIcon(type?: string | null): string {
  switch (type) {
    case 'CommentMention':
      return 'mdi-at';
    case 'WorkItemAssigned':
      return 'mdi-account-arrow-right';
    default:
      return 'mdi-bell-outline';
  }
}

function typeColor(type?: string | null): string {
  switch (type) {
    case 'CommentMention':
      return 'primary';
    case 'WorkItemAssigned':
      return 'info';
    default:
      return 'secondary';
  }
}

const rtf = computed(() => {
  try {
    return new Intl.RelativeTimeFormat(localeStore.currentLocale || 'tr', { numeric: 'auto' });
  } catch {
    return new Intl.RelativeTimeFormat('tr', { numeric: 'auto' });
  }
});

function relativeTime(iso?: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  const diffMs = date.getTime() - Date.now();
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['year', 1000 * 60 * 60 * 24 * 365],
    ['month', 1000 * 60 * 60 * 24 * 30],
    ['day', 1000 * 60 * 60 * 24],
    ['hour', 1000 * 60 * 60],
    ['minute', 1000 * 60],
  ];
  for (const [unit, ms] of units) {
    if (Math.abs(diffMs) >= ms) {
      return rtf.value.format(Math.round(diffMs / ms), unit);
    }
  }
  return rtf.value.format(Math.round(diffMs / 1000), 'second');
}

onMounted(() => {
  load();
  timer = setInterval(load, REFRESH_MS);
});

onBeforeUnmount(() => {
  if (timer) clearInterval(timer);
});
</script>

<template>
  <v-menu :close-on-content-click="false" @update:model-value="onMenuToggle">
    <template #activator="{ props }">
      <v-btn icon variant="text" color="primary" v-bind="props">
        <v-badge v-if="hasUnread" color="error" :content="unreadCount" :max="99">
          <v-icon size="24">mdi-bell</v-icon>
        </v-badge>
        <v-icon v-else size="24">mdi-bell</v-icon>
      </v-btn>
    </template>
    <v-sheet rounded="md" width="380" elevation="10">
      <div class="px-6 pb-3 pt-5">
        <div class="d-flex align-center justify-space-between">
          <h6 class="text-h5">{{ t('header.notifications.title') }}</h6>
          <v-chip
            v-if="hasUnread"
            color="primary"
            variant="flat"
            size="small"
            class="text-white"
          >
            {{ t('header.notifications.unreadCount', { count: unreadCount }) }}
          </v-chip>
        </div>
      </div>
      <v-divider />
      <perfect-scrollbar style="height: 400px">
        <div v-if="loading && !items.length" class="d-flex justify-center py-8">
          <v-progress-circular indeterminate color="primary" size="28" />
        </div>
        <div
          v-else-if="errored && !items.length"
          class="text-center text-medium-emphasis py-8 px-6"
        >
          {{ t('header.notifications.loadError') }}
        </div>
        <div
          v-else-if="!items.length"
          class="text-center text-medium-emphasis py-8 px-6"
        >
          {{ t('header.notifications.empty') }}
        </div>
        <v-list v-else class="py-0 theme-list" lines="two">
          <v-list-item
            v-for="item in items"
            :key="item.id"
            :value="item.id"
            color="primary"
            class="py-3 px-6 border-b"
            :class="{ 'bg-lightprimary': !item.isRead }"
            @click="onItemClick(item)"
          >
            <template #prepend>
              <v-avatar size="40" :color="typeColor(item.notificationType)" variant="tonal">
                <v-icon size="20">{{ typeIcon(item.notificationType) }}</v-icon>
              </v-avatar>
            </template>
            <div class="d-flex align-center ga-2">
              <h6 class="text-subtitle-1 font-weight-semibold mb-0">
                {{ item.title || typeLabel(item.notificationType) }}
              </h6>
              <v-icon v-if="!item.isRead" size="8" color="primary">mdi-circle</v-icon>
            </div>
            <p class="text-body-2 textSecondary mb-0 text-truncate">{{ item.message }}</p>
            <div class="d-flex align-center justify-space-between mt-1">
              <span class="text-caption text-medium-emphasis">{{ typeLabel(item.notificationType) }}</span>
              <span class="text-caption text-medium-emphasis">{{ relativeTime(item.createdAt) }}</span>
            </div>
          </v-list-item>
        </v-list>
      </perfect-scrollbar>
      <v-divider />
      <div class="py-3 px-4">
        <v-btn
          color="primary"
          variant="text"
          block
          :disabled="!hasUnread"
          @click="markAllRead"
        >
          {{ t('header.notifications.markAllRead') }}
        </v-btn>
      </div>
    </v-sheet>
  </v-menu>
</template>
