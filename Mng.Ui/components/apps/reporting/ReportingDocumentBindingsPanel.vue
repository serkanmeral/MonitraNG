<script setup lang="ts">
/**
 * Designer — Belge şablonları paneli (DI template bindings).
 */
import { computed, onMounted, ref, watch } from 'vue';
import DiPickResourceDialog from '@/components/apps/document-intelligence/DiPickResourceDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { diGetById, diListTemplates } from '@/services/documentIntelligenceService';
import { ReportingCatalogService } from '@/services/reportingCatalogService';
import type { DiResource, DiTemplateSummary } from '@/types/apps/documentIntelligence';
import type {
  ReportingDocumentBinding,
  ReportingDocumentChildLink,
  ReportingDocumentContextType,
} from '@/types/apps/reporting';
import type { AfListFilter } from '@/utils/afListFilters';
import {
  findReportingTemplateBindingConflict,
  newReportingDocumentBindingId,
} from '@/utils/reportingDocumentBindings';
import { defaultDocumentNamePattern } from '@/utils/reportingDocumentTokens';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  bindings: ReportingDocumentBinding[];
  reportId: string | null;
  domainKey: string;
  disabled?: boolean;
}>();

const emit = defineEmits<{
  'update:bindings': [ReportingDocumentBinding[]];
}>();

const { t } = useAppI18n();

const templatesLoading = ref(false);
const templatesError = ref('');
const allTemplates = ref<DiTemplateSummary[]>([]);
const addDialog = ref(false);
const selectedTemplateId = ref<string | null>(null);
const selectedContextType = ref<ReportingDocumentContextType>('reportRun');
const addError = ref('');
const expandedIds = ref<string[]>([]);
const folderNames = ref<Record<string, string>>({});
const folderPickerOpen = ref(false);
const folderPickerBindingId = ref<string | null>(null);

const contextTypeItems = computed(() =>
  (['reportRun', 'parentRow', 'childRow'] as const).map((value) => ({
    value,
    title: t(`reporting.documentBindings.contextTypes.${value}`),
  }))
);

const boundTemplateIds = computed(
  () => new Set(props.bindings.map((b) => b.templateId).filter(Boolean))
);

const availableTemplates = computed(() => {
  const catalog = new ReportingCatalogService(props.domainKey);
  const reports = catalog.listReports();
  return allTemplates.value.filter((tpl) => {
    if (boundTemplateIds.value.has(tpl.id) || boundTemplateIds.value.has(tpl.code)) {
      return false;
    }
    const conflict = findReportingTemplateBindingConflict(
      reports,
      tpl.id,
      props.reportId,
      tpl.code
    );
    return !conflict;
  });
});

const templateSelectItems = computed(() =>
  availableTemplates.value.map((tpl) => ({
    value: tpl.id,
    title: `${tpl.name} (${tpl.code})`,
    subtitle: tpl.outputFormat || '',
  }))
);

async function loadTemplates() {
  templatesLoading.value = true;
  templatesError.value = '';
  try {
    const res = await diListTemplates();
    allTemplates.value = res.items.filter((t) => t.status === 'published' || !t.status);
  } catch (e: unknown) {
    templatesError.value =
      e instanceof Error ? e.message : t('reporting.documentBindings.loadTemplatesFailed');
    allTemplates.value = [];
  } finally {
    templatesLoading.value = false;
  }
}

function openAdd() {
  addError.value = '';
  selectedTemplateId.value = null;
  selectedContextType.value = 'reportRun';
  addDialog.value = true;
  void loadTemplates();
}

function confirmAdd() {
  addError.value = '';
  const tpl = allTemplates.value.find((t) => t.id === selectedTemplateId.value);
  if (!tpl) {
    addError.value = t('reporting.documentBindings.pickTemplate');
    return;
  }
  const catalog = new ReportingCatalogService(props.domainKey);
  const conflict = findReportingTemplateBindingConflict(
    catalog.listReports(),
    tpl.id,
    props.reportId,
    tpl.code
  );
  if (conflict) {
    addError.value = t('reporting.documentBindings.exclusiveConflict', {
      report: conflict.reportTitle,
    });
    return;
  }
  const next: ReportingDocumentBinding = {
    id: newReportingDocumentBindingId(),
    templateId: tpl.id,
    templateCode: tpl.code,
    label: tpl.name,
    contextType: selectedContextType.value,
    documentNamePattern: defaultDocumentNamePattern(selectedContextType.value),
    generatedAtPattern: 'yyyy-MM-dd HH:mm',
  };
  emit('update:bindings', [...props.bindings, next]);
  addDialog.value = false;
}

