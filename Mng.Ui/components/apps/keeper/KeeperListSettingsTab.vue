<script setup lang="ts">
import { computed, ref } from 'vue';
import { VueDraggableNext } from 'vue-draggable-next';
import OdakSiparisListColumnFormatDialog from '@/components/apps/odak-siparis/OdakSiparisListColumnFormatDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  useKeeperListSettingsStore,
} from '@/stores/apps/keeperListSettings';
import { isActiveListColumnFormat, type AfListColumnFormat } from '@/utils/afListColumnFormat';
import type { OdakHubListColumnConfig } from '@/utils/odakSiparisHubListConfig';
import type { KeeperListSettingsScope } from '@/utils/keeperListSettingsStorage';
import { odakPackageSettingsFormatTypeLabelTr } from '@/utils/odakSiparisSettingsLabels';

const props = defineProps<{
  scope: KeeperListSettingsScope;
  hintKey: string;
  fieldLabel: (fieldName: string) => string;
}>();

const { t } = useAppI18n();
const keeperStore = useKeeperListSettingsStore();

const errorMessage = ref('');
const successMessage = ref('');

const listConfig = computed(() => keeperStore.scopes[props.scope].config);
const canSave = computed(() => keeperStore.canSaveScope(props.scope));
const saving = computed(() => keeperStore.scopeSaving(props.scope));

const formatDialogOpen = ref(false);
const formatColumn = ref<OdakHubListColumnConfig | null>(null);

const conditionFields = computed(() =>
  listConfig.value.columns.map((c) => ({
    value: c.fieldName,
    title: props.fieldLabel(c.fieldName),
  }))
);

function formatSummary(element: OdakHubListColumnConfig): string {
  if (!isActiveListColumnFormat(element.format)) {
    return t('keeper.listSettings.noFormat');
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

async function save() {
  if (!canSave.value) return;
  reorderColumns();
  errorMessage.value = '';
  successMessage.value = '';
  try {
    await keeperStore.saveScope(props.scope);
    successMessage.value = t('keeper.listSettings.saved');
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  }
}

function resetDefaults() {
  keeperStore.resetScopeToDefaults(props.scope);
}
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t(hintKey) }}
    </v-alert>
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4">
      {{ errorMessage }}
    </v-alert>
    <v-alert v-if="successMessage" type="success" variant="tonal" density="compact" class="mb-4">
      {{ successMessage }}
    </v-alert>

    <VueDraggableNext
      v-if="listConfig.columns.length"
      :list="listConfig.columns"
      item-key="fieldName"
      handle=".drag-handle"
      class="d-flex flex-column ga-2"
      @end="reorderColumns"
    >
      <v-card v-for="element in listConfig.columns" :key="element.fieldName" variant="outlined">
        <v-card-text class="d-flex flex-wrap align-center ga-3 py-3">
          <v-icon class="drag-handle cursor-move" size="20">mdi-drag</v-icon>
          <div class="text-subtitle-2 font-weight-medium" style="min-width: 160px">
            {{ fieldLabel(element.fieldName) }}
          </div>
          <v-switch v-model="element.visible" :label="t('keeper.listSettings.visible')" hide-details density="compact" />
          <v-switch v-model="element.sortable" :label="t('keeper.listSettings.sortable')" hide-details density="compact" />
          <v-switch v-model="element.filterable" :label="t('keeper.listSettings.filterable')" hide-details density="compact" />
          <v-spacer />
          <span class="text-caption text-medium-emphasis">{{ formatSummary(element) }}</span>
          <v-btn size="small" variant="tonal" @click="openFormatDialog(element)">
            {{ t('keeper.listSettings.format') }}
          </v-btn>
        </v-card-text>
      </v-card>
    </VueDraggableNext>

    <div class="d-flex flex-wrap ga-2 mt-4">
      <v-btn color="primary" variant="flat" :loading="saving" :disabled="!canSave" @click="save">
        {{ t('keeper.listSettings.save') }}
      </v-btn>
      <v-btn variant="outlined" @click="resetDefaults">
        {{ t('keeper.listSettings.resetDefaults') }}
      </v-btn>
    </div>

    <OdakSiparisListColumnFormatDialog
      v-model="formatDialogOpen"
      :column="formatColumn"
      :column-label="formatColumn ? fieldLabel(formatColumn.fieldName) : ''"
      :condition-fields="conditionFields"
      @save="onFormatSave"
    />
  </div>
</template>

<style scoped>
.cursor-move {
  cursor: move;
}
</style>
