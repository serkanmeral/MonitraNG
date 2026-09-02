<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  fetchAgentPackages,
  type AgentPackageCatalog,
  type AgentPackageDto,
} from '@/services/siemAgentPackageService';

const props = defineProps<{
  open: boolean;
}>();

const emit = defineEmits<{
  'update:open': [value: boolean];
}>();

const { t } = useAppI18n();
const runtime = useRuntimeConfig();

const dialogOpen = computed({
  get: () => props.open,
  set: (v: boolean) => emit('update:open', v),
});

const loading = ref(false);
const error = ref<string | null>(null);
const catalog = ref<AgentPackageCatalog | null>(null);
const copiedKey = ref<string | null>(null);
let copiedTimer: ReturnType<typeof setTimeout> | undefined;

const collectorUrl = computed(() => {
  const fromApi = catalog.value?.collectorBaseUrl?.trim();
  if (fromApi) return fromApi.replace(/\/$/, '');
  const fromEnv = String(runtime.public.logCollectorUrl || '').trim().replace(/\/$/, '');
  return fromEnv;
});

const windows = computed(() => catalog.value?.packages.find((p) => p.id === 'windows') ?? null);
const linux = computed(() => catalog.value?.packages.find((p) => p.id === 'linux') ?? null);

watch(
  () => props.open,
  (open) => {
    if (open) void load();
  },
);

async function load() {
  loading.value = true;
  error.value = null;
  try {
    catalog.value = await fetchAgentPackages();
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('siemCenter.discovery.agentInstall.loadError');
    catalog.value = null;
  } finally {
    loading.value = false;
  }
}

function formatSize(bytes: number): string {
  if (!bytes || bytes < 0) return '—';
  if (bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb < 10 ? kb.toFixed(1) : Math.round(kb)} KB`;
  const mb = kb / 1024;
  return `${mb < 10 ? mb.toFixed(1) : Math.round(mb)} MB`;
}

function windowsLocalCmd(pkg: AgentPackageDto): string {
  return t('siemCenter.discovery.agentInstall.windowsLocalCmd', {
    fileName: pkg.fileName,
    collectorUrl: collectorUrl.value,
  });
}

function windowsTargetCmd(pkg: AgentPackageDto): string {
  return t('siemCenter.discovery.agentInstall.windowsTargetCmd', {
    downloadUrl: pkg.downloadUrl,
    fileName: pkg.fileName,
    collectorUrl: collectorUrl.value,
  });
}

function linuxTargetCmd(pkg: AgentPackageDto): string {
  return t('siemCenter.discovery.agentInstall.linuxTargetCmd', {
    downloadUrl: pkg.downloadUrl,
    fileName: pkg.fileName,
    collectorUrl: collectorUrl.value,
  });
}

async function copyText(key: string, text: string) {
  try {
    await navigator.clipboard.writeText(text);
    copiedKey.value = key;
    if (copiedTimer) clearTimeout(copiedTimer);
    copiedTimer = setTimeout(() => {
      copiedKey.value = null;
    }, 2000);
  } catch {
    copiedKey.value = 'failed';
  }
}
</script>

<template>
  <v-dialog v-model="dialogOpen" max-width="760" scrollable>
    <v-card>
      <v-card-title class="d-flex align-center justify-space-between">
        <span>{{ t('siemCenter.discovery.agentInstall.title') }}</span>
        <v-btn icon="mdi-close" variant="text" @click="dialogOpen = false" />
      </v-card-title>
      <v-card-subtitle class="text-wrap pb-2">
        {{ t('siemCenter.discovery.agentInstall.subtitle') }}
      </v-card-subtitle>
      <v-card-text>
        <v-alert v-if="error" type="warning" variant="tonal" density="comfortable" class="mb-3">
          {{ error }}
        </v-alert>

        <div v-if="loading" class="d-flex justify-center py-8">
          <v-progress-circular indeterminate color="primary" />
        </div>

        <template v-else>
          <div class="text-body-2 mb-4">
            <span class="text-medium-emphasis">{{ t('siemCenter.discovery.agentInstall.collector') }}:</span>
            <code class="ms-2">{{ collectorUrl || '—' }}</code>
          </div>

          <v-alert
            v-if="!windows && !linux"
            type="info"
            variant="tonal"
            density="comfortable"
            class="mb-3"
          >
            {{ t('siemCenter.discovery.agentInstall.empty') }}
          </v-alert>

          <div v-for="pkg in [windows, linux].filter(Boolean) as AgentPackageDto[]" :key="pkg.id" class="mb-5">
            <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-2">
              <div>
                <div class="text-body-1 font-weight-medium">
                  {{ pkg.id === 'windows'
                    ? t('siemCenter.discovery.agentInstall.windows')
                    : t('siemCenter.discovery.agentInstall.linux') }}
                </div>
                <div class="text-caption text-medium-emphasis">
                  {{ pkg.fileName }}
                  <span v-if="pkg.version">
                    · {{ t('siemCenter.discovery.agentInstall.version', { version: pkg.version }) }}
                  </span>
                  · {{ formatSize(pkg.sizeBytes) }}
                </div>
              </div>
              <v-btn
                color="primary"
                variant="flat"
                prepend-icon="mdi-download"
                :href="pkg.downloadUrl"
                target="_blank"
                rel="noopener"
              >
                {{ t('siemCenter.discovery.agentInstall.download') }}
              </v-btn>
            </div>
            <div v-if="pkg.sha256" class="text-caption text-medium-emphasis mb-2 text-break">
              {{ t('siemCenter.discovery.agentInstall.sha256') }}: {{ pkg.sha256 }}
            </div>

            <div class="text-caption font-weight-medium mb-1">
              {{ pkg.id === 'windows'
                ? t('siemCenter.discovery.agentInstall.onThisPc')
                : t('siemCenter.discovery.agentInstall.onTarget') }}
            </div>
            <v-textarea
              :model-value="pkg.id === 'windows' ? windowsLocalCmd(pkg) : linuxTargetCmd(pkg)"
              readonly
              auto-grow
              rows="2"
              variant="outlined"
              density="compact"
              hide-details
              class="mb-2 font-monospace"
            />
            <v-btn
              size="small"
              variant="tonal"
              prepend-icon="mdi-content-copy"
              class="mb-3"
              @click="copyText(pkg.id + '-local', pkg.id === 'windows' ? windowsLocalCmd(pkg) : linuxTargetCmd(pkg))"
            >
              {{ copiedKey === pkg.id + '-local'
                ? t('siemCenter.discovery.agentInstall.copied')
                : t('siemCenter.discovery.agentInstall.copyCommand') }}
            </v-btn>

            <div v-if="pkg.id === 'windows'" class="text-caption font-weight-medium mb-1">
              {{ t('siemCenter.discovery.agentInstall.onTarget') }}
            </div>
            <template v-if="pkg.id === 'windows'">
              <v-textarea
                :model-value="windowsTargetCmd(pkg)"
                readonly
                auto-grow
                rows="3"
                variant="outlined"
                density="compact"
                hide-details
                class="mb-2"
              />
              <v-btn
                size="small"
                variant="tonal"
                prepend-icon="mdi-content-copy"
                @click="copyText('windows-target', windowsTargetCmd(pkg))"
              >
                {{ copiedKey === 'windows-target'
                  ? t('siemCenter.discovery.agentInstall.copied')
                  : t('siemCenter.discovery.agentInstall.copyCommand') }}
              </v-btn>
            </template>
          </div>
        </template>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>
