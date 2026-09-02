<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

const props = defineProps<{
  xml: string;
  title?: string;
}>();

const { t } = useAppI18n();
const iframeEl = ref<HTMLIFrameElement | null>(null);
const failed = ref(false);
let initTimer: ReturnType<typeof setTimeout> | null = null;
let loaded = false;

const embedSrc =
  'https://embed.diagrams.net/?embed=1&ui=min&spin=1&proto=json&chrome=0&lightbox=1&nav=1&layers=1&modified=0&saveAndExit=0&noSaveBtn=1&noExitBtn=1';

function clearTimer() {
  if (initTimer) {
    clearTimeout(initTimer);
    initTimer = null;
  }
}

function sendLoad(win: Window) {
  win.postMessage(JSON.stringify({ action: 'load', autosave: 0, xml: props.xml }), '*');
}

function onMessage(event: MessageEvent) {
  if (typeof event.data !== 'string' || !event.data.length) return;
  let msg: { event?: string } | null = null;
  try {
    msg = JSON.parse(event.data) as { event?: string };
  } catch {
    return;
  }
  if (msg?.event === 'init') {
    loaded = true;
    failed.value = false;
    clearTimer();
    const win = iframeEl.value?.contentWindow;
    if (win) sendLoad(win);
  }
}

function armTimeout() {
  clearTimer();
  loaded = false;
  failed.value = false;
  initTimer = setTimeout(() => {
    if (!loaded) failed.value = true;
  }, 8000);
}

onMounted(() => {
  window.addEventListener('message', onMessage);
  armTimeout();
});

onBeforeUnmount(() => {
  window.removeEventListener('message', onMessage);
  clearTimer();
});

watch(
  () => props.xml,
  () => {
    if (loaded && iframeEl.value?.contentWindow) {
      sendLoad(iframeEl.value.contentWindow);
    } else {
      armTimeout();
    }
  },
);
</script>

<template>
  <div class="di-drawio-viewer">
    <iframe
      v-show="!failed"
      ref="iframeEl"
      :src="embedSrc"
      class="di-drawio-frame"
      :title="title || 'draw.io'"
      referrerpolicy="no-referrer-when-downgrade"
    />
    <div v-if="failed" class="di-drawio-fallback pa-4">
      <p class="text-body-2 text-medium-emphasis mb-3">
        {{ t('documentIntelligence.drawioPreviewOffline') }}
      </p>
      <pre class="di-drawio-xml">{{ xml.slice(0, 8000) }}</pre>
    </div>
  </div>
</template>

<style scoped>
.di-drawio-viewer {
  min-height: 420px;
}

.di-drawio-frame {
  width: 100%;
  height: 70vh;
  min-height: 420px;
  border: 0;
  display: block;
}

.di-drawio-xml {
  margin: 0;
  max-height: 40vh;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: 'Roboto Mono', ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 0.75rem;
  line-height: 1.45;
}
</style>
