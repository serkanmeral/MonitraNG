<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import OdakSiparisPoDocumentSection from '@/components/apps/odak-siparis/OdakSiparisPoDocumentSection.vue';
import { useOdakFieldAccess } from '@/composables/useOdakFieldAccess';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { packageRecordForPolicyEval } from '@/utils/odakSiparisFieldPolicies';
import type { OdakPackageRow, OdakPoDocumentScope } from '@/utils/odakSiparisConfig';
import {
  loadOdakHubFieldPoliciesOnly,
  loadOdakPackagePoDocumentAccessOnly,
} from '@/utils/odakSiparisHubSettingsService';
import {
  canViewRestrictedPoDocuments,
  type OdakPackagePoDocumentAccessConfig,
} from '@/utils/odakSiparisPoDocumentAccess';
import {
  appendPoDocumentUpload,
  buildPoUploadPayload,
  downloadPoEntry,
  hasAnyPendingPoUpload,
  hasAnyStoredPoDocument,
  isPoDocumentsStateDirty,
  loadPackagePoState,
  resolvePoEntryPreviewBlobUrl,
  savePackagePoDocuments,
  type PoDocumentEntry,
} from '@/utils/odakSiparisPoService';
import { XIcon } from 'vue-tabler-icons';

const props = defineProps<{
  packageId: string;
  packageNo?: string;
}>();

const emit = defineEmits<{
  saved: [];
}>();

const { t } = useAppI18n();
const auth = useAuthStore();

const loading = ref(false);
const saving = ref(false);
const errorMessage = ref('');
const poDocumentsGlobal = ref<unknown>(null);
const poDocumentsRestricted = ref<unknown>(null);
const savedGlobal = ref<unknown>(null);
const savedRestricted = ref<unknown>(null);
const poVersion = ref('');
const savedPoVersion = ref('');
const accessConfig = ref<OdakPackagePoDocumentAccessConfig>({ restrictedViewerGroups: [] });
const fieldPolicies = ref<OdakFieldPoliciesBlob>({ policiesByField: {} });

const previewDialogOpen = ref(false);
const previewDialogLoading = ref(false);
const previewDialogError = ref('');
const previewDialogUrl = ref<string | null>(null);
const previewDialogObjectUrl = ref<string | null>(null);
const previewDialogFileName = ref('');
const previewScope = ref<OdakPoDocumentScope>('global');
const previewEntry = ref<PoDocumentEntry | null>(null);

const userGroups = computed(() => auth.userGroups);
const policyRecord = computed(() =>
  packageRecordForPolicyEval({ poVersion: poVersion.value } as OdakPackageRow)
);
const { canViewField, canEditField } = useOdakFieldAccess(fieldPolicies);

const canViewGlobal = computed(() => canViewField('poDocumentsGlobal', policyRecord.value));
const canEditGlobal = computed(() => canEditField('poDocumentsGlobal', policyRecord.value));
const canViewRestrictedSection = computed(
  () =>
    canViewRestrictedPoDocuments(userGroups.value, accessConfig.value) &&
    canViewField('poDocumentsRestricted', policyRecord.value)
);
const canEditRestricted = computed(() => canEditField('poDocumentsRestricted', policyRecord.value));

const hasStored = computed(() =>
  hasAnyStoredPoDocument(poDocumentsGlobal.value, poDocumentsRestricted.value)
);
const hasPending = computed(() =>
  hasAnyPendingPoUpload(poDocumentsGlobal.value, poDocumentsRestricted.value)
);

const dirty = computed(() =>
  isPoDocumentsStateDirty(
    {
      global: poDocumentsGlobal.value,
      restricted: poDocumentsRestricted.value,
      poVersion: poVersion.value,
    },
    {
      global: savedGlobal.value,
      restricted: savedRestricted.value,
      poVersion: savedPoVersion.value,
    }
  )
);

function revokePreviewObjectUrl() {
  if (previewDialogObjectUrl.value) {
    URL.revokeObjectURL(previewDialogObjectUrl.value);
    previewDialogObjectUrl.value = null;
  }
}

function closePreviewDialog() {
  previewDialogOpen.value = false;
  previewDialogUrl.value = null;
  previewDialogError.value = '';
  previewDialogFileName.value = '';
  previewEntry.value = null;
  revokePreviewObjectUrl();
}

