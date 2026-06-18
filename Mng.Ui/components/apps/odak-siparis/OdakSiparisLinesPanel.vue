<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisLineDialog from '@/components/apps/odak-siparis/OdakSiparisLineDialog.vue';
import OdakSiparisSubListScroll from '@/components/apps/odak-siparis/OdakSiparisSubListScroll.vue';
import OdakSiparisSubListToolbar from '@/components/apps/odak-siparis/OdakSiparisSubListToolbar.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocDelete } from '@/services/operationCoreService';
import {
  ODAK_SIPARIS_CONFIG,
  ODAK_DATA_TABLE_EXPAND_COLUMN,
  ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  ODAK_SUB_LIST_TABLE_CLASS,
  type OdakLineRow,
} from '@/utils/odakSiparisConfig';
import { hubListCellDisplayValue, hubListCellStyle } from '@/utils/odakSiparisHubListCellFormat';
import { loadOdakLineFieldPoliciesOnly, loadOdakLineListConfigOnly } from '@/utils/odakSiparisHubSettingsService';
import {
  buildLineListHeaders,
  defaultOdakLineListConfig,
  lineListCellRaw,
  ODAK_LINE_LIST_KEY_TO_FIELD,
  type OdakLineListConfig,
} from '@/utils/odakSiparisLineListSettings';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { useOdakFieldAccess } from '@/composables/useOdakFieldAccess';
import { odakLineSettingsFieldLabelTr } from '@/utils/odakSiparisSettingsLabels';
import { buildOdakLineExpandSummaryRows } from '@/utils/odakSiparisLineSummary';
import {
  lineDataId,
  listLinesForPackage,
  type OdakLineDialogMode,
} from '@/utils/odakSiparisLineService';
import { EditIcon, EyeIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

const props = withDefaults(
  defineProps<{
    packageId: string;
    packageNo?: string;
    compact?: boolean;
  }>(),
  {
    compact: false,
  }
);

const { t } = useAppI18n();

const loading = ref(false);
const errorMessage = ref('');
const lines = ref<OdakLineRow[]>([]);
const expandedLineIds = ref<string[]>([]);

const deleteDialog = ref(false);
const lineToDelete = ref<OdakLineRow | null>(null);
const deleting = ref(false);

const lineDialogOpen = ref(false);
const lineDialogMode = ref<OdakLineDialogMode>('view');
const lineDialogId = ref<string | undefined>();
const lineDialogSeed = ref<OdakLineRow | null>(null);

const listConfig = ref<OdakLineListConfig>(defaultOdakLineListConfig());
const fieldPolicies = ref<OdakFieldPoliciesBlob>({ policiesByField: {} });
const { canViewListColumn } = useOdakFieldAccess(fieldPolicies, ODAK_LINE_LIST_KEY_TO_FIELD);

function columnTitle(fieldName: string, listKey: string): string {
  void listKey;
  return odakLineSettingsFieldLabelTr(fieldName);
}

const configurableHeaders = computed(() =>
  buildLineListHeaders(listConfig.value, columnTitle, (listKey) => canViewListColumn(listKey))
);

const headers = computed(() => [
  { ...ODAK_DATA_TABLE_EXPAND_COLUMN },
  ...configurableHeaders.value,
  {
    title: t('odakSiparis.lines.columns.actions'),
    key: 'actions',
    width: 132,
    ...ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  },
]);

function cellDisplayValue(raw: string, listKey: string, item: OdakLineRow): string {
  return hubListCellDisplayValue(raw, listKey, listConfig.value, ODAK_LINE_LIST_KEY_TO_FIELD, item);
}

function cellStyle(listKey: string, raw: string, item: OdakLineRow): Record<string, string> {
  return hubListCellStyle(listKey, raw, listConfig.value, ODAK_LINE_LIST_KEY_TO_FIELD, item);
}

const filterLabel = computed(() => {
  if (props.compact) return '';
  return props.packageNo
    ? `${props.packageNo} · ${t('odakSiparis.detail.tabs.lines')}`
    : t('odakSiparis.lines.defaultTitle');
});

function expandSummaryRows(line: OdakLineRow) {
  return buildOdakLineExpandSummaryRows(line, t);
}

async function loadLines() {
  if (!props.packageId) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    lines.value = await listLinesForPackage(props.packageId);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    lines.value = [];
  } finally {
    loading.value = false;
  }
}

function openLineDialog(mode: OdakLineDialogMode, row?: OdakLineRow) {
  lineDialogMode.value = mode;
  lineDialogId.value = row ? lineDataId(row) : undefined;
  lineDialogSeed.value = row ?? null;
  lineDialogOpen.value = true;
}

function confirmDelete(row: OdakLineRow) {
  lineToDelete.value = row;
  deleteDialog.value = true;
}

