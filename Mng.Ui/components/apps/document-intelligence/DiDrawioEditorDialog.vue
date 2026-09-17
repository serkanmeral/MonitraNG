<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  diFetchFileBlob,
  diGetById,
  diReplaceFileContent,
} from '@/services/documentIntelligenceService';
import type { DiResource } from '@/types/apps/documentIntelligence';
import {
  DI_EMPTY_DRAWIO_XML,
  getDiDrawioEmbedOrigin,
  getDiDrawioEditorSrc,
  isDiDrawioEmbedOrigin,
  utf8ToBase64,
} from '@/utils/diDrawio';

const props = defineProps<{
  modelValue: boolean;
  resourceId: string | null;
  title?: string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [resource: DiResource];
}>();

const { t } = useAppI18n();
const toast = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const iframeEl = ref<HTMLIFrameElement | null>(null);
const loading = ref(false);
const saving = ref(false);
const failed = ref(false);
const currentXml = ref(DI_EMPTY_DRAWIO_XML);
const savedXml = ref(DI_EMPTY_DRAWIO_XML);
const fileName = ref('');
const iframeKey = ref(0);
let loaded = false;
let initTimer: ReturnType<typeof setTimeout> | null = null;
let pendingSave = false;

const dirty = computed(() => currentXml.value !== savedXml.value);
const heading = computed(() => props.title || fileName.value || t('documentIntelligence.drawioEditorTitle'));
const editorSrc = computed(() => getDiDrawioEditorSrc());

function clearTimer() {
  if (initTimer) {
    clearTimeout(initTimer);
    initTimer = null;
  }
}

function parseMsg(data: unknown): Record<string, unknown> | null {
  if (typeof data === 'string') {
    try {
      return JSON.parse(data) as Record<string, unknown>;
    } catch {
      return null;
    }
  }
  if (data && typeof data === 'object') return data as Record<string, unknown>;
  return null;
}

function post(win: Window, payload: Record<string, unknown>) {
  const origin = getDiDrawioEmbedOrigin();
  if (!origin) return;
  win.postMessage(JSON.stringify(payload), origin);
}

function sendLoad(win: Window) {
  post(win, { action: 'load', autosave: 1, xml: currentXml.value || DI_EMPTY_DRAWIO_XML });
}

function requestExport(win: Window) {
  post(win, { action: 'export', format: 'xml' });
}

function xmlFromMessage(msg: Record<string, unknown>): string | null {
  const xml = msg.xml;
  if (typeof xml === 'string' && xml.trim()) return xml;
  const data = msg.data;
  if (typeof data === 'string' && data.trim() && (data.includes('<mx') || data.includes('<mxfile'))) return data;
  return null;
}

async function persist(xml: string) {
  if (!props.resourceId) return;
  saving.value = true;
  try {
    const updated = await diReplaceFileContent(props.resourceId, {
      content: utf8ToBase64(xml),
      originalFileName: fileName.value || drawioFallbackName(),
      mimeType: 'application/vnd.jgraph.mxfile',
      extension: 'drawio',
      changeNote: 'drawio-edit',
    });
    currentXml.value = xml;
    savedXml.value = xml;
    emit('saved', updated);
    toast.push({
      title: t('documentIntelligence.notify.successTitle'),
      message: t('documentIntelligence.drawioSaved'),
      severity: 'success',
    });
  } catch (e: unknown) {
    toast.push({
      title: t('errors.dg.toastTitle'),
      message: panelError(e, 'documentIntelligence.errors.replace'),
      severity: 'error',
    });
  } finally {
    saving.value = false;
    pendingSave = false;
  }
}

function drawioFallbackName() {
  return 'process.drawio';
}

function onMessage(event: MessageEvent) {
  if (!isDiDrawioEmbedOrigin(event.origin)) return;
  const msg = parseMsg(event.data);
  if (!msg) return;
  const type = String(msg.event || '');
  if (type === 'init') {
    loaded = true;
    failed.value = false;
    clearTimer();
    const win = iframeEl.value?.contentWindow;
    if (win) sendLoad(win);
    return;
  }
  const xml = xmlFromMessage(msg);
  if ((type === 'autosave' || type === 'change') && xml) {
    currentXml.value = xml;
    return;
  }
  if (type === 'save' && xml) {
    currentXml.value = xml;
    void persist(xml);
    if (msg.exit === true) open.value = false;
    return;
  }
  if (type === 'export' && xml) {
    currentXml.value = xml;
    if (pendingSave) void persist(xml);
  }
}

