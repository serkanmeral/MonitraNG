<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useLocaleStore } from '@/stores/locale';
import {
  ocGetNotifications,
  ocMarkAllNotificationsRead,
  ocMarkNotificationRead,
} from '@/services/operationCoreService';
import type { OcNotification } from '@/types/apps/operationCore';
import {
  isAlarmNotification,
  notificationTypeColor,
  notificationTypeIcon,
  resolveNotificationNavigationTarget,
} from '@/utils/ocNotificationNavigation';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const localeStore = useLocaleStore();

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  tail: computed(() => ({ text: t('operationCore.notifications.title'), disabled: true })),
});

const PER_PAGE_OPTIONS = [10, 20, 50];

const items = ref<OcNotification[]>([]);
const total = ref(0);
const unreadCount = ref(0);
const loading = ref(false);
const errored = ref(false);

const page = ref(1);
const perPage = ref(20);
const unreadOnly = ref(false);

const pageCount = computed(() => Math.max(1, Math.ceil(total.value / perPage.value)));
const hasUnread = computed(() => unreadCount.value > 0);

async function load() {
  loading.value = true;
  errored.value = false;
  try {
    const res = await ocGetNotifications({
      skip: (page.value - 1) * perPage.value,
      take: perPage.value,
      unreadOnly: unreadOnly.value,
    });
    items.value = res.items;
    total.value = res.total;
    unreadCount.value = res.unreadCount;
  } catch {
    errored.value = true;
    items.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

watch([page, perPage, unreadOnly], () => {
  void load();
});

// Sayfa boyutu / filtre değişince ilk sayfaya dön (watch zaten reload tetikler).
watch([perPage, unreadOnly], () => {
  if (page.value !== 1) page.value = 1;
});

async function markRead(item: OcNotification) {
  if (item.isRead) return;
  try {
    await ocMarkNotificationRead(item.id);
    item.isRead = true;
    unreadCount.value = Math.max(0, unreadCount.value - 1);
    // "Yalnızca okunmamış" görünümünde okunan kayıt listeden düşer.
    if (unreadOnly.value) void load();
  } catch {
    // best-effort
  }
}

async function onItemClick(item: OcNotification) {
  await markRead(item);
  const target = resolveNotificationNavigationTarget(item);
  if (target) void navigateTo(target);
}

async function markAllRead() {
  try {
    await ocMarkAllNotificationsRead();
    items.value = items.value.map((n) => ({ ...n, isRead: true }));
    unreadCount.value = 0;
    if (unreadOnly.value) void load();
  } catch {
    // best-effort
  }
}

const KNOWN_TYPES = new Set([
  'CommentMention',
  'WorkItemAssigned',
  'AlarmRaised',
  'AlarmUpdated',
  'AlarmResolved',
]);

function typeLabel(type?: string | null): string {
  const key = type && KNOWN_TYPES.has(type)
    ? `operationCore.notifications.types.${type}`
    : 'operationCore.notifications.types.default';
  return t(key);
}

function typeIcon(type?: string | null): string {
  return notificationTypeIcon(type);
}

function typeColor(type?: string | null): string {
  return notificationTypeColor(type);
}

function openActionLabel(item: OcNotification): string | null {
  if (item.workItemId) return t('operationCore.notifications.openWorkItem');
  if (isAlarmNotification(item)) return t('operationCore.notifications.openAlarm');
  return null;
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
  void load();
});
</script>

<template>
  <div class="oc-notifications-page">
    <BaseBreadcrumb
      :title="t('operationCore.notifications.title')"
      :breadcrumbs="breadcrumbs"
    />

    <v-card elevation="10" rounded="lg">
      <v-card-text>
        <div class="d-flex align-center justify-space-between flex-wrap ga-3 mb-4">
          <div class="d-flex align-center ga-2">
            <p class="text-body-2 text-medium-emphasis mb-0">
              {{ t('operationCore.notifications.subtitle') }}
            </p>
            <v-chip v-if="hasUnread" color="primary" variant="flat" size="small">
              {{ t('operationCore.notifications.unreadCount', { count: unreadCount }) }}
            </v-chip>
          </div>
          <div class="d-flex align-center ga-2">
            <v-btn-toggle
              v-model="unreadOnly"
              density="comfortable"
              variant="outlined"
              divided
              mandatory
            >
              <v-btn :value="false" size="small" class="text-none">
                {{ t('operationCore.notifications.all') }}
              </v-btn>
              <v-btn :value="true" size="small" class="text-none">
                {{ t('operationCore.notifications.unreadOnly') }}
              </v-btn>
            </v-btn-toggle>
            <v-btn
              color="primary"
              variant="tonal"
              size="small"
              class="text-none"
              prepend-icon="mdi-check-all"
              :disabled="!hasUnread"
              @click="markAllRead"
            >
              {{ t('operationCore.notifications.markAllRead') }}
            </v-btn>
          </div>
        </div>

        <div v-if="loading && !items.length" class="d-flex justify-center py-12">
          <v-progress-circular indeterminate color="primary" size="36" />
        </div>
        <div
          v-else-if="errored && !items.length"
          class="text-center text-medium-emphasis py-12"
        >
          {{ t('operationCore.notifications.loadError') }}
        </div>
        <div
          v-else-if="!items.length"
          class="text-center text-medium-emphasis py-12"
        >
          {{ t('operationCore.notifications.empty') }}
        </div>

        <v-list v-else class="py-0" lines="two">
          <template v-for="(item, idx) in items" :key="item.id">
            <v-list-item
              class="px-2 py-3 rounded-lg oc-notif-item"
              :class="{ 'bg-lightprimary': !item.isRead }"
              @click="onItemClick(item)"
            >
              <template #prepend>
                <v-avatar size="42" :color="typeColor(item.notificationType)" variant="tonal">
                  <v-icon size="22">{{ typeIcon(item.notificationType) }}</v-icon>
                </v-avatar>
              </template>

              <div class="d-flex align-center ga-2">
                <h6 class="text-subtitle-1 font-weight-semibold mb-0">
                  {{ item.title || typeLabel(item.notificationType) }}
                </h6>
                <v-icon v-if="!item.isRead" size="8" color="primary">mdi-circle</v-icon>
                <v-chip
                  v-if="item.workItemKey"
                  size="x-small"
                  variant="tonal"
                  color="secondary"
                >
                  {{ item.workItemKey }}
                </v-chip>
              </div>
              <p class="text-body-2 text-medium-emphasis mb-0">{{ item.message }}</p>
              <div class="d-flex align-center ga-2 mt-1">
                <span class="text-caption text-medium-emphasis">
                  {{ typeLabel(item.notificationType) }}
                </span>
                <span class="text-caption text-disabled">·</span>
                <span class="text-caption text-medium-emphasis">
                  {{ relativeTime(item.createdAt) }}
                </span>
                <template v-if="openActionLabel(item) && resolveNotificationNavigationTarget(item)">
                  <span class="text-caption text-disabled">·</span>
                  <span class="text-caption text-primary">{{ openActionLabel(item) }}</span>
                </template>
              </div>

              <template #append>
                <v-btn
                  v-if="!item.isRead"
                  icon="mdi-check"
                  variant="text"
                  size="small"
                  density="comfortable"
                  :title="t('operationCore.notifications.markRead')"
                  @click.stop="markRead(item)"
                />
              </template>
            </v-list-item>
            <v-divider v-if="idx < items.length - 1" />
          </template>
        </v-list>

        <div
          v-if="items.length || total"
          class="d-flex align-center justify-space-between flex-wrap ga-3 mt-4"
        >
          <div class="d-flex align-center ga-2" style="max-width: 180px">
            <span class="text-caption text-medium-emphasis">
              {{ t('operationCore.notifications.perPage') }}
            </span>
            <v-select
              v-model="perPage"
              :items="PER_PAGE_OPTIONS"
              variant="outlined"
              density="compact"
              hide-details
              style="max-width: 90px"
            />
          </div>
          <v-pagination
            v-model="page"
            :length="pageCount"
            :total-visible="5"
            density="comfortable"
            rounded="circle"
          />
        </div>
      </v-card-text>
    </v-card>
  </div>
</template>

<style scoped>
.oc-notif-item {
  cursor: pointer;
}
</style>
