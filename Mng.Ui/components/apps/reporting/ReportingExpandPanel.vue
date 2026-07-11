<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import ReportingChildListPanel from '@/components/apps/reporting/ReportingChildListPanel.vue';
import ReportingDetailView from '@/components/apps/reporting/ReportingDetailView.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type {
  ReportingDocumentBinding,
  ReportingExpandAction,
  ReportingExpandConfig,
} from '@/types/apps/reporting';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import { emptyOdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import {
  parsedReportingExpandLayout,
  reportingExpandChildTabs,
  reportingExpandFieldsTabTitle,
  reportingRowId,
  resolveReportingExpandNavigatePath,
} from '@/utils/reportingExpandLayout';
import { ensureOdakEgitimParticipantsExpandTab } from '@/utils/reportingOdakEgitimExpandMigrations';
import { filterVisibleReportingExpandChildTabs } from '@/utils/reportingReportAccess';
import { generateReportingParentRowDocument } from '@/utils/reportingParentRowDocument';
import { openDiResourceInNewTab } from '@/utils/diResourceLink';
import { FileTextIcon } from 'vue-tabler-icons';

const props = defineProps<{
  row: Record<string, unknown>;
  expandConfig: ReportingExpandConfig;
  /** Parent rapor dataset — odak eğitim expand sekmeleri için. */
  datasetName?: string;
  fields: FieldDefinition[];
  listConfig: OdakHubListConfig;
  canViewField?: (fieldName: string, row: Record<string, unknown>) => boolean;
  /** Runner: parentRow belge üretimi. */
  reportId?: string | null;
  reportTitle?: string | null;
  documentBindings?: ReportingDocumentBinding[];
  enableParentRowDocuments?: boolean;
}>();

const router = useRouter();
const { t } = useAppI18n();
const authStore = useAuthStore();

const effectiveExpandConfig = computed(() =>
  ensureOdakEgitimParticipantsExpandTab(props.expandConfig, props.datasetName ?? '').expand
);

const layout = computed(() => parsedReportingExpandLayout(effectiveExpandConfig.value));
const allChildTabs = computed(() => reportingExpandChildTabs(effectiveExpandConfig.value));
const childTabs = computed(() =>
  filterVisibleReportingExpandChildTabs(allChildTabs.value, authStore.userGroups)
);

/** Child sekmeler tanımlıysa şerit göster (gizli sekmeler filtrelenir). */
const showTabStrip = computed(() => allChildTabs.value.length > 0);

const fieldsTabTitle = computed(
  () => reportingExpandFieldsTabTitle(effectiveExpandConfig.value) || t('reporting.expand.fieldsTab')
);

const initialTab = computed(() => {
  const preferred = effectiveExpandConfig.value.defaultTabId?.trim();
  if (preferred && preferred !== 'fields' && childTabs.value.some((tab) => tab.id === preferred)) {
    return preferred;
  }
  return 'fields';
});

const activeTab = ref(initialTab.value);

const parentRowBindings = computed(() =>
  (props.documentBindings ?? []).filter((b) => b.contextType === 'parentRow')
);

const showParentRowDocuments = computed(
  () =>
    props.enableParentRowDocuments === true &&
    !!props.reportId &&
    parentRowBindings.value.length > 0
);

const docGeneratingId = ref<string | null>(null);
const docError = ref('');
const docSuccess = ref('');
const lastDocResourceId = ref<string | null>(null);

watch(
  () => reportingRowId(props.row),
  () => {
    activeTab.value = initialTab.value;
    docError.value = '';
    docSuccess.value = '';
    lastDocResourceId.value = null;
  }
);

watch(childTabs, (tabs) => {
  if (activeTab.value !== 'fields' && !tabs.some((tab) => tab.id === activeTab.value)) {
    activeTab.value = 'fields';
  }
});

function tabFieldPolicies(tab: (typeof childTabs.value)[number]) {
  return tab.fieldPolicies ?? emptyOdakFieldPoliciesBlob();
}

function navigateActionPath(action: ReportingExpandAction): string | null {
  if (action.type !== 'navigate') return null;
  const template = String(action.config?.path ?? '').trim();
  if (!template) return null;
  return resolveReportingExpandNavigatePath(template, props.row);
}

function onActionClick(action: ReportingExpandAction) {
  const path = navigateActionPath(action);
  if (!path) return;
  void router.push(path);
}

function actionDisabled(action: ReportingExpandAction): boolean {
  if (action.type !== 'navigate') return true;
  return !reportingRowId(props.row) || !navigateActionPath(action);
}

function openLastDocInDi() {
  openDiResourceInNewTab(lastDocResourceId.value);
}

async function generateParentRowDocument(binding: ReportingDocumentBinding) {
  if (!props.reportId) return;
  docGeneratingId.value = binding.id;
  docError.value = '';
  docSuccess.value = '';
  lastDocResourceId.value = null;
  try {
    await authStore.ensureValidToken();
    const result = await generateReportingParentRowDocument({
      reportId: props.reportId,
      reportTitle: props.reportTitle ?? '',
      binding,
      row: props.row,
      listConfig: props.listConfig,
    });
    lastDocResourceId.value = result.resourceId || null;
    docSuccess.value = t('reporting.runner.generateSuccess', {
      fileName: result.fileName || binding.label,
    });
  } catch (e: unknown) {
    docError.value =
      e instanceof Error ? e.message : t('reporting.runner.generateFailed');
  } finally {
    docGeneratingId.value = null;
  }
}
</script>

<template>
  <div class="reporting-expand-panel pa-4 bg-grey-lighten-5">
    <div v-if="effectiveExpandConfig.actions?.length" class="d-flex flex-wrap ga-2 mb-3">
      <v-btn
        v-for="action in effectiveExpandConfig.actions"
        :key="action.id"
        size="small"
        variant="tonal"
        color="primary"
        :disabled="actionDisabled(action)"
        @click="onActionClick(action)"
      >
        {{ action.label }}
      </v-btn>
    </div>

    <div v-if="showParentRowDocuments" class="mb-3">
      <div class="text-caption text-medium-emphasis mb-2">
        {{ t('reporting.runner.parentRowDocuments') }}
      </div>
      <v-alert v-if="docError" type="error" variant="tonal" density="compact" class="mb-2">
        {{ docError }}
      </v-alert>
      <v-alert v-if="docSuccess" type="success" variant="tonal" density="compact" class="mb-2">
        {{ docSuccess }}
        <v-btn
          v-if="lastDocResourceId"
          class="ml-2"
          size="small"
          variant="text"
          @click="openLastDocInDi"
        >
          {{ t('reporting.runner.openInDi') }}
        </v-btn>
      </v-alert>
      <div class="d-flex flex-wrap ga-2">
        <v-btn
          v-for="b in parentRowBindings"
          :key="b.id"
          size="small"
          variant="tonal"
          color="secondary"
          :loading="docGeneratingId === b.id"
          :disabled="!!docGeneratingId"
          @click="generateParentRowDocument(b)"
        >
          <FileTextIcon size="14" class="mr-1" />
          {{ b.label }}
        </v-btn>
      </div>
    </div>

    <v-tabs
      v-if="showTabStrip"
      v-model="activeTab"
      density="compact"
      color="primary"
      class="mb-3 reporting-expand-tabs"
      show-arrows
    >
      <v-tab value="fields">{{ fieldsTabTitle }}</v-tab>
      <v-tab v-for="tab in childTabs" :key="tab.id" :value="tab.id">
        {{ tab.title }}
      </v-tab>
    </v-tabs>

    <v-window v-if="showTabStrip" v-model="activeTab">
      <v-window-item value="fields">
        <ReportingDetailView
          :layout="layout"
          :fields="fields"
          :row="row"
          :list-config="listConfig"
          :hide-empty-fields="effectiveExpandConfig.hideEmptyFields"
          :can-view-field="canViewField"
        />
      </v-window-item>
      <v-window-item v-for="tab in childTabs" :key="tab.id" :value="tab.id">
        <ReportingChildListPanel
          :parent-row="row"
          :child-list="tab.childList"
          :field-policies="tabFieldPolicies(tab)"
          :active="activeTab === tab.id"
          :tab-id="tab.id"
          :report-id="reportId"
          :report-title="reportTitle"
          :parent-list-config="listConfig"
          :document-bindings="documentBindings"
          :enable-child-row-documents="enableParentRowDocuments"
        />
      </v-window-item>
    </v-window>

    <ReportingDetailView
      v-else
      :layout="layout"
      :fields="fields"
      :row="row"
      :list-config="listConfig"
      :hide-empty-fields="effectiveExpandConfig.hideEmptyFields"
      :can-view-field="canViewField"
    />
  </div>
</template>

<style scoped>
.reporting-expand-panel {
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.reporting-expand-tabs {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 4px;
}
</style>
