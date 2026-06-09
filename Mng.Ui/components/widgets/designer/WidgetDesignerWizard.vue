<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useWidgetStore, type CreateWidgetDto, type UpdateWidgetDto, type Widget } from '@/stores/apps/widget';
import { useAuthStore } from '@/stores/auth';
import { fetchFromMngKeeper } from '@/services/apiService';
import type { ParameterSchemaField, ParameterValue, WidgetTemplateRecord } from '@/types/apps/widgetManifest';
import WidgetTemplateCatalog from './WidgetTemplateCatalog.vue';
import WidgetFieldMappingPanel from './WidgetFieldMappingPanel.vue';
import WidgetHost from '@/components/widgets/WidgetHost.vue';
import {
  REFRESH_INTERVAL_OPTIONS,
  createDraftFromTemplate,
  createDraftFromWidget,
  draftToCreateWidgetDto,
  draftToUpdateWidgetDto,
  resolveCategoryIdFromTemplate,
  type WidgetDesignerDraft,
} from '@/utils/widgets/widgetDesignerHelpers';
import { PRESENTATION_PRESETS } from '@/utils/widgets/presentationPresets';
import {
  durationPresetToHours,
  manifestToLegacyWidget,
  pickLocalized,
  resolveWidgetDefinitionManifest,
} from '@/utils/widgets/widgetManifestAdapter';

const props = defineProps<{
  initialTemplateId?: string | null;
  /** Edit modu — mevcut @widgets kaydı */
  initialWidget?: Widget | null;
  mode?: 'create' | 'edit';
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  submit: [dto: CreateWidgetDto | UpdateWidgetDto];
  cancel: [];
}>();

const isEditMode = computed(() => props.mode === 'edit' || Boolean(props.initialWidget));

const widgetStore = useWidgetStore();
const authStore = useAuthStore();

const step = ref(1);
const draft = ref<WidgetDesignerDraft | null>(null);
const draftLoading = ref(false);
const draftLoadError = ref<string | null>(null);
const advancedPanel = ref<number | undefined>(undefined);
const saving = ref(false);
const validationError = ref<string | null>(null);

const groups = ref<Array<{ id: string; name: string; isActive: boolean }>>([]);
const loadingGroups = ref(false);

const lbl = (key: string) => props.t?.(`widgets.designer.${key}`) ?? key;
const plbl = (key: string) => props.t?.(`widgets.designer.parameters.${key}`) ?? key;

const filteredGroups = computed(() => {
  const active = groups.value.filter((g) => g.isActive !== false);
  if (authStore.isAdmin) return active;
  return active.filter((g) => g.name.toLowerCase() !== 'admins');
});

const groupOptions = computed(() =>
  filteredGroups.value.map((g) => ({ title: g.name, value: g.name }))
);

const allowedPresets = computed(() => {
  if (!draft.value) return [];
  const ids =
    draft.value.template.presentation.allowedPresets ??
    [draft.value.template.presentation.defaultPreset ?? 'stat-simple'];
  return ids
    .map((id) => PRESENTATION_PRESETS[id])
    .filter(Boolean)
    .map((p) => ({ id: p!.id, kind: p!.kind, icon: p!.defaultConfig.icon ?? 'mdi-palette' }));
});

const visibleSchemaFields = computed(() => {
  if (!draft.value) return [];
  return (draft.value.template.parametersSchema ?? []).filter((f) => {
    if (f.hidden) return false;
    if (f.advanced && advancedPanel.value == null) return false;
    return true;
  });
});

const previewWidget = computed(() => {
  if (!draft.value) return null;
  const categoryId = resolveCategoryIdFromTemplate(draft.value.template, widgetStore.categories);
  const definition = {
    ...draft.value.template,
    name: draft.value.name,
    title: { tr: draft.value.title, en: draft.value.title },
    templateId: draft.value.template.templateId,
    templateVersion: draft.value.template.templateVersion,
    presentation: {
      ...draft.value.template.presentation,
      preset: draft.value.preset,
    },
    parameters: draft.value.parameters,
    isActive: true,
    category: categoryId ?? draft.value.template.category,
  };
  return manifestToLegacyWidget(definition as any, {
    name: draft.value.name,
    categoryId: categoryId ?? undefined,
    parameters: draft.value.parameters,
    presentationConfigOverrides: draft.value.presentationConfigOverrides,
  });
});

