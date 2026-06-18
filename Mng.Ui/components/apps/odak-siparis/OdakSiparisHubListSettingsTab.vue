<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { VueDraggableNext } from 'vue-draggable-next';
import OdakSiparisListColumnFormatDialog from '@/components/apps/odak-siparis/OdakSiparisListColumnFormatDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { isActiveListColumnFormat, type AfListColumnFormat } from '@/utils/afListColumnFormat';
import type { OdakHubListColumnConfig, OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import { odakPackageSettingsFormatTypeLabelTr } from '@/utils/odakSiparisSettingsLabels';
import { invalidateOdakPackageHubSettingsCache } from '@/utils/odakSiparisHubSettingsService';

const props = defineProps({
  scope: { type: String, required: true },
  hintKey: { type: String, required: true },
  fieldLabel: { type: Function, required: true },
  defaultConfig: { type: Function, required: true },
  mergeConfig: { type: Function, required: true },
  loadConfig: { type: Function, required: true },
  saveConfig: { type: Function, required: true },
});

const { t } = useAppI18n();

const loading = ref(true);
const saving = ref(false);
const errorMessage = ref('');
const successMessage = ref('');
const rowId = ref<string | null>(null);
const listConfig = ref<OdakHubListConfig>((props.defaultConfig as () => OdakHubListConfig)());
const formatDialogOpen = ref(false);
const formatColumn = ref<OdakHubListColumnConfig | null>(null);

const conditionFields = computed(() =>
  listConfig.value.columns.map((c) => ({
    value: c.fieldName,
    title: columnLabel(c.fieldName),
  }))
);

function columnLabel(fieldName: string): string {
  return (props.fieldLabel as (name: string) => string)(fieldName);
}

function formatSummary(element: OdakHubListColumnConfig): string {
  if (!isActiveListColumnFormat(element.format)) {
    return t('odakSiparis.packages.settings.listColumns.noFormat');
  }
  const typeKey = element.format?.type ?? 'none';
  const labelKey =
    typeKey === 'text-transform'
      ? 'textTransform'
      : typeKey === 'conditional-color'
        ? 'conditionalColor'
        : typeKey;
  return odakPackageSettingsFormatTypeLabelTr(labelKey);
}

function openFormatDialog(element: OdakHubListColumnConfig) {
  formatColumn.value = element;
  formatDialogOpen.value = true;
}

function onFormatSave(format: AfListColumnFormat | undefined) {
  if (!formatColumn.value) return;
  formatColumn.value.format = format;
  formatColumn.value = null;
}

function reorderColumns() {
  listConfig.value.columns.forEach((c, idx) => {
    c.order = idx + 1;
  });
}

async function load() {
  loading.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    const resp = await (props.loadConfig as () => Promise<{ config: OdakHubListConfig; rowId: string | null }>)();
    listConfig.value = (props.mergeConfig as (saved: unknown) => OdakHubListConfig)({ listConfig: resp.config });
    rowId.value = resp.rowId;
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    listConfig.value = (props.defaultConfig as () => OdakHubListConfig)();
  } finally {
    loading.value = false;
  }
}

async function save() {
  reorderColumns();
  saving.value = true;
  errorMessage.value = '';
  successMessage.value = '';
  try {
    rowId.value = await (props.saveConfig as (config: OdakHubListConfig, rowId: string | null) => Promise<string>)(
      listConfig.value,
      rowId.value
    );
    invalidateOdakPackageHubSettingsCache();
    successMessage.value = t('odakSiparis.packages.settings.saved');
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

function resetDefaults() {
  listConfig.value = (props.defaultConfig as () => OdakHubListConfig)();
}

onMounted(() => void load());
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t(hintKey) }}
    </v-alert>
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4">{{ errorMessage }}</v-alert>
    <v-alert v-if="successMessage" type="success" variant="tonal" density="compact" class="mb-4">{{ successMessage }}</v-alert>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <VueDraggableNext
      v-if="!loading && listConfig.columns.length"
      :list="listConfig.columns"
      item-key="fieldName"
      handle=".drag-handle"
      class="d-flex flex-column"
      @end="reorderColumns"
    >
      <v-card
        v-for="element in listConfig.columns"
        :key="element.fieldName"
        variant="outlined"
        class="mb-2 pa-3"
      >
        <div class="d-flex flex-wrap align-center ga-3">
          <v-icon class="drag-handle cursor-grab" icon="mdi-drag" />
          <div class="d-flex flex-column">
            <span class="font-weight-medium">{{ columnLabel(element.fieldName) }}</span>
            <span class="text-caption text-medium-emphasis">{{ formatSummary(element) }}</span>
          </div>
          <v-spacer />
          <v-btn
            variant="tonal"
            size="small"
            :color="isActiveListColumnFormat(element.format) ? 'primary' : undefined"
            @click="openFormatDialog(element)"
          >
            <v-icon start size="18">mdi-format-paint</v-icon>
            {{ t('odakSiparis.packages.settings.listColumns.editFormat') }}
          </v-btn>
          <v-switch v-model="element.visible" :label="t('odakSiparis.packages.settings.listColumns.visible')" hide-details density="compact" color="primary" />
          <v-switch v-model="element.sortable" :label="t('odakSiparis.packages.settings.listColumns.sortable')" hide-details density="compact" />
          <v-switch
            v-if="scope === 'packages_list'"
            v-model="element.filterable"
            :label="t('odakSiparis.packages.settings.listColumns.filterable')"
            hide-details
            density="compact"
          />
        </div>
      </v-card>
    </VueDraggableNext>

    <v-alert
      v-else-if="!loading && !listConfig.columns.length"
      type="warning"
      variant="tonal"
      density="compact"
      class="mb-4"
    >
      {{ t('odakSiparis.packages.settings.listColumns.empty') }}
    </v-alert>

    <OdakSiparisListColumnFormatDialog
      v-model="formatDialogOpen"
      :column="formatColumn"
      :column-label="formatColumn ? columnLabel(formatColumn.fieldName) : ''"
      :condition-fields="conditionFields"
      @save="onFormatSave"
    />

    <div class="d-flex ga-2 mt-4">
      <v-btn variant="tonal" @click="resetDefaults">{{ t('odakSiparis.packages.settings.listColumns.reset') }}</v-btn>
      <v-spacer />
      <v-btn color="primary" variant="flat" :loading="saving" @click="save">{{ t('odakSiparis.packages.settings.save') }}</v-btn>
    </div>
  </div>
</template>

<style scoped>
.cursor-grab {
  cursor: grab;
}
</style>