function removeBinding(id: string) {
  emit(
    'update:bindings',
    props.bindings.filter((b) => b.id !== id)
  );
  expandedIds.value = expandedIds.value.filter((x) => x !== id);
}

function patchBinding(id: string, patch: Partial<ReportingDocumentBinding>) {
  emit(
    'update:bindings',
    props.bindings.map((b) => (b.id === id ? { ...b, ...patch } : b))
  );
}

const rowOperatorItems = computed(() => [
  { value: 'eq', title: t('reporting.documentBindings.rowOperatorEq') },
  { value: 'neq', title: t('reporting.documentBindings.rowOperatorNeq') },
]);

function rowFilterOf(binding: ReportingDocumentBinding): AfListFilter {
  return binding.rowFilters?.[0] ?? { field: '', operator: 'eq', value: '' };
}

function patchChildLink(id: string, patch: Partial<ReportingDocumentChildLink>) {
  const current = props.bindings.find((binding) => binding.id === id);
  if (!current) return;
  const next: ReportingDocumentChildLink = {
    childTabId: current.linkToChild?.childTabId ?? '',
    matchField: current.linkToChild?.matchField ?? '',
    matchValue: current.linkToChild?.matchValue ?? '',
    relationType: current.linkToChild?.relationType,
    refField: current.linkToChild?.refField,
    parentRefField: current.linkToChild?.parentRefField,
    ...patch,
  };
  const childTabId = next.childTabId.trim();
  const matchField = next.matchField.trim();
  const matchValue = next.matchValue.trim();
  const relationType = next.relationType?.trim() || undefined;
  const refField = next.refField?.trim() || undefined;
  const parentRefField = next.parentRefField?.trim() || undefined;
  const hasAny = childTabId || matchField || matchValue || relationType || refField || parentRefField;
  patchBinding(id, {
    linkToChild: hasAny
      ? { childTabId, matchField, matchValue, relationType, refField, parentRefField }
      : undefined,
  });
}

function patchRowFilter(id: string, patch: Partial<AfListFilter>) {
  const current = props.bindings.find((binding) => binding.id === id);
  if (!current) return;
  const base = rowFilterOf(current);
  const field = String(patch.field ?? base.field).trim();
  const operator = String(patch.operator ?? base.operator).trim() || 'eq';
  const value = String(patch.value ?? base.value).trim();
  patchBinding(id, {
    rowFilters: field ? [{ field, operator, value }] : undefined,
  });
}

function namePatternPlaceholder(b: ReportingDocumentBinding): string {
  return defaultDocumentNamePattern(b.contextType);
}

function folderLabel(binding: ReportingDocumentBinding): string {
  const id = binding.outputFolderId?.trim();
  if (!id) return t('reporting.documentBindings.outputFolderDefault');
  return folderNames.value[id] || id;
}

async function rememberFolderName(folderId: string) {
  const id = folderId.trim();
  if (!id || folderNames.value[id]) return;
  try {
    const folder = await diGetById(id);
    const name = folder.name?.trim();
    if (name) folderNames.value = { ...folderNames.value, [id]: name };
  } catch {
    folderNames.value = { ...folderNames.value, [id]: id };
  }
}

function openFolderPicker(bindingId: string) {
  folderPickerBindingId.value = bindingId;
  folderPickerOpen.value = true;
}

function clearOutputFolder(bindingId: string) {
  patchBinding(bindingId, { outputFolderId: undefined });
}

function onOutputFolderPicked(resource: DiResource) {
  const bindingId = folderPickerBindingId.value;
  if (!bindingId || !resource.id) return;
  patchBinding(bindingId, { outputFolderId: resource.id });
  const name = resource.name?.trim();
  if (name) folderNames.value = { ...folderNames.value, [resource.id]: name };
  folderPickerOpen.value = false;
}

watch(
  () => props.bindings.map((binding) => binding.outputFolderId || '').join('|'),
  () => {
    for (const binding of props.bindings) {
      const id = binding.outputFolderId?.trim();
      if (id) void rememberFolderName(id);
    }
  },
  { immediate: true }
);

