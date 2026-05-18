<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import type { MonHttpAuthConfig } from '@/types/apps/httpAuthConfig';

const props = defineProps<{
  modelValue: boolean;
  config: MonHttpAuthConfig | Partial<MonHttpAuthConfig> | null;
  loading?: boolean;
  canEdit?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [v: boolean];
  save: [data: Partial<MonHttpAuthConfig>];
}>();

const TOKEN_METHODS = [
  { title: 'GET', value: 'GET' },
  { title: 'POST', value: 'POST' },
];

const TOKEN_BODY_TYPES = [
  { title: 'JSON', value: 'json' },
  { title: 'Form (x-www-form-urlencoded)', value: 'form' },
];

const form = ref<Partial<MonHttpAuthConfig>>({
  name: '',
  tokenUrl: '',
  tokenMethod: 'POST',
  tokenBodyType: 'json',
  tokenBody: {},
  tokenResponsePath: '$.access_token',
  description: null,
});

const tokenBodyJson = ref('{}');
const tokenBodyError = ref('');
const testLoading = ref(false);
const testResult = ref<{ success: boolean; message: string; tokenPreview?: string } | null>(null);

watch(
  () => props.config,
  (v) => {
    if (v) {
      form.value = {
        name: v.name ?? '',
        tokenUrl: v.tokenUrl ?? '',
        tokenMethod: (v.tokenMethod ?? 'POST') as 'GET' | 'POST',
        tokenBodyType: (v.tokenBodyType ?? 'json') as 'json' | 'form',
        tokenBody: v.tokenBody && typeof v.tokenBody === 'object' ? { ...v.tokenBody } : {},
        tokenResponsePath: v.tokenResponsePath ?? '$.access_token',
        description: v.description ?? null,
      };
      if ('__dataId' in v && v.__dataId) (form.value as any).__dataId = v.__dataId;
      tokenBodyJson.value = JSON.stringify(form.value.tokenBody ?? {}, null, 2);
      tokenBodyError.value = '';
      testResult.value = null;
    } else {
      form.value = { name: '', tokenUrl: '', tokenMethod: 'POST', tokenBodyType: 'json', tokenBody: {}, tokenResponsePath: '$.access_token', description: null };
      tokenBodyJson.value = '{}';
      tokenBodyError.value = '';
      testResult.value = null;
    }
  },
  { immediate: true }
);

watch(tokenBodyJson, (val) => {
  tokenBodyError.value = '';
  try {
    const parsed = JSON.parse(val || '{}');
    if (typeof parsed !== 'object') tokenBodyError.value = 'Geçerli bir JSON objesi girin';
    else form.value.tokenBody = parsed;
  } catch {
    tokenBodyError.value = 'Geçersiz JSON formatı';
  }
});

const isEdit = computed(() => !!(props.config && '__dataId' in props.config && (props.config as any).__dataId));

function parseTokenBody(): Record<string, unknown> | null {
  try {
    const parsed = JSON.parse(tokenBodyJson.value || '{}');
    return typeof parsed === 'object' && parsed !== null ? parsed : {};
  } catch {
    return null;
  }
}

function save() {
  const name = (form.value.name ?? '').trim();
  const tokenUrl = (form.value.tokenUrl ?? '').trim();
  const tokenResponsePath = (form.value.tokenResponsePath ?? '').trim();
  if (!name || !tokenUrl || !tokenResponsePath) return;

  const body = parseTokenBody();
  if (body === null) {
    tokenBodyError.value = 'Geçersiz JSON formatı';
    return;
  }

  emit('save', {
    ...form.value,
    name,
    tokenUrl,
    tokenMethod: form.value.tokenMethod ?? 'POST',
    tokenBodyType: form.value.tokenBodyType ?? 'json',
    tokenBody: body,
    tokenResponsePath,
    description: (form.value.description ?? '').trim() || null,
  });
  emit('update:modelValue', false);
}

function close() {
  emit('update:modelValue', false);
}

/** Basit JSON path: $.key1.key2 -> obj.key1.key2. Ayrıca yaygın token anahtarları için fallback. */
function extractByPath(obj: unknown, path: string): unknown {
  const p = (path || '').trim().replace(/^\$\.?/, '');
  if (!p) return obj;

  const traverse = (o: unknown, parts: string[]): unknown => {
    if (parts.length === 0) return o;
    if (o == null || typeof o !== 'object') return undefined;
    const part = parts[0];
    const m = part.match(/^(\w+)(\[\d+\])?$/);
    const key = m ? m[1] : part;
    let next = (o as Record<string, unknown>)[key];
    if (m?.[2]) {
      const idx = parseInt(m[2].replace(/[\[\]]/g, ''), 10);
      next = Array.isArray(next) ? next[idx] : undefined;
    }
    return traverse(next, parts.slice(1));
  };

  let result = traverse(obj, p.split('.'));
  if (result != null && result !== '') return result;

  // Fallback: access_token / accessToken gibi yaygın token anahtarları (Keycloak, OAuth vb.)
  if (typeof obj === 'object' && obj !== null && p.toLowerCase().includes('access') && p.toLowerCase().includes('token')) {
    const o = obj as Record<string, unknown>;
    result = o.accessToken ?? o.access_token ?? o.token;
    if (result != null && result !== '') return result;
  }
  return undefined;
}

