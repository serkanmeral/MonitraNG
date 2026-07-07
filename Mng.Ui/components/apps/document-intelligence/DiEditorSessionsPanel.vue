<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diGetEditorSessionStats,
  diRevokeEditorSession,
} from '@/services/documentIntelligenceService';
import { subscribeEditorSessionChanges, notifyEditorSessionChanged } from '@/utils/diEditorSessionBroadcast';
import type { DiEditorSessionStats } from '@/types/apps/documentIntelligence';

const STATS_POLL_MS = 5000;

const props = defineProps<{
  /** Toolbar chip modu — tıklanınca diyalog açılır. */
  compact?: boolean;
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const dialogOpen = ref(false);
const loading = ref(false);
const revokingToken = ref<string | null>(null);
const stats = ref<DiEditorSessionStats | null>(null);
let pollTimer: ReturnType<typeof setInterval> | null = null;
let unsubscribeBroadcast: (() => void) | undefined;

const capacityLabel = computed(() => {
  const s = stats.value;
  if (!s) return '…';
  return t('documentIntelligence.editorSessions.capacityShort', {
    connections: s.activeConnections,
    maxConnections: s.limits.maxConnections,
    documents: s.activeDocuments,
    maxDocuments: s.limits.maxDocuments,
  });
});

const connectionsPercent = computed(() => {
  const s = stats.value;
  if (!s || s.limits.maxConnections <= 0) return 0;
  return Math.min(100, Math.round((s.activeConnections / s.limits.maxConnections) * 100));
});

const documentsPercent = computed(() => {
  const s = stats.value;
  if (!s || s.limits.maxDocuments <= 0) return 0;
  return Math.min(100, Math.round((s.activeDocuments / s.limits.maxDocuments) * 100));
});

const capacityColor = computed(() => {
  const maxPct = Math.max(connectionsPercent.value, documentsPercent.value);
  if (maxPct >= 95) return 'error';
  if (maxPct >= 75) return 'warning';
  return 'primary';
});

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

function kindLabel(kind: string): string {
  const key = `documentIntelligence.editorSessions.kind.${kind}` as const;
  const translated = t(key);
  return translated === key ? kind : translated;
}

async function refresh(silent = false) {
  if (!silent) loading.value = true;
  try {
    stats.value = await diGetEditorSessionStats();
  } catch (e: unknown) {
    if (!silent) panelError(e, 'documentIntelligence.editorSessions.errors.load');
  } finally {
    if (!silent) loading.value = false;
  }
}

function openDialog() {
  dialogOpen.value = true;
}

function startPolling() {
  stopPolling();
  pollTimer = setInterval(() => {
    void refresh(true);
  }, STATS_POLL_MS);
}

function onWindowFocus() {
  void refresh(true);
}

function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer);
    pollTimer = null;
  }
}

async function revokeSession(accessToken: string | null | undefined) {
  const token = accessToken?.trim();
  if (!token) return;
  revokingToken.value = token;
  try {
    await diRevokeEditorSession(token);
    notifyEditorSessionChanged();
    await refresh(true);
  } catch (e: unknown) {
    panelError(e, 'documentIntelligence.editorSessions.errors.revoke');
  } finally {
    revokingToken.value = null;
  }
}

watch(dialogOpen, (open) => {
  if (open) void refresh();
});

onMounted(() => {
  void refresh(true);
  startPolling();
  unsubscribeBroadcast = subscribeEditorSessionChanges(() => {
    void refresh(true);
  });
  if (import.meta.client) {
    window.addEventListener('focus', onWindowFocus);
    document.addEventListener('visibilitychange', onWindowFocus);
  }
});

onUnmounted(() => {
  stopPolling();
  unsubscribeBroadcast?.();
  if (import.meta.client) {
    window.removeEventListener('focus', onWindowFocus);
    document.removeEventListener('visibilitychange', onWindowFocus);
  }
});
</script>

