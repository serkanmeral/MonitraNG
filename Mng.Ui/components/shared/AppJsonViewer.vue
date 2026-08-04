<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(
  defineProps<{
    /** Pretty-printed JSON text (or raw object stringified by parent). */
    modelValue?: string | null;
    /** Optional compact height; default editor pane. */
    maxHeight?: string;
    /** Show line numbers gutter. */
    lineNumbers?: boolean;
  }>(),
  {
    modelValue: '',
    maxHeight: '320px',
    lineNumbers: true,
  },
);

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

/** Lightweight JSON highlighter — no extra deps. */
function highlightJsonLine(line: string): string {
  const re =
    /("(?:\\.|[^"\\])*")\s*:|("(?:\\.|[^"\\])*")|(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)|\b(true|false|null)\b|([{}[\],:])/g;
  let out = '';
  let last = 0;
  let m: RegExpExecArray | null;
  while ((m = re.exec(line)) !== null) {
    out += escapeHtml(line.slice(last, m.index));
    if (m[1] != null) {
      const key = m[1];
      const afterKey = line.slice(m.index + key.length, re.lastIndex);
      out += `<span class="ajv-key">${escapeHtml(key)}</span>`;
      out += escapeHtml(afterKey);
    } else if (m[2] != null) {
      out += `<span class="ajv-str">${escapeHtml(m[2])}</span>`;
    } else if (m[3] != null) {
      out += `<span class="ajv-num">${escapeHtml(m[3])}</span>`;
    } else if (m[4] != null) {
      out += `<span class="ajv-lit">${escapeHtml(m[4])}</span>`;
    } else if (m[5] != null) {
      out += `<span class="ajv-punc">${escapeHtml(m[5])}</span>`;
    }
    last = re.lastIndex;
  }
  out += escapeHtml(line.slice(last));
  return out || '&nbsp;';
}

const lines = computed(() => {
  const text = (props.modelValue ?? '').replace(/\r\n/g, '\n');
  if (!text.trim()) return [] as { n: number; html: string }[];
  return text.split('\n').map((line, i) => ({
    n: i + 1,
    html: highlightJsonLine(line),
  }));
});

const gutterWidth = computed(() => {
  const digits = String(Math.max(lines.value.length, 1)).length;
  return `${Math.max(digits, 2) + 1}ch`;
});
</script>

<template>
  <div
    v-if="lines.length"
    class="app-json-viewer"
    :style="{ maxHeight, '--ajv-gutter': gutterWidth }"
  >
    <div class="app-json-viewer__chrome">
      <span class="app-json-viewer__lang">JSON</span>
      <span class="app-json-viewer__meta">{{ lines.length }} lines</span>
    </div>
    <div class="app-json-viewer__scroll">
      <div
        v-for="line in lines"
        :key="line.n"
        class="app-json-viewer__row"
      >
        <span v-if="lineNumbers" class="app-json-viewer__gutter" aria-hidden="true">{{ line.n }}</span>
        <code class="app-json-viewer__code" v-html="line.html" />
      </div>
    </div>
  </div>
  <div v-else class="app-json-viewer app-json-viewer--empty text-body-2 text-medium-emphasis">
    —
  </div>
</template>

<style scoped>
.app-json-viewer {
  --ajv-bg: rgba(var(--v-theme-on-surface), 0.04);
  --ajv-border: rgba(var(--v-theme-on-surface), 0.12);
  --ajv-gutter-fg: rgba(var(--v-theme-on-surface), 0.38);
  --ajv-gutter-bg: rgba(var(--v-theme-on-surface), 0.03);
  --ajv-fg: rgba(var(--v-theme-on-surface), 0.87);
  --ajv-key: #1565c0;
  --ajv-str: #2e7d32;
  --ajv-num: #6a1b9a;
  --ajv-lit: #ad1457;
  --ajv-punc: rgba(var(--v-theme-on-surface), 0.55);

  display: flex;
  flex-direction: column;
  border: 1px solid var(--ajv-border);
  border-radius: 10px;
  background: var(--ajv-bg);
  overflow: hidden;
  font-family: ui-monospace, 'Cascadia Code', 'Consolas', 'SFMono-Regular', Menlo, monospace;
  font-size: 0.75rem;
  line-height: 1.55;
  color: var(--ajv-fg);
}

/* Darker pane when Vuetify dark theme is active */
:global(.v-theme--dark) .app-json-viewer {
  --ajv-bg: #0f1419;
  --ajv-border: rgba(255, 255, 255, 0.1);
  --ajv-gutter-fg: rgba(255, 255, 255, 0.35);
  --ajv-gutter-bg: #0b0f14;
  --ajv-fg: #e6edf3;
  --ajv-key: #79c0ff;
  --ajv-str: #a5d6ff;
  --ajv-num: #d2a8ff;
  --ajv-lit: #ff7b72;
  --ajv-punc: #8b949e;
}

.app-json-viewer--empty {
  padding: 16px;
}

.app-json-viewer__chrome {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 6px 12px;
  border-bottom: 1px solid var(--ajv-border);
  background: var(--ajv-gutter-bg);
}

.app-json-viewer__lang {
  font-size: 0.6875rem;
  font-weight: 700;
  letter-spacing: 0.06em;
  color: rgba(var(--v-theme-primary), 1);
}

.app-json-viewer__meta {
  font-size: 0.6875rem;
  color: var(--ajv-gutter-fg);
}

.app-json-viewer__scroll {
  flex: 1 1 auto;
  min-height: 0;
  overflow: auto;
  padding: 8px 0;
}

.app-json-viewer__row {
  display: grid;
  grid-template-columns: var(--ajv-gutter, 3ch) minmax(0, 1fr);
  min-height: 1.55em;
}

.app-json-viewer__row:hover {
  background: rgba(var(--v-theme-primary), 0.06);
}

.app-json-viewer__gutter {
  user-select: none;
  text-align: right;
  padding: 0 10px 0 8px;
  color: var(--ajv-gutter-fg);
  background: var(--ajv-gutter-bg);
  border-right: 1px solid var(--ajv-border);
  font-variant-numeric: tabular-nums;
}

.app-json-viewer__code {
  display: block;
  padding: 0 12px;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: inherit;
  font-size: inherit;
  background: transparent;
  color: inherit;
}

.app-json-viewer :deep(.ajv-key) {
  color: var(--ajv-key);
  font-weight: 600;
}

.app-json-viewer :deep(.ajv-str) {
  color: var(--ajv-str);
}

.app-json-viewer :deep(.ajv-num) {
  color: var(--ajv-num);
}

.app-json-viewer :deep(.ajv-lit) {
  color: var(--ajv-lit);
  font-weight: 600;
}

.app-json-viewer :deep(.ajv-punc) {
  color: var(--ajv-punc);
}
</style>
