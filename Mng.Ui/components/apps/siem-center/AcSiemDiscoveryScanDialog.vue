<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  cancelDiscoveryScan,
  fetchDiscoveryScanStatus,
  startDiscoveryScan,
  type DiscoveryScanStatusResponse,
} from '@/services/siemDiscoveryService';

const props = defineProps<{
  open: boolean;
}>();

const emit = defineEmits<{
  'update:open': [value: boolean];
  completed: [];
}>();

const { t } = useAppI18n();

const dialogOpen = computed({
  get: () => props.open,
  set: (v: boolean) => emit('update:open', v),
});

const cidr = ref('192.168.20.1-40');
const enrichWithAd = ref(false);
const starting = ref(false);
const cancelling = ref(false);
const error = ref<string | null>(null);
const runId = ref<string | null>(null);
const status = ref<DiscoveryScanStatusResponse | null>(null);
const snackbar = ref(false);
const snackbarText = ref('');
const snackbarColor = ref<'success' | 'error' | 'warning'>('success');

let pollTimer: ReturnType<typeof setInterval> | undefined;
let closeTimer: ReturnType<typeof setTimeout> | undefined;

const isRunning = computed(() => {
  const s = status.value?.status;
  return s === 'queued' || s === 'running';
});

const progress = computed(() => status.value?.progressPercent ?? 0);

const statusLabel = computed(() => {
  const s = status.value?.status;
  if (!s) return '';
  const key = `siemCenter.discovery.scan.status.${s}`;
  const translated = t(key);
  return translated === key ? s : translated;
});

watch(
  () => props.open,
  (v) => {
    if (!v) {
      stopPoll();
      clearCloseTimer();
      return;
    }
    error.value = null;
    if (!isRunning.value) {
      runId.value = null;
      status.value = null;
    }
  },
);

function stopPoll() {
  if (pollTimer) {
    clearInterval(pollTimer);
    pollTimer = undefined;
  }
}

function clearCloseTimer() {
  if (closeTimer) {
    clearTimeout(closeTimer);
    closeTimer = undefined;
  }
}

function finishAndClose(kind: 'completed' | 'cancelled', st: DiscoveryScanStatusResponse) {
  stopPoll();
  if (kind === 'completed') {
    emit('completed');
    snackbarColor.value = 'success';
    snackbarText.value = t('siemCenter.discovery.scan.toastCompleted', {
      alive: st.foundAlive,
      upserted: st.upserted,
    });
  } else {
    snackbarColor.value = 'warning';
    snackbarText.value = t('siemCenter.discovery.scan.toastCancelled');
  }
  snackbar.value = true;
  clearCloseTimer();
  closeTimer = setTimeout(() => {
    dialogOpen.value = false;
  }, 600);
}

async function pollOnce() {
  if (!runId.value) return;
  try {
    status.value = await fetchDiscoveryScanStatus(runId.value);
    const s = status.value.status;
    if (s === 'completed') {
      finishAndClose('completed', status.value);
      return;
    }
    if (s === 'cancelled') {
      finishAndClose('cancelled', status.value);
      return;
    }
    if (s === 'failed') {
      stopPoll();
      error.value = status.value.error || t('siemCenter.discovery.scan.startFailed');
      snackbarColor.value = 'error';
      snackbarText.value = error.value;
      snackbar.value = true;
    }
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
    stopPoll();
  }
}

function startPoll() {
  stopPoll();
  pollTimer = setInterval(() => {
    void pollOnce();
  }, 1500);
  void pollOnce();
}

function normalizeCidrInput(raw: string): string {
  return raw
    .trim()
    .replace(/[\u2044\u2215\uFF0F]/g, '/')
    .replace(/[\u2013\u2014\u2212]/g, '-')
    .replace(/\s*\/\s*/g, '/')
    .replace(/\s*-\s*/g, '-');
}

function validateCidrInput(raw: string): string | null {
  const v = normalizeCidrInput(raw);
  if (!v) return t('siemCenter.discovery.scan.cidrRequired');
  if (v.includes('/')) {
    const [ip, prefixRaw] = v.split('/');
    const prefix = Number(prefixRaw);
    if (!ip || !/^\d{1,3}(\.\d{1,3}){3}$/.test(ip) || !Number.isInteger(prefix) || prefix < 0 || prefix > 32) {
      return t('siemCenter.discovery.scan.cidrInvalid', { value: v });
    }
    if (prefix < 24) {
      return t('siemCenter.discovery.scan.cidrTooWide', { prefix });
    }
    return null;
  }
  if (v.includes('-')) {
    const idx = v.lastIndexOf('-');
    const left = v.slice(0, idx);
    const right = v.slice(idx + 1);
    if (!/^\d{1,3}(\.\d{1,3}){3}$/.test(left)) {
      return t('siemCenter.discovery.scan.cidrInvalid', { value: v });
    }
    if (!/^\d{1,3}$/.test(right) && !/^\d{1,3}(\.\d{1,3}){3}$/.test(right)) {
      return t('siemCenter.discovery.scan.cidrInvalid', { value: v });
    }
    return null;
  }
  if (/^\d{1,3}(\.\d{1,3}){3}$/.test(v)) return null;
  return t('siemCenter.discovery.scan.cidrInvalid', { value: v });
}