const sampleFieldKeys = computed(() => {
  const fm = draft.value?.template.dataBinding.fieldMap ?? {};
  const keys = new Set<string>(['value', 'count', 'severity', 'bucket', 'timestamp', 'total']);
  Object.values(fm).forEach((v) => {
    if (typeof v === 'string') keys.add(v);
  });
  if (fm.series) keys.add(fm.series);
  if (fm.x) keys.add(fm.x);
  if (fm.y) keys.add(fm.y);
  return [...keys];
});

async function loadGroups() {
  loadingGroups.value = true;
  try {
    const response = await fetchFromMngKeeper('/group?page=1&pageSize=1000', 'GET');
    let loaded: any[] = [];
    if (Array.isArray(response)) loaded = response;
    else if (response?.groups) loaded = response.groups;
    else if (response?.data) loaded = response.data;
    else if (response?.Groups) loaded = response.Groups;
    groups.value = loaded.filter((g: any) => (g.isActive ?? g.IsActive ?? true) !== false);
  } catch {
    groups.value = [];
  } finally {
    loadingGroups.value = false;
  }
}

async function loadDraftFromWidget(widget: Widget) {
  draftLoading.value = true;
  draftLoadError.value = null;
  try {
    const templateId =
      widget.templateId ??
      (widget.config as Record<string, unknown> | undefined)?.templateId ??
      resolveWidgetDefinitionManifest(widget)?.templateId;
    let record = templateId ? widgetStore.getTemplateById(String(templateId)) : undefined;
    if (templateId && !record) {
      await widgetStore.fetchWidgetTemplates({ activeOnly: true, limit: 200 });
      record = widgetStore.getTemplateById(String(templateId));
    }
    draft.value = createDraftFromWidget(widget, record ?? null);
    step.value = 2;
  } catch (e: unknown) {
    draftLoadError.value = e instanceof Error ? e.message : lbl('editLoadFailed');
    draft.value = null;
  } finally {
    draftLoading.value = false;
  }
}

onMounted(async () => {
  await widgetStore.fetchWidgetCategories();
  await loadGroups();
  if (props.initialWidget) {
    await loadDraftFromWidget(props.initialWidget);
    return;
  }
  if (props.initialTemplateId) {
    await widgetStore.fetchWidgetTemplates({ activeOnly: true, limit: 100 });
    const record = widgetStore.getTemplateById(props.initialTemplateId);
    if (record) onTemplateSelected(record);
  }
});

watch(
  () => props.initialWidget,
  async (widget) => {
    if (widget) await loadDraftFromWidget(widget);
  },
);

function onTemplateSelected(record: WidgetTemplateRecord) {
  if (isEditMode.value) return;
  draft.value = createDraftFromTemplate(record);
  validationError.value = null;
}

function isContextBound(field: ParameterSchemaField): boolean {
  return Boolean(field.bindToContext);
}

function fieldLabel(field: ParameterSchemaField): string {
  return pickLocalized(field.label) || field.name;
}

function contextHint(field: ParameterSchemaField): string {
  if (!field.bindToContext) return '';
  return plbl('fromContext').replace('{path}', field.bindToContext ?? '');
}

function displayParameterValue(field: ParameterSchemaField): string {
  const value = draft.value?.parameters[field.name];
  if (value != null && typeof value === 'object' && '$ref' in value) {
    return String(value.$ref);
  }
  return value == null ? '' : String(value);
}

function setParameterValue(field: ParameterSchemaField, raw: unknown) {
  if (!draft.value || isContextBound(field)) return;
  draft.value.parameters[field.name] = raw as ParameterValue;
}