function armTimeout() {
  clearTimer();
  initTimer = setTimeout(() => {
    if (!loaded) failed.value = true;
  }, 20000);
}

async function loadResource() {
  if (!props.resourceId) return;
  loading.value = true;
  failed.value = false;
  try {
    const resource = await diGetById(props.resourceId);
    fileName.value = resource.fileName || resource.name || drawioFallbackName();
    if (resource.filePath) {
      const blob = await diFetchFileBlob(resource.filePath);
      const text = (await blob.text()).trim();
      currentXml.value = text || DI_EMPTY_DRAWIO_XML;
    } else {
      currentXml.value = DI_EMPTY_DRAWIO_XML;
    }
    savedXml.value = currentXml.value;
    if (loaded && iframeEl.value?.contentWindow) sendLoad(iframeEl.value.contentWindow);
  } catch (e: unknown) {
    failed.value = true;
    toast.push({
      title: t('errors.dg.toastTitle'),
      message: panelError(e, 'documentIntelligence.errors.preview'),
      severity: 'error',
    });
  } finally {
    loading.value = false;
  }
}

function onSave() {
  const win = iframeEl.value?.contentWindow;
  if (!win || !loaded) {
    void persist(currentXml.value);
    return;
  }
  pendingSave = true;
  requestExport(win);
}

function onClose() {
  if (dirty.value && !window.confirm(t('documentIntelligence.drawioUnsaved'))) return;
  open.value = false;
}

watch(
  () => [props.modelValue, props.resourceId] as const,
  ([isOpen, id]) => {
    if (isOpen && id) {
      loaded = false;
      failed.value = false;
      iframeKey.value += 1;
      armTimeout();
      void loadResource();
    } else if (!isOpen) {
      clearTimer();
      pendingSave = false;
    }
  },
);

onBeforeUnmount(() => {
  window.removeEventListener('message', onMessage);
  clearTimer();
});

watch(
  () => props.modelValue,
  (isOpen) => {
    if (isOpen) window.addEventListener('message', onMessage);
    else window.removeEventListener('message', onMessage);
  },
  { immediate: true },
);
</script>

<template>
  <v-dialog v-model="open" fullscreen scrim persistent>
    <v-card class="d-flex flex-column" min-height="100%">
      <v-card-title class="d-flex align-center ga-2 py-3">
        <v-icon icon="mdi-vector-polyline" color="primary" size="20" />
        <span class="text-subtitle-1 font-weight-bold text-truncate flex-grow-1">{{ heading }}</span>
        <v-chip v-if="dirty" size="small" color="warning" variant="tonal">
          {{ t('documentIntelligence.drawioDirty') }}
        </v-chip>
        <v-btn
          color="primary"
          class="text-none"
          prepend-icon="mdi-content-save-outline"
          :loading="saving"
          :disabled="!resourceId || saving"
          @click="onSave"
        >
          {{ t('documentIntelligence.save') }}
        </v-btn>
        <v-btn icon="mdi-close" variant="text" @click="onClose" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-0 flex-grow-1 di-drawio-editor-body">
        <iframe
          :key="iframeKey"
          ref="iframeEl"
          :src="editorSrc"
          class="di-drawio-editor-frame"
          :title="heading"
          referrerpolicy="no-referrer-when-downgrade"
          allow="clipboard-read; clipboard-write"
        />
        <div v-if="loading" class="di-drawio-editor-overlay">
          <v-progress-circular indeterminate color="primary" size="36" />
        </div>
        <div v-else-if="failed" class="di-drawio-editor-overlay pa-6">
          <v-alert type="warning" variant="tonal" class="rounded-lg">
            {{ t('documentIntelligence.drawioEditorOffline') }}
          </v-alert>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.di-drawio-editor-body {
  position: relative;
  min-height: 0;
  height: calc(100vh - 64px);
}
.di-drawio-editor-frame {
  width: 100%;
  height: 100%;
  border: 0;
  display: block;
}
.di-drawio-editor-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.35);
  z-index: 1;
}
</style>