async function start() {
  starting.value = true;
  error.value = null;
  status.value = null;
  runId.value = null;
  const cidrNorm = normalizeCidrInput(cidr.value);
  cidr.value = cidrNorm;
  const validationError = validateCidrInput(cidrNorm);
  if (validationError) {
    error.value = validationError;
    starting.value = false;
    return;
  }
  try {
    const res = await startDiscoveryScan({
      cidr: cidrNorm,
      enrichWithAd: enrichWithAd.value,
    });
    if (res.status === 'error' || res.error) {
      const base = res.error || t('siemCenter.discovery.scan.startFailed');
      error.value = `${base} · ${cidrNorm}`;
      return;
    }
    runId.value = res.runId;
    status.value = {
      runId: res.runId,
      domainId: '',
      cidr: cidr.value.trim(),
      enrichWithAd: enrichWithAd.value,
      status: res.status || 'queued',
      progressPercent: 0,
      totalTargets: res.totalTargets,
      probed: 0,
      foundAlive: 0,
      foundWindows: 0,
      foundLinux: 0,
      foundUnknown: 0,
      upserted: 0,
    };
    startPoll();
  } catch (e: unknown) {
    const data = (e as { data?: { error?: string; Error?: string } })?.data;
    error.value =
      data?.error
      || data?.Error
      || (e instanceof Error ? e.message : String(e))
      || t('siemCenter.discovery.scan.startFailed');
  } finally {
    starting.value = false;
  }
}

async function cancel() {
  if (!runId.value) return;
  cancelling.value = true;
  try {
    status.value = await cancelDiscoveryScan(runId.value);
    if (status.value.status === 'cancelled') {
      finishAndClose('cancelled', status.value);
    }
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    cancelling.value = false;
  }
}

function close() {
  stopPoll();
  clearCloseTimer();
  dialogOpen.value = false;
}
</script>

<template>
  <v-dialog
    v-model="dialogOpen"
    max-width="560"
    :persistent="isRunning"
    scrim
  >
    <v-card>
      <v-card-title class="d-flex align-center justify-space-between ga-2">
        <span class="text-h6">{{ t('siemCenter.discovery.scan.title') }}</span>
        <v-btn
          icon
          variant="text"
          size="small"
          :aria-label="t('siemCenter.discovery.scan.close')"
          @click="close"
        >
          <v-icon icon="mdi-close" />
        </v-btn>
      </v-card-title>
      <v-card-subtitle>
        {{ t('siemCenter.discovery.scan.subtitle') }}
      </v-card-subtitle>

      <v-card-text class="d-flex flex-column ga-4">
        <v-text-field
          v-model="cidr"
          :label="t('siemCenter.discovery.scan.cidrLabel')"
          :hint="t('siemCenter.discovery.scan.cidrHint')"
          persistent-hint
          density="comfortable"
          :disabled="isRunning"
          variant="outlined"
          autocomplete="off"
          name="discovery-scan-cidr"
          spellcheck="false"
        />

        <v-checkbox
          v-model="enrichWithAd"
          :label="t('siemCenter.discovery.scan.enrichAd')"
          :hint="t('siemCenter.discovery.scan.enrichAdHint')"
          persistent-hint
          :disabled="isRunning"
          density="compact"
        />

        <v-alert
          v-if="error"
          type="error"
          variant="tonal"
          density="compact"
          :text="error"
        />

        <div v-if="status" class="scan-progress">
          <div class="d-flex justify-space-between text-caption mb-1">
            <span>{{ statusLabel }}</span>
            <span>{{ progress }}%</span>
          </div>
          <v-progress-linear
            :model-value="progress"
            :indeterminate="status.status === 'queued'"
            color="primary"
            height="8"
            rounded
          />
          <div class="text-caption text-medium-emphasis mt-2">
            {{
              t('siemCenter.discovery.scan.summary', {
                probed: status.probed,
                total: status.totalTargets,
                alive: status.foundAlive,
                win: status.foundWindows,
                lin: status.foundLinux,
                unk: status.foundUnknown,
                upserted: status.upserted,
              })
            }}
          </div>
        </div>
      </v-card-text>

      <v-card-actions class="pa-4 flex-wrap ga-2">
        <v-spacer />
        <v-btn
          v-if="isRunning"
          variant="text"
          color="warning"
          :loading="cancelling"
          @click="cancel"
        >
          {{ t('siemCenter.discovery.scan.cancel') }}
        </v-btn>
        <v-btn variant="text" @click="close">
          {{ t('siemCenter.discovery.scan.close') }}
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          :loading="starting"
          :disabled="isRunning || !cidr.trim()"
          @click="start"
        >
          {{ t('siemCenter.discovery.scan.start') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-snackbar
    v-model="snackbar"
    :color="snackbarColor"
    location="bottom right"
    :timeout="4000"
    rounded="md"
  >
    {{ snackbarText }}
  </v-snackbar>
</template>