function durationOptions(field: ParameterSchemaField) {
  return (field.durationPresets ?? ['1h', '6h', '24h', '7d']).map((preset) => ({
    title: preset,
    value: durationPresetToHours(preset),
  }));
}

function enumOptions(field: ParameterSchemaField) {
  return (field.enum ?? []).map((item) => ({
    title: pickLocalized(item.label) || String(item.value),
    value: item.value,
  }));
}

function canProceedFromStep(current: number): boolean {
  if (current === 1) return Boolean(draft.value);
  if (current === 2) {
    if (!draft.value) return false;
    for (const field of draft.value.template.parametersSchema ?? []) {
      if (field.hidden || field.advanced) continue;
      if (field.required && !isContextBound(field)) {
        const val = draft.value.parameters[field.name];
        if (val == null || val === '') return false;
      }
    }
    return true;
  }
  if (current === 3) {
    return Boolean(draft.value?.name.trim() && draft.value?.title.trim());
  }
  return true;
}

function nextStep() {
  validationError.value = null;
  if (!canProceedFromStep(step.value)) {
    validationError.value = lbl('validation.stepIncomplete');
    return;
  }
  if (step.value < 4) step.value += 1;
}

function prevStep() {
  validationError.value = null;
  if (step.value > 1) step.value -= 1;
}

async function save() {
  if (!draft.value) return;
  validationError.value = null;

  if (!draft.value.name.trim() || !draft.value.title.trim()) {
    validationError.value = lbl('validation.nameTitleRequired');
    return;
  }

  let categoryId = resolveCategoryIdFromTemplate(draft.value.template, widgetStore.categories);
  if (!categoryId && props.initialWidget) {
    const cat = props.initialWidget.category;
    categoryId = typeof cat === 'string' ? cat : cat?.__dataId ?? cat?.dataId;
  }
  if (!categoryId) {
    validationError.value = lbl('validation.categoryMissing').replace(
      '{slug}',
      draft.value.template.category
    );
    return;
  }

  saving.value = true;
  try {
    const dto = isEditMode.value
      ? draftToUpdateWidgetDto(draft.value, categoryId)
      : draftToCreateWidgetDto(draft.value, categoryId);
    emit('submit', dto);
  } finally {
    saving.value = false;
  }
}

const drillDownPath = computed(() => {
  const drill = draft.value?.template.interactions?.drillDown as { path?: string } | undefined;
  return drill?.path ?? '';
});
</script>

