<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AlarmRule, AlarmRuleSavePayload, CreateAlarmRuleRequest, UpdateAlarmRuleRequest } from '@/types/apps/alarm';
import {
  alarmRuleCreate,
  alarmRuleDelete,
  alarmRuleList,
  alarmRuleUpdate,
} from '@/services/alarmService';
import { severityBand } from '@/composables/useAlarmRuleFormCatalog';
import {
  buildDuplicateMatchKeySet,
  buildRuleConditionSummary,
  classifyRuleSource,
  computeRuleListStats,
  filterAlarmRules,
  getRuleScenarioId,
  isDuplicateRule,
  loadRuleListViewMode,
  ruleSourceColor,
  ruleSourceLabelKey,
  saveRuleListViewMode,
  type AlarmRuleListViewMode,
} from '@/composables/useAlarmRuleList';
import AcAlarmRuleFormDialog from '@/components/apps/alarm-center/AcAlarmRuleFormDialog.vue';
import AcAlarmRuleDetailPanel from '@/components/apps/alarm-center/AcAlarmRuleDetailPanel.vue';
import AcAlarmRulesSiemGrid from '@/components/apps/alarm-center/AcAlarmRulesSiemGrid.vue';

const { t } = useAppI18n();
const route = useRoute();

const loading = ref(true);
const saving = ref(false);
const togglingId = ref<string | null>(null);
const deletingId = ref<string | null>(null);
const errorLocal = ref<string | null>(null);
const infoLocal = ref<string | null>(null);
const rows = ref<AlarmRule[]>([]);

const viewMode = ref<AlarmRuleListViewMode>(loadRuleListViewMode());
const searchQuery = ref('');
const filterType = ref('');
const filterSource = ref('');
const filterMinSeverity = ref<number | null>(null);
const filterEnabledOnly = ref(false);

const dialogOpen = ref(false);
const dialogSaveError = ref<string | null>(null);
const deleteDialogOpen = ref(false);
const editingRule = ref<AlarmRule | null>(null);
const deleteTarget = ref<AlarmRule | null>(null);
const selectedRuleId = ref<string | null>(null);
const pendingRuleId = ref<string | null>(null);

const duplicateKeys = computed(() => buildDuplicateMatchKeySet(rows.value));

const listStats = computed(() => computeRuleListStats(rows.value));

const filteredRows = computed(() =>
  filterAlarmRules(rows.value, {
    search: searchQuery.value,
    type: filterType.value,
    source: filterSource.value,
    minSeverity: filterMinSeverity.value,
    enabledOnly: filterEnabledOnly.value,
  }),
);

const selectedRule = computed(
  () => filteredRows.value.find((r) => r.id === selectedRuleId.value) ?? rows.value.find((r) => r.id === selectedRuleId.value) ?? null,
);

const typeFilterItems = computed(() => [
  { title: t('alarmCenter.rules.filterAllTypes'), value: '' },
  { title: t('alarmCenter.rules.typeThresholdShort'), value: 'threshold' },
  { title: t('alarmCenter.rules.typeCorrelationShort'), value: 'correlation' },
  { title: t('alarmCenter.rules.typeScheduledShort'), value: 'scheduled' },
  { title: t('alarmCenter.rules.typeSequenceShort'), value: 'sequence' },
]);

const sourceFilterItems = computed(() => [
  { title: t('alarmCenter.rules.filterAllSources'), value: '' },
  { title: t('alarmCenter.rules.sourceSiemPack'), value: 'siem-pack' },
  { title: t('alarmCenter.rules.sourceMetric'), value: 'metric' },
  { title: t('alarmCenter.rules.sourceManual'), value: 'manual' },
  { title: t('alarmCenter.rules.sourceTest'), value: 'test' },
]);