async function doDelete() {
  const row = lineToDelete.value;
  if (!row) return;
  const id = lineDataId(row);
  if (!id) return;
  deleting.value = true;
  try {
    await ocDelete(ODAK_SIPARIS_CONFIG.linesDataset, id);
    deleteDialog.value = false;
    lineToDelete.value = null;
    await loadLines();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
}

watch(expandedLineIds, (ids) => {
  if (ids.length > 1) expandedLineIds.value = [ids[ids.length - 1]!];
});

watch(
  () => props.packageId,
  () => {
    expandedLineIds.value = [];
    void loadLines();
  }
);

onMounted(() => {
  void loadOdakLineListConfigOnly()
    .then((cfg) => {
      listConfig.value = cfg;
    })
    .catch(() => {
      listConfig.value = defaultOdakLineListConfig();
    });
  void loadOdakLineFieldPoliciesOnly()
    .then((blob) => {
      fieldPolicies.value = blob;
    })
    .catch(() => {
      fieldPolicies.value = { policiesByField: {} };
    });
  void loadLines();
});
</script>

<template>
  <div>
    <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <OdakSiparisSubListScroll sticky-expand-column>
      <template #toolbar>
        <OdakSiparisSubListToolbar>
          <template #info>
            <span v-if="filterLabel" class="text-subtitle-1 font-weight-medium">{{ filterLabel }}</span>
            <v-chip v-if="lines.length" size="small" variant="tonal" color="primary">
              {{ lines.length }}
            </v-chip>
          </template>
          <template #actions>
            <v-btn icon variant="outlined" size="small" :loading="loading" @click="loadLines">
              <RefreshIcon size="18" />
            </v-btn>
            <v-btn color="primary" variant="flat" size="small" @click="openLineDialog('create')">
              <PlusIcon class="mr-1" size="16" />
              {{ t('odakSiparis.lines.add') }}
            </v-btn>
          </template>
        </OdakSiparisSubListToolbar>
      </template>

      <v-data-table
        v-model:expanded="expandedLineIds"
        :headers="headers"
        :items="lines"
        :loading="loading"
        item-value="__dataId"
        show-expand
        :expand-on-click="false"
        :density="compact ? 'compact' : 'comfortable'"
        :class="['border', 'rounded-md', ODAK_SUB_LIST_TABLE_CLASS]"
      >
      <template #expanded-row="{ columns, item }">
        <tr>
          <td :colspan="columns.length" class="pa-0">
            <div class="odak-line-expand-panel pa-3 px-4">
              <div class="text-caption font-weight-medium text-medium-emphasis mb-2">
                {{ t('odakSiparis.lines.expandTitle', { lineNo: item.lineNo ?? '—' }) }}
              </div>
              <v-row dense>
                <v-col
                  v-for="row in expandSummaryRows(item)"
                  :key="row.label"
                  cols="12"
                  sm="6"
                  md="4"
                >
                  <div class="text-caption text-medium-emphasis">{{ row.label }}</div>
                  <div class="text-body-2">{{ row.value }}</div>
                </v-col>
              </v-row>
              <div class="mt-2">
                <v-btn size="small" variant="tonal" color="primary" @click="openLineDialog('view', item)">
                  {{ t('odakSiparis.lines.viewFull') }}
                </v-btn>
              </div>
            </div>
          </td>
        </tr>
      </template>

      <template v-for="col in configurableHeaders" :key="col.key" #[`item.${col.key}`]="{ item }">
        <span
          :class="col.key === 'description' ? 'text-truncate d-inline-block' : undefined"
          :style="[
            col.key === 'description' ? { maxWidth: '280px' } : undefined,
            cellStyle(col.key, lineListCellRaw(item, col.key), item),
          ]"
          :title="col.key === 'description' ? item.description : undefined"
        >
          {{ cellDisplayValue(lineListCellRaw(item, col.key), col.key, item) }}
        </span>
      </template>
      <template #item.actions="{ item }">
        <div class="d-inline-flex align-center justify-end ga-1">
          <v-btn icon size="x-small" variant="text" color="primary" @click="openLineDialog('view', item)">
            <EyeIcon size="18" />
          </v-btn>
          <v-btn icon size="x-small" variant="text" @click="openLineDialog('edit', item)">
            <EditIcon size="18" />
          </v-btn>
          <v-btn icon size="x-small" variant="text" color="error" @click="confirmDelete(item)">
            <TrashIcon size="18" />
          </v-btn>
        </div>
      </template>
    </v-data-table>
    </OdakSiparisSubListScroll>

    <OdakSiparisLineDialog
      v-model="lineDialogOpen"
      :mode="lineDialogMode"
      :package-id="packageId"
      :package-no="packageNo"
      :line-id="lineDialogId"
      :seed-row="lineDialogSeed"
      @saved="loadLines"
    />

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card>
        <v-card-title>{{ t('odakSiparis.lines.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('odakSiparis.lines.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ t('odakSiparis.packages.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="doDelete">
            {{ t('odakSiparis.packages.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.odak-line-expand-panel {
  background: rgba(var(--v-theme-surface-variant), 0.2);
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