async function reload() {
  if (!props.packageId) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    const [access, policies] = await Promise.all([
      loadOdakPackagePoDocumentAccessOnly(),
      loadOdakHubFieldPoliciesOnly('field_policies'),
    ]);
    accessConfig.value = access;
    fieldPolicies.value = policies;

    const state = await loadPackagePoState(props.packageId, {
      userGroups: userGroups.value,
      accessConfig: access,
    });

    poDocumentsGlobal.value = state.poDocumentsGlobal;
    poDocumentsRestricted.value = state.poDocumentsRestricted;
    savedGlobal.value = state.poDocumentsGlobal;
    savedRestricted.value = state.poDocumentsRestricted;
    poVersion.value = state.poVersion;
    savedPoVersion.value = state.poVersion;
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

async function savePo() {
  if (!props.packageId || !dirty.value) return;
  saving.value = true;
  errorMessage.value = '';
  try {
    await savePackagePoDocuments(props.packageId, {
      poDocumentsGlobal: canViewGlobal.value ? poDocumentsGlobal.value : savedGlobal.value,
      poDocumentsRestricted: canViewRestrictedSection.value
        ? poDocumentsRestricted.value
        : savedRestricted.value,
      poVersion: poVersion.value,
    });
    await reload();
    emit('saved');
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

function mapUploadError(e: unknown): string {
  if (e instanceof Error && e.message === 'PDF only') return t('odakSiparis.po.errors.pdfOnly');
  if (e instanceof Error && e.message === 'File too large') {
    return t('odakSiparis.po.errors.tooLarge', { max: 25 });
  }
  return e instanceof Error ? e.message : String(e);
}

async function addFiles(scope: OdakPoDocumentScope, files: FileList) {
  errorMessage.value = '';
  try {
    let current = scope === 'global' ? poDocumentsGlobal.value : poDocumentsRestricted.value;
    const prefix = scope === 'global' ? 'global' : 'restricted';
    for (const file of Array.from(files)) {
      const payload = await buildPoUploadPayload(file, scope);
      current = appendPoDocumentUpload(current, payload, prefix);
    }
    if (scope === 'global') poDocumentsGlobal.value = current;
    else poDocumentsRestricted.value = current;
  } catch (e: unknown) {
    errorMessage.value = mapUploadError(e);
  }
}

async function openPreviewModal(entry: PoDocumentEntry, scope: OdakPoDocumentScope) {
  previewScope.value = scope;
  previewEntry.value = entry;
  revokePreviewObjectUrl();
  previewDialogUrl.value = null;
  previewDialogError.value = '';
  previewDialogFileName.value = entry.fileName;
  previewDialogOpen.value = true;
  previewDialogLoading.value = true;
  try {
    const url = await resolvePoEntryPreviewBlobUrl(
      entry,
      scope,
      userGroups.value,
      accessConfig.value
    );
    previewDialogUrl.value = url;
    if (url.startsWith('blob:')) previewDialogObjectUrl.value = url;
  } catch (e: unknown) {
    if (e instanceof Error && e.message === 'PO access denied') {
      previewDialogError.value = t('odakSiparis.po.errors.accessDenied');
    } else {
      previewDialogError.value = e instanceof Error ? e.message : String(e);
    }
  } finally {
    previewDialogLoading.value = false;
  }
}

async function downloadEntry(entry: PoDocumentEntry, scope: OdakPoDocumentScope) {
  errorMessage.value = '';
  try {
    await downloadPoEntry(entry, scope, userGroups.value, accessConfig.value);
  } catch (e: unknown) {
    if (e instanceof Error && e.message === 'PO access denied') {
      errorMessage.value = t('odakSiparis.po.errors.accessDenied');
    } else {
      errorMessage.value = e instanceof Error ? e.message : String(e);
    }
  }
}

function previewScopeFromKey(key: string): OdakPoDocumentScope {
  return key.startsWith('restricted') ? 'restricted' : 'global';
}

watch(
  () => props.packageId,
  () => {
    void reload();
  },
  { immediate: true }
);

onBeforeUnmount(() => {
  revokePreviewObjectUrl();
});
</script>

<template>
  <v-card variant="outlined" class="odak-po-panel h-100 d-flex flex-column">
    <v-card-title class="text-subtitle-2 py-2 px-3 d-flex align-center flex-wrap ga-2">
      <span>{{ t('odakSiparis.po.titleShort') }}</span>
      <v-chip v-if="hasStored && !hasPending" size="x-small" color="success" variant="tonal">
        {{ t('odakSiparis.po.hasDocument') }}
      </v-chip>
      <v-chip v-else-if="hasPending" size="x-small" color="warning" variant="tonal">
        {{ t('odakSiparis.po.pendingUpload') }}
      </v-chip>
    </v-card-title>
    <v-divider />

    <v-card-text class="px-3 py-2 flex-grow-1 d-flex flex-column ga-3">
      <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact">
        {{ errorMessage }}
      </v-alert>
      <v-progress-linear v-if="loading" indeterminate color="primary" />

      <OdakSiparisPoDocumentSection
        v-if="canViewGlobal"
        v-model="poDocumentsGlobal"
        :title="t('odakSiparis.po.sections.global')"
        :hint="t('odakSiparis.po.sections.globalHint')"
        :readonly="!canEditGlobal"
        :package-no="packageNo"
        key-prefix="global"
        @add-files="addFiles('global', $event)"
        @preview="openPreviewModal($event, 'global')"
        @download="downloadEntry($event, 'global')"
      />

      <OdakSiparisPoDocumentSection
        v-if="canViewRestrictedSection"
        v-model="poDocumentsRestricted"
        :title="t('odakSiparis.po.sections.restricted')"
        :hint="t('odakSiparis.po.sections.restrictedHint')"
        :readonly="!canEditRestricted"
        :package-no="packageNo"
        key-prefix="restricted"
        @add-files="addFiles('restricted', $event)"
        @preview="openPreviewModal($event, 'restricted')"
        @download="downloadEntry($event, 'restricted')"
      />

      <v-alert
        v-if="!canViewGlobal && !canViewRestrictedSection && !loading"
        type="info"
        variant="tonal"
        density="compact"
      >
        {{ t('odakSiparis.po.noAccess') }}
      </v-alert>

      <v-text-field
        v-model="poVersion"
        :label="t('odakSiparis.packages.fields.poVersion')"
        variant="outlined"
        density="compact"
        hide-details
        :disabled="loading || saving"
      />

      <div class="d-flex justify-end">
        <v-btn
          color="primary"
          variant="flat"
          size="small"
          :loading="saving"
          :disabled="!dirty || loading"
          @click="savePo"
        >
          {{ t('odakSiparis.po.save') }}
        </v-btn>
      </div>
    </v-card-text>

    <v-dialog v-model="previewDialogOpen" max-width="960" scrollable @after-leave="closePreviewDialog">
      <v-card class="odak-po-preview-dialog">
        <v-card-title class="d-flex align-center py-2 px-3">
          <span class="text-subtitle-2">{{ t('odakSiparis.po.previewDialogTitle') }}</span>
          <span class="text-caption text-medium-emphasis ms-2 text-truncate flex-grow-1">
            {{ previewDialogFileName }}
          </span>
          <v-btn icon variant="text" size="small" @click="previewDialogOpen = false">
            <XIcon size="18" />
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-0 odak-po-preview-dialog__body">
          <div v-if="previewDialogLoading" class="d-flex justify-center align-center py-16">
            <v-progress-circular indeterminate color="primary" size="48" />
          </div>
          <v-alert
            v-else-if="previewDialogError"
            type="warning"
            variant="tonal"
            density="compact"
            class="ma-3"
          >
            {{ previewDialogError || t('odakSiparis.po.previewFailed') }}
            <v-btn
              v-if="previewEntry"
              size="x-small"
              variant="text"
              class="ms-1"
              @click="downloadEntry(previewEntry, previewScopeFromKey(previewEntry.key))"
            >
              {{ t('odakSiparis.po.downloadInstead') }}
            </v-btn>
          </v-alert>
          <iframe
            v-else-if="previewDialogUrl"
            :key="previewDialogUrl"
            :src="previewDialogUrl"
            class="odak-po-preview-dialog__iframe"
            :title="previewDialogFileName || 'PDF'"
          />
        </v-card-text>
      </v-card>
    </v-dialog>
  </v-card>
</template>

<style scoped>
.odak-po-panel {
  background: rgba(var(--v-theme-surface), 1);
  min-height: 0;
}

.odak-po-preview-dialog__body {
  min-height: 70vh;
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.odak-po-preview-dialog__iframe {
  width: 100%;
  height: 78vh;
  min-height: 480px;
  border: 0;
  display: block;
  background: #525659;
}
</style>