const severityFilterItems = computed(() => [
  { title: t('alarmCenter.rules.filterAllSeverity'), value: null },
  { title: t('alarmCenter.rules.filterSeverityHigh', { n: 7 }), value: 7 },
  { title: t('alarmCenter.rules.filterSeverityMedium', { n: 4 }), value: 4 },
]);

const headers = computed(() => [
  { title: t('alarmCenter.rules.colName'), key: 'name', sortable: true },
  { title: t('alarmCenter.rules.colSource'), key: 'source', sortable: false },
  { title: t('alarmCenter.rules.colCondition'), key: 'condition', sortable: false },
  { title: t('alarmCenter.rules.colSeverity'), key: 'severity', sortable: true },
  { title: t('alarmCenter.rules.colEnabled'), key: 'enabled', sortable: true },
  { title: t('alarmCenter.rules.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

function severityColor(severity: number): string {
  if (severity >= 8) return 'error';
  if (severity >= 5) return 'warning';
  return 'info';
}

function conditionFor(rule: AlarmRule): string {
  return buildRuleConditionSummary(rule, t);
}

function selectRule(rule: AlarmRule) {
  selectedRuleId.value = rule.id;
}

function trySelectPendingRule() {
  const id = pendingRuleId.value;
  if (!id) return false;
  pendingRuleId.value = null;
  const rule = rows.value.find((r) => r.id === id);
  if (rule) {
    selectedRuleId.value = rule.id;
    return true;
  }
  infoLocal.value = t('alarmCenter.rules.ruleNotFound');
  return false;
}

function openCreate() {
  editingRule.value = null;
  dialogSaveError.value = null;
  dialogOpen.value = true;
}

function openEdit(row: AlarmRule) {
  editingRule.value = row;
  dialogSaveError.value = null;
  dialogOpen.value = true;
}

function openDelete(row: AlarmRule) {
  deleteTarget.value = row;
  deleteDialogOpen.value = true;
}

async function loadRows() {
  loading.value = true;
  errorLocal.value = null;
  try {
    rows.value = await alarmRuleList();
    if (trySelectPendingRule()) {
      return;
    }
    if (selectedRuleId.value && !rows.value.some((r) => r.id === selectedRuleId.value)) {
      selectedRuleId.value = rows.value[0]?.id ?? null;
    } else if (!selectedRuleId.value && rows.value.length > 0) {
      selectedRuleId.value = rows.value[0].id;
    }
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('alarmCenter.rules.loadError');
    rows.value = [];
    selectedRuleId.value = null;
  } finally {
    loading.value = false;
  }
}

async function toggleEnabled(rule: AlarmRule) {
  togglingId.value = rule.id;
  errorLocal.value = null;
  try {
    await alarmRuleUpdate(rule.id, { enabled: !rule.enabled });
    await loadRows();
    infoLocal.value = t('alarmCenter.rules.toggleSuccess');
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('alarmCenter.rules.saveError');
  } finally {
    togglingId.value = null;
  }
}

async function handleRuleSave(payload: AlarmRuleSavePayload) {
  saving.value = true;
  dialogSaveError.value = null;
  errorLocal.value = null;
  infoLocal.value = null;
  try {
    if (payload.isEdit && payload.id) {
      await alarmRuleUpdate(payload.id, payload.body as UpdateAlarmRuleRequest);
    } else {
      await alarmRuleCreate(payload.body as CreateAlarmRuleRequest);
    }
    dialogOpen.value = false;
    editingRule.value = null;
    infoLocal.value = t('alarmCenter.rules.saveSuccess');
    await loadRows();
  } catch (e: unknown) {
    dialogSaveError.value = e instanceof Error ? e.message : t('alarmCenter.rules.saveError');
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deletingId.value = deleteTarget.value.id;
  errorLocal.value = null;
  infoLocal.value = null;
  try {
    await alarmRuleDelete(deleteTarget.value.id);
    if (selectedRuleId.value === deleteTarget.value.id) selectedRuleId.value = null;
    deleteDialogOpen.value = false;
    infoLocal.value = t('alarmCenter.rules.deleteSuccess');
    await loadRows();
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('alarmCenter.rules.deleteError');
  } finally {
    deletingId.value = null;
    deleteTarget.value = null;
  }
}

function clearFilters() {
  searchQuery.value = '';
  filterType.value = '';
  filterSource.value = '';
  filterMinSeverity.value = null;
  filterEnabledOnly.value = false;
}

function tableRowProps(data: { item: AlarmRule }) {
  return {
    class: data.item.id === selectedRuleId.value ? 'ac-rules-table__row--selected' : '',
  };
}

function onTableRowClick(_event: Event, ctx: { item: AlarmRule }) {
  selectRule(ctx.item);
}

watch(viewMode, (mode) => saveRuleListViewMode(mode));

onMounted(() => {
  const q = route.query;
  if (typeof q.search === 'string' && q.search.trim()) {
    searchQuery.value = q.search.trim();
  }
  if (typeof q.ruleId === 'string' && q.ruleId.trim()) {
    pendingRuleId.value = q.ruleId.trim();
  }
  void loadRows();
});
</script>

<template>
  <div>
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>
    <v-alert v-if="infoLocal" type="success" variant="tonal" class="mb-4" closable @click:close="infoLocal = null">
      {{ infoLocal }}
    </v-alert>

    <!-- Summary strip -->
    <v-row dense class="mb-4">
      <v-col cols="6" sm="4" md="2">
        <v-card variant="tonal" color="primary" class="rounded-lg pa-3 text-center">
          <div class="text-h5 font-weight-bold">{{ listStats.total }}</div>
          <div class="text-caption">{{ t('alarmCenter.rules.statLabelTotal') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="4" md="2">
        <v-card variant="tonal" color="success" class="rounded-lg pa-3 text-center">
          <div class="text-h5 font-weight-bold">{{ listStats.enabled }}</div>
          <div class="text-caption">{{ t('alarmCenter.rules.statLabelEnabled') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="4" md="2">
        <v-card variant="tonal" color="primary" class="rounded-lg pa-3 text-center">
          <div class="text-h5 font-weight-bold">{{ listStats.siemPack }}</div>
          <div class="text-caption">{{ t('alarmCenter.rules.statLabelSiem') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="4" md="2">
        <v-card variant="tonal" color="teal" class="rounded-lg pa-3 text-center">
          <div class="text-h5 font-weight-bold">{{ listStats.metric }}</div>
          <div class="text-caption">{{ t('alarmCenter.rules.statLabelMetric') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="4" md="2">
        <v-card variant="tonal" class="rounded-lg pa-3 text-center">
          <div class="text-h5 font-weight-bold">{{ listStats.manual }}</div>
          <div class="text-caption">{{ t('alarmCenter.rules.statLabelManual') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="4" md="2">
        <v-card variant="tonal" :color="listStats.disabled ? 'warning' : 'default'" class="rounded-lg pa-3 text-center">
          <div class="text-h5 font-weight-bold">{{ listStats.disabled }}</div>
          <div class="text-caption">{{ t('alarmCenter.rules.statLabelDisabled') }}</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Toolbar -->
    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <v-btn-toggle v-model="viewMode" mandatory density="compact" color="primary" variant="outlined">
        <v-btn value="table" size="small">
          <v-icon start size="18">mdi-table</v-icon>
          {{ t('alarmCenter.rules.viewTable') }}
        </v-btn>
        <v-btn value="siem" size="small">
          <v-icon start size="18">mdi-view-grid</v-icon>
          {{ t('alarmCenter.rules.viewSiem') }}
        </v-btn>
      </v-btn-toggle>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('alarmCenter.rules.create') }}
      </v-btn>
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="loadRows">
        {{ t('alarmCenter.rules.refresh') }}
      </v-btn>
    </div>

    <!-- Filters -->
    <v-card variant="outlined" class="rounded-lg pa-3 pa-md-4 mb-4">
      <v-row dense>
        <v-col cols="12" md="4">
          <v-text-field
            v-model="searchQuery"
            :label="t('alarmCenter.rules.filterSearch')"
            :placeholder="t('alarmCenter.rules.filterSearchPlaceholder')"
            prepend-inner-icon="mdi-magnify"
            variant="outlined"
            density="compact"
            hide-details
            clearable
          />
        </v-col>
        <v-col cols="6" md="2">
          <v-select
            v-model="filterType"
            :items="typeFilterItems"
            item-title="title"
            item-value="value"
            :label="t('alarmCenter.rules.filterType')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col cols="6" md="2">
          <v-select
            v-model="filterSource"
            :items="sourceFilterItems"
            item-title="title"
            item-value="value"
            :label="t('alarmCenter.rules.filterSource')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col cols="6" md="2">
          <v-select
            v-model="filterMinSeverity"
            :items="severityFilterItems"
            item-title="title"
            item-value="value"
            :label="t('alarmCenter.rules.filterSeverity')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col cols="6" md="2" class="d-flex align-center gap-2">
          <v-switch
            v-model="filterEnabledOnly"
            :label="t('alarmCenter.rules.filterEnabledOnly')"
            color="primary"
            density="compact"
            hide-details
          />
          <v-btn v-if="searchQuery || filterType || filterSource || filterMinSeverity || filterEnabledOnly" icon="mdi-filter-off" size="small" variant="text" @click="clearFilters" />
        </v-col>
      </v-row>
    </v-card>

    <!-- Empty state -->
    <v-card v-if="!loading && rows.length === 0" variant="outlined" class="rounded-lg pa-8 text-center mb-4">
      <v-icon icon="mdi-shield-off-outline" size="48" color="primary" class="mb-3 opacity-60" />
      <div class="text-h6 font-weight-bold mb-2">{{ t('alarmCenter.rules.empty') }}</div>
      <p class="text-body-2 text-medium-emphasis mb-4">{{ t('alarmCenter.rules.emptyHint') }}</p>
      <div class="d-flex flex-wrap justify-center gap-2">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">{{ t('alarmCenter.rules.create') }}</v-btn>
      </div>
      <p class="text-caption text-medium-emphasis mt-4 mb-0">{{ t('alarmCenter.rules.emptySeedHint') }}</p>
    </v-card>

    <!-- SIEM view -->
    <template v-else-if="viewMode === 'siem'">
      <AcAlarmRulesSiemGrid
        :rules="filteredRows"
        :selected-id="selectedRuleId"
        @select="selectRule"
        @edit="openEdit"
        @create="openCreate"
      />
      <v-row v-if="selectedRule" class="mt-4">
        <v-col cols="12" md="6" lg="5">
          <AcAlarmRuleDetailPanel
            :rule="selectedRule"
            :duplicate-keys="duplicateKeys"
            :toggling="togglingId === selectedRule.id"
            @edit="openEdit(selectedRule)"
            @delete="openDelete(selectedRule)"
            @toggle-enabled="toggleEnabled(selectedRule)"
            @open-alarms="() => {}"
          />
        </v-col>
      </v-row>
    </template>

    <!-- Table + detail split -->
    <v-row v-else>
      <v-col cols="12" :lg="selectedRule ? 8 : 12">
        <v-data-table
          :headers="headers"
          :items="filteredRows"
          :loading="loading"
          :row-props="tableRowProps"
          item-value="id"
          density="comfortable"
          class="rounded-lg ac-rules-table"
          :no-data-text="t('alarmCenter.rules.emptyFiltered')"
          @click:row="onTableRowClick"
        >
          <template #item.name="{ item }">
            <div class="d-flex align-center gap-2 min-w-0">
              <v-tooltip v-if="isDuplicateRule(item, duplicateKeys)" :text="t('alarmCenter.rules.duplicateWarning')">
                <template #activator="{ props: tipProps }">
                  <v-icon v-bind="tipProps" icon="mdi-alert-circle-outline" color="warning" size="18" />
                </template>
              </v-tooltip>
              <button type="button" class="ac-rule-name-btn text-body-2 font-weight-medium text-start" @click.stop="openEdit(item)">
                {{ item.name }}
              </button>
            </div>
          </template>
          <template #item.source="{ item }">
            <div class="d-flex flex-wrap gap-1">
              <v-chip v-if="getRuleScenarioId(item)" size="x-small" color="primary" variant="tonal">
                {{ getRuleScenarioId(item) }}
              </v-chip>
              <v-chip size="x-small" :color="ruleSourceColor(classifyRuleSource(item))" variant="tonal">
                {{ t(ruleSourceLabelKey(classifyRuleSource(item))) }}
              </v-chip>
            </div>
          </template>
          <template #item.condition="{ item }">
            <span class="text-body-2 text-medium-emphasis">{{ conditionFor(item) }}</span>
          </template>
          <template #item.severity="{ item }">
            <v-chip size="small" :color="severityColor(item.severity)" variant="tonal">
              {{ item.severity }} · {{ t(`alarmCenter.rules.severityBand.${severityBand(item.severity)}`) }}
            </v-chip>
          </template>
          <template #item.enabled="{ item }">
            <v-switch
              :model-value="item.enabled"
              :loading="togglingId === item.id"
              color="primary"
              density="compact"
              hide-details
              @click.stop
              @update:model-value="toggleEnabled(item)"
            />
          </template>
          <template #item.actions="{ item }">
            <v-tooltip :text="t('alarmCenter.rules.openAlarms')">
              <template #activator="{ props: tipProps }">
                <v-btn v-bind="tipProps" icon="mdi-bell-ring-outline" size="small" variant="text" :to="`/apps/alarm-center/alarms?ruleId=${item.id}`" @click.stop />
              </template>
            </v-tooltip>
            <v-btn icon="mdi-pencil" size="small" variant="text" @click.stop="openEdit(item)" />
            <v-btn
              icon="mdi-delete"
              size="small"
              variant="text"
              color="error"
              :loading="deletingId === item.id"
              @click.stop="openDelete(item)"
            />
          </template>
        </v-data-table>
      </v-col>
      <v-col v-if="selectedRule" cols="12" lg="4">
        <AcAlarmRuleDetailPanel
          :rule="selectedRule"
          :duplicate-keys="duplicateKeys"
          :toggling="togglingId === selectedRule.id"
          @edit="openEdit(selectedRule)"
          @delete="openDelete(selectedRule)"
          @toggle-enabled="toggleEnabled(selectedRule)"
          @open-alarms="() => {}"
        />
      </v-col>
    </v-row>

    <AcAlarmRuleFormDialog
      v-model="dialogOpen"
      :editing-rule="editingRule"
      :saving="saving"
      :save-error="dialogSaveError"
      @save="handleRuleSave"
    />

    <v-dialog v-model="deleteDialogOpen" max-width="440">
      <v-card class="rounded-lg">
        <v-card-title>{{ t('alarmCenter.rules.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('alarmCenter.rules.deleteConfirm', { name: deleteTarget?.name ?? '' }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialogOpen = false">{{ t('alarmCenter.rules.cancel') }}</v-btn>
          <v-btn color="error" :loading="deletingId != null" @click="confirmDelete">{{ t('alarmCenter.rules.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.ac-rule-name-btn {
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
  color: inherit;
  max-width: 100%;
}

.ac-rule-name-btn:hover {
  color: rgb(var(--v-theme-primary));
  text-decoration: underline;
}

.ac-rules-table :deep(.ac-rules-table__row--selected) {
  background: rgba(var(--v-theme-primary), 0.06);
}
</style>