onMounted(() => {
  void loadTemplates();
});
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
      <div>
        <div class="text-subtitle-2">{{ t('reporting.documentBindings.title') }}</div>
        <p class="text-caption text-medium-emphasis mb-0">
          {{ t('reporting.documentBindings.hint') }}
        </p>
      </div>
      <v-btn
        size="small"
        color="primary"
        variant="tonal"
        :disabled="disabled"
        @click="openAdd"
      >
        <PlusIcon size="16" class="mr-1" />
        {{ t('reporting.documentBindings.add') }}
      </v-btn>
    </div>

    <v-alert v-if="templatesError" type="warning" variant="tonal" density="compact" class="mb-3">
      {{ templatesError }}
    </v-alert>

    <v-alert
      v-if="!bindings.length"
      type="info"
      variant="tonal"
      density="compact"
      class="mb-3"
    >
      {{ t('reporting.documentBindings.empty') }}
    </v-alert>

    <v-expansion-panels v-else v-model="expandedIds" multiple class="border rounded">
      <v-expansion-panel v-for="b in bindings" :key="b.id" :value="b.id">
        <v-expansion-panel-title>
          <div class="d-flex flex-column align-start flex-grow-1 pr-2">
            <span class="text-body-2">{{ b.label }}</span>
            <span class="text-caption text-medium-emphasis">
              {{ b.templateCode || b.templateId }}
              ·
              {{ t(`reporting.documentBindings.contextTypes.${b.contextType}`) }}
            </span>
          </div>
          <v-btn
            icon
            variant="text"
            size="small"
            color="error"
            :disabled="disabled"
            :aria-label="t('reporting.documentBindings.remove')"
            @click.stop="removeBinding(b.id)"
          >
            <TrashIcon size="16" />
          </v-btn>
        </v-expansion-panel-title>
        <v-expansion-panel-text>
          <div class="d-flex align-start ga-2 mb-3">
            <v-text-field
              :model-value="folderLabel(b)"
              :label="t('reporting.documentBindings.outputFolder')"
              :hint="t('reporting.documentBindings.outputFolderHint')"
              persistent-hint
              readonly
              density="compact"
              variant="outlined"
              class="flex-grow-1"
              :disabled="disabled"
            />
            <v-btn
              size="small"
              variant="tonal"
              class="mt-1"
              :disabled="disabled"
              @click="openFolderPicker(b.id)"
            >
              {{ t('reporting.documentBindings.pickFolder') }}
            </v-btn>
            <v-btn
              v-if="b.outputFolderId"
              size="small"
              variant="text"
              class="mt-1"
              :disabled="disabled"
              @click="clearOutputFolder(b.id)"
            >
              {{ t('reporting.documentBindings.clearFolder') }}
            </v-btn>
          </div>
          <div v-if="b.contextType !== 'reportRun'" class="mb-3">
            <div class="d-flex align-start ga-2">
              <v-text-field
                :model-value="rowFilterOf(b).field"
                :label="t('reporting.documentBindings.rowField')"
                :hint="t('reporting.documentBindings.rowFilterHint')"
                persistent-hint
                density="compact"
                variant="outlined"
                class="flex-grow-1"
                :disabled="disabled"
                @update:model-value="patchRowFilter(b.id, { field: String($event ?? '') })"
              />
              <v-select
                :model-value="rowFilterOf(b).operator || 'eq'"
                :items="rowOperatorItems"
                item-title="title"
                item-value="value"
                :label="t('reporting.documentBindings.rowOperator')"
                density="compact"
                variant="outlined"
                hide-details
                class="row-operator"
                :disabled="disabled"
                @update:model-value="patchRowFilter(b.id, { operator: String($event ?? 'eq') })"
              />
              <v-text-field
                :model-value="rowFilterOf(b).value"
                :label="t('reporting.documentBindings.rowValue')"
                density="compact"
                variant="outlined"
                hide-details
                class="flex-grow-1"
                :disabled="disabled"
                @update:model-value="patchRowFilter(b.id, { value: String($event ?? '') })"
              />
            </div>
          </div>
          <div v-if="b.contextType === 'parentRow'" class="mb-3">
            <div class="text-caption text-medium-emphasis mb-2">
              {{ t('reporting.documentBindings.linkToChildHint') }}
            </div>
            <div class="d-flex flex-wrap ga-2">
              <v-text-field
                :model-value="b.linkToChild?.childTabId ?? ''"
                :label="t('reporting.documentBindings.linkChildTab')"
                density="compact"
                variant="outlined"
                hide-details
                class="link-field"
                :disabled="disabled"
                @update:model-value="patchChildLink(b.id, { childTabId: String($event ?? '') })"
              />
              <v-text-field
                :model-value="b.linkToChild?.matchField ?? ''"
                :label="t('reporting.documentBindings.linkMatchField')"
                density="compact"
                variant="outlined"
                hide-details
                class="link-field"
                :disabled="disabled"
                @update:model-value="patchChildLink(b.id, { matchField: String($event ?? '') })"
              />
              <v-text-field
                :model-value="b.linkToChild?.matchValue ?? ''"
                :label="t('reporting.documentBindings.linkMatchValue')"
                density="compact"
                variant="outlined"
                hide-details
                class="link-field"
                :disabled="disabled"
                @update:model-value="patchChildLink(b.id, { matchValue: String($event ?? '') })"
              />
              <v-text-field
                :model-value="b.linkToChild?.refField ?? ''"
                :label="t('reporting.documentBindings.linkRefField')"
                density="compact"
                variant="outlined"
                hide-details
                class="link-field"
                :disabled="disabled"
                @update:model-value="patchChildLink(b.id, { refField: String($event ?? '') })"
              />
              <v-text-field
                :model-value="b.linkToChild?.parentRefField ?? ''"
                :label="t('reporting.documentBindings.linkParentRefField')"
                density="compact"
                variant="outlined"
                hide-details
                class="link-field"
                :disabled="disabled"
                @update:model-value="patchChildLink(b.id, { parentRefField: String($event ?? '') })"
              />
              <v-text-field
                :model-value="b.linkToChild?.relationType ?? ''"
                :label="t('reporting.documentBindings.linkRelation')"
                density="compact"
                variant="outlined"
                hide-details
                class="link-field"
                :disabled="disabled"
                @update:model-value="patchChildLink(b.id, { relationType: String($event ?? '') })"
              />
            </div>
          </div>
          <v-text-field
            :model-value="b.documentNamePattern ?? ''"
            :label="t('reporting.documentBindings.documentNamePattern')"
            :placeholder="namePatternPlaceholder(b)"
            :hint="t('reporting.documentBindings.documentNamePatternHint')"
            persistent-hint
            density="compact"
            variant="outlined"
            :disabled="disabled"
            class="mb-3"
            @update:model-value="
              patchBinding(b.id, {
                documentNamePattern: String($event ?? '').trim() || undefined,
              })
            "
          />
          <v-text-field
            :model-value="b.generatedAtPattern ?? ''"
            :label="t('reporting.documentBindings.generatedAtPattern')"
            placeholder="yyyy-MM-dd HH:mm"
            :hint="t('reporting.documentBindings.generatedAtPatternHint')"
            persistent-hint
            density="compact"
            variant="outlined"
            :disabled="disabled"
            @update:model-value="
              patchBinding(b.id, {
                generatedAtPattern: String($event ?? '').trim() || undefined,
              })
            "
          />
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>

    <DiPickResourceDialog
      v-model="folderPickerOpen"
      pick-folder
      :title="t('reporting.documentBindings.pickFolderTitle')"
      :hint="t('reporting.documentBindings.pickFolderHint')"
      :confirm-label="t('reporting.documentBindings.pickFolderConfirm')"
      @pick="onOutputFolderPicked"
    />

    <v-dialog v-model="addDialog" max-width="520" persistent>
      <v-card>
        <v-card-title>{{ t('reporting.documentBindings.addTitle') }}</v-card-title>
        <v-card-text>
          <v-alert v-if="addError" type="error" variant="tonal" density="compact" class="mb-3">
            {{ addError }}
          </v-alert>
          <v-autocomplete
            v-model="selectedTemplateId"
            :items="templateSelectItems"
            item-title="title"
            item-value="value"
            :label="t('reporting.documentBindings.template')"
            :loading="templatesLoading"
            :disabled="templatesLoading"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-select
            v-model="selectedContextType"
            :items="contextTypeItems"
            item-title="title"
            item-value="value"
            :label="t('reporting.documentBindings.contextType')"
            density="compact"
            variant="outlined"
            hide-details
          />
          <p class="text-caption text-medium-emphasis mt-3 mb-0">
            {{ t('reporting.documentBindings.exclusiveHint') }}
          </p>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="addDialog = false">
            {{ t('reporting.actions.cancel') }}
          </v-btn>
          <v-btn color="primary" :disabled="!selectedTemplateId" @click="confirmAdd">
            {{ t('reporting.documentBindings.bind') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.row-operator {
  max-width: 150px;
  flex: 0 0 150px;
}

.link-field {
  flex: 1 1 180px;
  min-width: 160px;
}
</style>