async function runTest() {
  const tokenUrl = (form.value.tokenUrl ?? '').trim();
  const tokenResponsePath = (form.value.tokenResponsePath ?? '').trim();
  if (!tokenUrl || !tokenResponsePath) {
    testResult.value = { success: false, message: 'Token URL ve Response Path gerekli' };
    return;
  }

  const body = parseTokenBody();
  if (body === null) {
    testResult.value = { success: false, message: 'Geçersiz Token Body JSON' };
    return;
  }

  testLoading.value = true;
  testResult.value = null;
  try {
    const res = await $fetch<{ __success?: boolean; __error?: string; __status?: number; __response?: unknown }>(
      '/api/test-token-endpoint',
      {
        method: 'POST',
        body: {
          tokenUrl,
          tokenMethod: form.value.tokenMethod ?? 'POST',
          tokenBodyType: form.value.tokenBodyType ?? 'json',
          tokenBody: body,
        },
      }
    );

    if (res.__success === false) {
      const err = res.__error || 'Bilinmeyen hata';
      const status = res.__status ? ` (HTTP ${res.__status})` : '';
      testResult.value = { success: false, message: `${err}${status}` };
      return;
    }

    const response = res.__response;
    const token = extractByPath(response, tokenResponsePath);
    if (token == null || token === '') {
      testResult.value = {
        success: false,
        message: `Response path "${tokenResponsePath}" değer bulunamadı`,
      };
      return;
    }

    const tokenStr = typeof token === 'string' ? token : String(token);
    const preview = tokenStr.length > 20 ? tokenStr.slice(0, 20) + '...' : tokenStr;
    testResult.value = { success: true, message: 'Token başarıyla alındı', tokenPreview: preview };
  } catch (e: any) {
    testResult.value = { success: false, message: e?.message || e?.data?.message || 'İstek başarısız' };
  } finally {
    testLoading.value = false;
  }
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="620" persistent scrollable @update:model-value="(v) => emit('update:modelValue', v)">
    <v-card>
      <v-card-title>{{ isEdit ? 'HTTP Auth tanımı düzenle' : 'Yeni HTTP Auth tanımı' }}</v-card-title>
      <v-card-text class="pb-0">
        <v-text-field
          v-model="form.name"
          label="Tanım adı *"
          variant="outlined"
          density="comfortable"
          class="mb-3"
          hide-details
          placeholder="Örn: Sensor API Token"
        />
        <v-text-field
          v-model="form.tokenUrl"
          label="Token endpoint URL *"
          variant="outlined"
          density="comfortable"
          class="mb-3"
          hide-details
          placeholder="https://api.example.com/auth/token"
        />
        <v-row dense>
          <v-col cols="6">
            <v-select
              v-model="form.tokenMethod"
              :items="TOKEN_METHODS"
              item-title="title"
              item-value="value"
              label="HTTP metodu"
              variant="outlined"
              density="comfortable"
              hide-details
            />
          </v-col>
          <v-col cols="6">
            <v-select
              v-model="form.tokenBodyType"
              :items="TOKEN_BODY_TYPES"
              item-title="title"
              item-value="value"
              label="Body tipi"
              variant="outlined"
              density="comfortable"
              hide-details
            />
          </v-col>
        </v-row>
        <div class="mt-3">
          <label class="v-label text-body-2 mb-1 d-block">Token istek body (JSON) *</label>
          <v-textarea
            v-model="tokenBodyJson"
            variant="outlined"
            density="comfortable"
            rows="5"
            class="font-monospace"
            :error-messages="tokenBodyError"
            placeholder='{"username":"...","password":"...","grant_type":"password"}'
          />
        </div>
        <v-text-field
          v-model="form.tokenResponsePath"
          label="Token response path (JSON Path) *"
          variant="outlined"
          density="comfortable"
          class="mt-3"
          hide-details
          placeholder="$.access_token veya $.accessToken"
        />
        <v-textarea
          v-model="form.description"
          label="Açıklama"
          variant="outlined"
          density="comfortable"
          rows="2"
          class="mt-3"
          hide-details
        />

        <v-alert v-if="testResult" :type="testResult.success ? 'success' : 'error'" variant="tonal" density="compact" class="mt-3" closable @click:close="testResult = null">
          {{ testResult.message }}
          <template v-if="testResult.success && testResult.tokenPreview">
            <br /><span class="font-monospace text-caption">Token: {{ testResult.tokenPreview }}</span>
          </template>
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-btn variant="tonal" color="secondary" :loading="testLoading" :disabled="!!tokenBodyError" @click="runTest">
          Test Et
        </v-btn>
        <v-spacer />
        <v-btn variant="text" @click="close">İptal</v-btn>
        <v-btn v-if="canEdit" color="primary" variant="flat" :loading="loading" :disabled="!!tokenBodyError" @click="save">Kaydet</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