<template>
  <v-card variant="outlined">
    <v-stepper v-model="step" alt-labels flat>
      <v-stepper-header>
        <v-stepper-item :value="1" :complete="step > 1" editable :title="lbl('steps.catalog')" />
        <v-divider />
        <v-stepper-item
          :value="2"
          :complete="step > 2"
          editable
          :title="lbl('steps.parameters')"
          :disabled="!draft"
        />
        <v-divider />
        <v-stepper-item
          :value="3"
          :complete="step > 3"
          editable
          :title="lbl('steps.appearance')"
          :disabled="!draft"
        />
        <v-divider />
        <v-stepper-item :value="4" editable :title="lbl('steps.behavior')" :disabled="!draft" />
      </v-stepper-header>

      <v-stepper-window>
        <v-stepper-window-item :value="1">
          <v-card-text v-if="draftLoading" class="text-center py-8">
            <v-progress-circular indeterminate color="primary" />
          </v-card-text>
          <v-card-text v-else-if="draftLoadError">
            <v-alert type="error" variant="tonal">{{ draftLoadError }}</v-alert>
          </v-card-text>
          <v-card-text v-else-if="isEditMode && draft">
            <v-alert type="info" variant="tonal" class="mb-4">
              {{ lbl('editTemplateLocked') }}
            </v-alert>
            <v-chip color="primary" variant="tonal" class="mr-2">{{ draft.template.templateId }}</v-chip>
            <v-chip variant="outlined">v{{ draft.template.templateVersion }}</v-chip>
            <p class="text-body-2 text-medium-emphasis mt-4 mb-0">
              {{ pickLocalized(draft.template.title) }}
            </p>
          </v-card-text>
          <v-card-text v-else>
            <p class="text-body-2 text-medium-emphasis mb-4">{{ lbl('catalogHint') }}</p>
            <WidgetTemplateCatalog
              :selected-template-id="draft?.template.templateId"
              :t="t"
              @select="onTemplateSelected"
            />
          </v-card-text>
        </v-stepper-window-item>

        <v-stepper-window-item :value="2">
          <v-card-text v-if="draft">
            <p class="text-body-2 text-medium-emphasis mb-4">{{ lbl('parametersHint') }}</p>

            <div v-if="visibleSchemaFields.length">
              <div v-for="field in visibleSchemaFields" :key="field.name" class="mb-4">
                <v-select
                  v-if="field.type === 'enum' && !isContextBound(field)"
                  :model-value="draft.parameters[field.name]"
                  :items="enumOptions(field)"
                  item-title="title"
                  item-value="value"
                  :label="fieldLabel(field)"
                  variant="outlined"
                  density="compact"
                  @update:model-value="setParameterValue(field, $event)"
                />
                <v-select
                  v-else-if="field.type === 'duration' && !isContextBound(field)"
                  :model-value="draft.parameters[field.name]"
                  :items="durationOptions(field)"
                  :label="fieldLabel(field)"
                  variant="outlined"
                  density="compact"
                  @update:model-value="setParameterValue(field, $event)"
                />
                <v-text-field
                  v-else-if="field.type === 'string' && !isContextBound(field)"
                  :model-value="displayParameterValue(field)"
                  :label="fieldLabel(field)"
                  variant="outlined"
                  density="compact"
                  @update:model-value="setParameterValue(field, $event)"
                />
                <v-text-field
                  v-else-if="(field.type === 'number' || field.type === 'integer') && !isContextBound(field)"
                  :model-value="draft.parameters[field.name]"
                  type="number"
                  :label="fieldLabel(field)"
                  variant="outlined"
                  density="compact"
                  @update:model-value="setParameterValue(field, Number($event))"
                />
                <v-text-field
                  v-else
                  :model-value="displayParameterValue(field)"
                  :label="fieldLabel(field)"
                  :hint="contextHint(field)"
                  persistent-hint
                  variant="outlined"
                  density="compact"
                  disabled
                />
              </div>
            </div>
            <v-alert v-else type="info" variant="tonal" density="compact">
              {{ lbl('parametersEmpty') }}
            </v-alert>

            <v-expansion-panels v-model="advancedPanel" class="mt-4">
              <v-expansion-panel>
                <v-expansion-panel-title>{{ lbl('advancedMode') }}</v-expansion-panel-title>
                <v-expansion-panel-text>
                  <pre class="text-caption pa-3 bg-grey-lighten-4 rounded">{{
                    JSON.stringify(draft.parameters, null, 2)
                  }}</pre>
                </v-expansion-panel-text>
              </v-expansion-panel>
            </v-expansion-panels>
          </v-card-text>
        </v-stepper-window-item>

        <v-stepper-window-item :value="3">
          <v-card-text v-if="draft">
            <v-row>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="draft.name"
                  :label="lbl('fields.name')"
                  variant="outlined"
                  density="compact"
                  class="mb-3"
                  :disabled="isEditMode"
                  :hint="isEditMode ? lbl('editNameLocked') : undefined"
                  :persistent-hint="isEditMode"
                />
                <v-text-field
                  v-model="draft.title"
                  :label="lbl('fields.title')"
                  variant="outlined"
                  density="compact"
                  class="mb-3"
                />
                <v-textarea
                  v-model="draft.description"
                  :label="lbl('fields.description')"
                  variant="outlined"
                  density="compact"
                  rows="2"
                  class="mb-4"
                />

                <div class="text-subtitle-2 mb-2">{{ lbl('presetGallery') }}</div>
                <div class="preset-grid mb-4">
                  <v-card
                    v-for="preset in allowedPresets"
                    :key="preset.id"
                    variant="outlined"
                    :class="['preset-card', { 'preset-card--active': draft.preset === preset.id }]"
                    @click="draft.preset = preset.id"
                  >
                    <v-card-text class="pa-3 text-center">
                      <v-icon :icon="String(preset.icon)" size="28" color="primary" class="mb-1" />
                      <div class="text-caption">{{ preset.id }}</div>
                    </v-card-text>
                  </v-card>
                </div>

                <WidgetFieldMappingPanel
                  v-model="draft.presentationConfigOverrides"
                  :kind="draft.template.presentation.kind"
                  :sample-row-keys="sampleFieldKeys"
                  :t="t"
                />
              </v-col>
              <v-col cols="12" md="6">
                <div class="text-subtitle-2 mb-2">{{ lbl('livePreview') }}</div>
                <v-sheet border rounded class="pa-3 preview-shell">
                  <WidgetHost v-if="previewWidget" :widget="previewWidget" :t="t" />
                </v-sheet>
              </v-col>
            </v-row>
          </v-card-text>
        </v-stepper-window-item>

        <v-stepper-window-item :value="4">
          <v-card-text v-if="draft">
            <v-switch
              v-model="draft.isActive"
              :label="lbl('fields.isActive')"
              color="primary"
              class="mb-4"
              hide-details
            />

            <v-text-field
              v-model.number="draft.order"
              type="number"
              :label="lbl('fields.order')"
              variant="outlined"
              density="compact"
              class="mb-4"
              hide-details
            />

            <v-select
              v-model="draft.refreshIntervalSeconds"
              :items="REFRESH_INTERVAL_OPTIONS"
              :item-title="(item) => lbl(item.labelKey)"
              item-value="value"
              :label="lbl('refreshInterval')"
              variant="outlined"
              density="compact"
              class="mb-4"
            />

            <v-select
              v-model="draft.permissionGroups"
              :items="groupOptions"
              :label="lbl('permissionGroups')"
              :hint="lbl('permissionGroupsHint')"
              persistent-hint
              variant="outlined"
              density="compact"
              multiple
              chips
              closable-chips
              :loading="loadingGroups"
              class="mb-4"
            />

            <v-alert
              v-if="drillDownPath"
              type="info"
              variant="tonal"
              density="compact"
            >
              {{ lbl('drillDownHint') }}: {{ drillDownPath }}
            </v-alert>
          </v-card-text>
        </v-stepper-window-item>
      </v-stepper-window>
    </v-stepper>

    <v-divider />

    <v-card-actions class="pa-4 flex-wrap ga-2">
      <v-btn variant="text" @click="emit('cancel')">{{ lbl('cancel') }}</v-btn>
      <v-spacer />
      <v-alert
        v-if="validationError"
        type="error"
        variant="tonal"
        density="compact"
        class="flex-grow-1 mr-2"
      >
        {{ validationError }}
      </v-alert>
      <v-btn v-if="step > 1" variant="outlined" @click="prevStep">{{ lbl('back') }}</v-btn>
      <v-btn v-if="step < 4" color="primary" variant="flat" @click="nextStep">{{ lbl('next') }}</v-btn>
      <v-btn
        v-else
        color="primary"
        variant="flat"
        :loading="saving || widgetStore.loading"
        :disabled="!draft"
        @click="save"
      >
        {{ isEditMode ? lbl('saveChanges') : lbl('save') }}
      </v-btn>
    </v-card-actions>
  </v-card>
</template>

<style scoped>
.preset-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 8px;
}

.preset-card {
  cursor: pointer;
}

.preset-card--active {
  border-color: rgb(var(--v-theme-primary));
  background-color: rgba(var(--v-theme-primary), 0.08);
}

.preview-shell {
  min-height: 180px;
}
</style>