<template>
  <div>
    <v-btn
      v-if="compact"
      variant="tonal"
      size="small"
      class="text-none"
      :color="capacityColor"
      prepend-icon="mdi-monitor-dashboard"
      :loading="loading && !dialogOpen"
      @click="openDialog"
    >
      {{ capacityLabel }}
    </v-btn>

    <v-dialog v-model="dialogOpen" max-width="960" scrollable>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center ga-2 py-3">
          <v-icon icon="mdi-monitor-dashboard" color="primary" />
          <span class="text-subtitle-1 font-weight-bold">
            {{ t('documentIntelligence.editorSessions.title') }}
          </span>
          <v-spacer />
          <v-tooltip :text="t('documentIntelligence.editorSessions.refreshHint')" location="bottom">
            <template #activator="{ props: tooltipProps }">
              <v-btn
                v-bind="tooltipProps"
                variant="tonal"
                size="small"
                class="text-none"
                prepend-icon="mdi-refresh"
                :loading="loading"
                @click="refresh()"
              >
                {{ t('documentIntelligence.editorSessions.refresh') }}
              </v-btn>
            </template>
          </v-tooltip>
          <v-btn icon="mdi-close" variant="text" size="small" @click="dialogOpen = false" />
        </v-card-title>

        <v-divider />

        <v-card-text class="pt-4">
          <div v-if="stats" class="mb-4">
            <div class="d-flex flex-wrap ga-4 mb-3">
              <div class="flex-grow-1" style="min-width: 200px">
                <div class="text-caption text-medium-emphasis mb-1">
                  {{ t('documentIntelligence.editorSessions.connections') }}
                </div>
                <v-progress-linear
                  :model-value="connectionsPercent"
                  :color="capacityColor"
                  height="8"
                  rounded
                />
                <div class="text-body-2 mt-1">
                  {{ stats.activeConnections }} / {{ stats.limits.maxConnections }}
                </div>
              </div>
              <div class="flex-grow-1" style="min-width: 200px">
                <div class="text-caption text-medium-emphasis mb-1">
                  {{ t('documentIntelligence.editorSessions.documents') }}
                </div>
                <v-progress-linear
                  :model-value="documentsPercent"
                  :color="capacityColor"
                  height="8"
                  rounded
                />
                <div class="text-body-2 mt-1">
                  {{ stats.activeDocuments }} / {{ stats.limits.maxDocuments }}
                </div>
              </div>
            </div>

            <v-alert
              v-if="stats.byUser.length === 0 && !(stats.sessions?.length)"
              type="info"
              variant="tonal"
              density="compact"
              class="rounded-lg"
            >
              {{ t('documentIntelligence.editorSessions.empty') }}
            </v-alert>

            <div v-if="stats.byUser.length" class="mb-4">
              <div class="text-subtitle-2 font-weight-bold mb-2">
                {{ t('documentIntelligence.editorSessions.byUserTitle') }}
              </div>
              <v-chip
                v-for="user in stats.byUser"
                :key="user.userId"
                size="small"
                variant="tonal"
                class="mr-2 mb-2"
              >
                {{ user.displayName || user.userId }} · {{ user.connectionCount }}
              </v-chip>
            </div>

            <div v-if="stats.sessions?.length">
              <div class="text-subtitle-2 font-weight-bold mb-2">
                {{ t('documentIntelligence.editorSessions.sessionsTitle') }}
              </div>
              <v-table density="compact" class="rounded-lg border">
                <thead>
                  <tr>
                    <th>{{ t('documentIntelligence.editorSessions.colUser') }}</th>
                    <th>{{ t('documentIntelligence.editorSessions.colDocument') }}</th>
                    <th>{{ t('documentIntelligence.editorSessions.colKind') }}</th>
                    <th>{{ t('documentIntelligence.editorSessions.colMode') }}</th>
                    <th>{{ t('documentIntelligence.editorSessions.colLastSeen') }}</th>
                    <th class="text-end">{{ t('documentIntelligence.editorSessions.colActions') }}</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="session in stats.sessions" :key="session.accessToken ?? session.tokenPrefix">
                    <td>{{ session.userName }}</td>
                    <td class="text-truncate" style="max-width: 240px">
                      {{ session.displayName || session.resourceId || session.templateId || session.letterheadId || '—' }}
                    </td>
                    <td>{{ kindLabel(session.kind) }}</td>
                    <td>
                      <v-chip
                        v-if="session.readOnly"
                        size="x-small"
                        variant="tonal"
                        color="warning"
                        label
                      >
                        {{ t('documentIntelligence.designer.editorReadOnlyHint') }}
                      </v-chip>
                      <span v-else class="text-caption">{{ t('documentIntelligence.editorSessions.modeEdit') }}</span>
                    </td>
                    <td class="text-caption">{{ formatDateTime(session.lastSeenAt) }}</td>
                    <td class="text-end">
                      <v-btn
                        size="x-small"
                        variant="text"
                        color="error"
                        class="text-none"
                        :loading="revokingToken === session.accessToken"
                        @click="revokeSession(session.accessToken)"
                      >
                        {{ t('documentIntelligence.editorSessions.revoke') }}
                      </v-btn>
                    </td>
                  </tr>
                </tbody>
              </v-table>
            </div>
          </div>

          <v-skeleton-loader v-else-if="loading" type="article" />
        </v-card-text>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
</style>
