<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AlarmRule, AlarmRuleType, CreateAlarmRuleRequest, UpdateAlarmRuleRequest } from '@/types/apps/alarm';
import {
  alarmRuleCreate,
  alarmRuleDelete,
  alarmRuleList,
  alarmRuleUpdate,
} from '@/services/alarmService';

const { t } = useAppI18n();

const loading = ref(true);
const saving = ref(false);
const deletingId = ref<string | null>(null);
const errorLocal = ref<string | null>(null);
const infoLocal = ref<string | null>(null);
const rows = ref<AlarmRule[]>([]);
const filterEnabledOnly = ref(false);

const dialogOpen = ref(false);
const deleteDialogOpen = ref(false);
const editingRule = ref<AlarmRule | null>(null);
const deleteTarget = ref<AlarmRule | null>(null);

const form = ref({
  name: '',
  type: 'threshold' as AlarmRuleType,
  enabled: true,
  severity: 5,
  matchKey: '',
  operator: 'gt',
  threshold: 0,
  cooldownMinutes: 5,
  windowMinutes: 5,
  stalenessMinutes: 0,
  groupByFieldsText: '',
  dedupKeyTemplate: '',
});

const typeItems = computed(() => [
  { title: t('operationCore.adminAlarmRules.typeThreshold'), value: 'threshold' },
  { title: t('operationCore.adminAlarmRules.typeCorrelation'), value: 'correlation' },
  { title: t('operationCore.adminAlarmRules.typeScheduled'), value: 'scheduled' },
]);

const operatorItems = computed(() => [
  { title: '>', value: 'gt' },
  { title: '>=', value: 'gte' },
  { title: '<', value: 'lt' },
  { title: '<=', value: 'lte' },
  { title: '=', value: 'eq' },
]);

const headers = computed(() => [
  { title: t('operationCore.adminAlarmRules.colName'), key: 'name', sortable: true },
  { title: t('operationCore.adminAlarmRules.colType'), key: 'type', sortable: true },
  { title: t('operationCore.adminAlarmRules.colMatchKey'), key: 'matchKey', sortable: true },
  { title: t('operationCore.adminAlarmRules.colSeverity'), key: 'severity', sortable: true },
  { title: t('operationCore.adminAlarmRules.colEnabled'), key: 'enabled', sortable: true },
  { title: t('operationCore.adminAlarmRules.colThreshold'), key: 'threshold', sortable: false },
  { title: t('operationCore.adminAlarmRules.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const filteredRows = computed(() => {
  if (!filterEnabledOnly.value) return rows.value;
  return rows.value.filter((r) => r.enabled);
});

const isEdit = computed(() => editingRule.value != null);
const showThresholdFields = computed(() => form.value.type === 'threshold');
const showCorrelationFields = computed(() => form.value.type === 'correlation');
const showScheduledFields = computed(() => form.value.type === 'scheduled');

function severityColor(severity: number): string {
  if (severity >= 8) return 'error';
  if (severity >= 5) return 'warning';
  return 'info';
}

function resetForm() {
  form.value = {
    name: '',
    type: 'threshold',
    enabled: true,
    severity: 5,
    matchKey: '',
    operator: 'gt',
    threshold: 0,
    cooldownMinutes: 5,
    windowMinutes: 5,
    stalenessMinutes: 0,
    groupByFieldsText: '',
    dedupKeyTemplate: '',
  };
}

function openCreate() {
  editingRule.value = null;
  resetForm();
  dialogOpen.value = true;
}

function openEdit(row: AlarmRule) {
  editingRule.value = row;
  form.value = {
    name: row.name,
    type: (row.type as AlarmRuleType) || 'threshold',
    enabled: row.enabled,
    severity: row.severity,
    matchKey: row.matchKey,
    operator: row.operator || 'gt',
    threshold: row.threshold,
    cooldownMinutes: row.cooldownMinutes,
    windowMinutes: row.windowMinutes,
    stalenessMinutes: row.stalenessMinutes,
    groupByFieldsText: (row.groupByFields || []).join(', '),
    dedupKeyTemplate: row.dedupKeyTemplate || '',
  };
  dialogOpen.value = true;
}

function openDelete(row: AlarmRule) {
  deleteTarget.value = row;
  deleteDialogOpen.value = true;
}

function parseGroupByFields(): string[] {
  return form.value.groupByFieldsText
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);
}

async function loadRows() {
  loading.value = true;
  errorLocal.value = null;
  try {
    rows.value = await alarmRuleList();
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('operationCore.adminAlarmRules.loadError');
    rows.value = [];
  } finally {
    loading.value = false;
  }
}

async function saveRule() {
  saving.value = true;
  errorLocal.value = null;
  infoLocal.value = null;
  try {
    const groupByFields = parseGroupByFields();
    if (isEdit.value && editingRule.value) {
      const body: UpdateAlarmRuleRequest = {
        name: form.value.name.trim(),
        enabled: form.value.enabled,
        severity: form.value.severity,
        operator: form.value.operator,
        threshold: form.value.threshold,
        cooldownMinutes: form.value.cooldownMinutes,
        windowMinutes: form.value.windowMinutes,
        stalenessMinutes: form.value.stalenessMinutes,
        groupByFields,
        dedupKeyTemplate: form.value.dedupKeyTemplate.trim() || undefined,
      };
      await alarmRuleUpdate(editingRule.value.id, body);
    } else {
      const body: CreateAlarmRuleRequest = {
        name: form.value.name.trim(),
        type: form.value.type,
        severity: form.value.severity,
        matchKey: form.value.matchKey.trim(),
        operator: form.value.operator,
        threshold: form.value.threshold,
        cooldownMinutes: form.value.cooldownMinutes,
        windowMinutes: form.value.windowMinutes,
        stalenessMinutes: form.value.stalenessMinutes,
        groupByFields: groupByFields.length ? groupByFields : undefined,
        dedupKeyTemplate: form.value.dedupKeyTemplate.trim() || undefined,
      };
      await alarmRuleCreate(body);
    }
    dialogOpen.value = false;
    infoLocal.value = t('operationCore.adminAlarmRules.saveSuccess');
    await loadRows();
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('operationCore.adminAlarmRules.saveError');
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
    deleteDialogOpen.value = false;
    infoLocal.value = t('operationCore.adminAlarmRules.deleteSuccess');
    await loadRows();
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('operationCore.adminAlarmRules.deleteError');
  } finally {
    deletingId.value = null;
    deleteTarget.value = null;
  }
}

onMounted(() => {
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

    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <v-chip variant="tonal" color="primary">
        {{ t('operationCore.adminAlarmRules.statTotal', { count: rows.length }) }}
      </v-chip>
      <v-switch
        v-model="filterEnabledOnly"
        density="compact"
        hide-details
        color="primary"
        :label="t('operationCore.adminAlarmRules.filterEnabledOnly')"
      />
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('operationCore.adminAlarmRules.create') }}
      </v-btn>
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="loadRows">
        {{ t('operationCore.adminAlarmRules.refresh') }}
      </v-btn>
    </div>

    <v-data-table
      :headers="headers"
      :items="filteredRows"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg"
      :no-data-text="t('operationCore.adminAlarmRules.empty')"
    >
      <template #item.type="{ item }">
        <v-chip size="small" variant="tonal">{{ item.type }}</v-chip>
      </template>
      <template #item.severity="{ item }">
        <v-chip size="small" :color="severityColor(item.severity)" variant="tonal">
          {{ item.severity }}
        </v-chip>
      </template>
      <template #item.enabled="{ item }">
        <v-chip size="small" :color="item.enabled ? 'success' : 'default'" variant="tonal">
          {{ item.enabled ? t('operationCore.adminAlarmRules.enabledYes') : t('operationCore.adminAlarmRules.enabledNo') }}
        </v-chip>
      </template>
      <template #item.threshold="{ item }">
        <span v-if="item.type === 'threshold'">{{ item.operator }} {{ item.threshold }}</span>
        <span v-else-if="item.type === 'correlation'">≥ {{ item.threshold }} / {{ item.windowMinutes }}m</span>
        <span v-else>{{ item.stalenessMinutes }}m</span>
      </template>
      <template #item.actions="{ item }">
        <v-btn icon="mdi-pencil" size="small" variant="text" @click="openEdit(item)" />
        <v-btn
          icon="mdi-delete"
          size="small"
          variant="text"
          color="error"
          :loading="deletingId === item.id"
          @click="openDelete(item)"
        />
      </template>
    </v-data-table>

    <v-dialog v-model="dialogOpen" max-width="640" persistent>
      <v-card class="rounded-lg">
        <v-card-title class="text-h6">
          {{ isEdit ? t('operationCore.adminAlarmRules.editTitle') : t('operationCore.adminAlarmRules.createTitle') }}
        </v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12" md="8">
              <v-text-field v-model="form.name" :label="t('operationCore.adminAlarmRules.fieldName')" required />
            </v-col>
            <v-col cols="12" md="4">
              <v-switch v-model="form.enabled" :label="t('operationCore.adminAlarmRules.fieldEnabled')" hide-details />
            </v-col>
            <v-col cols="12" md="6">
              <v-select
                v-model="form.type"
                :items="typeItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.adminAlarmRules.fieldType')"
                :disabled="isEdit"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="form.matchKey"
                :label="t('operationCore.adminAlarmRules.fieldMatchKey')"
                :disabled="isEdit"
                required
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model.number="form.severity" type="number" min="1" max="10" :label="t('operationCore.adminAlarmRules.fieldSeverity')" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model.number="form.cooldownMinutes" type="number" min="0" :label="t('operationCore.adminAlarmRules.fieldCooldown')" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model.number="form.windowMinutes" type="number" min="1" :label="t('operationCore.adminAlarmRules.fieldWindow')" />
            </v-col>
            <template v-if="showThresholdFields">
              <v-col cols="12" md="4">
                <v-select v-model="form.operator" :items="operatorItems" item-title="title" item-value="value" :label="t('operationCore.adminAlarmRules.fieldOperator')" />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field v-model.number="form.threshold" type="number" :label="t('operationCore.adminAlarmRules.fieldThreshold')" />
              </v-col>
            </template>
            <template v-if="showCorrelationFields">
              <v-col cols="12" md="4">
                <v-text-field v-model.number="form.threshold" type="number" min="1" :label="t('operationCore.adminAlarmRules.fieldEventCount')" />
              </v-col>
              <v-col cols="12">
                <v-text-field
                  v-model="form.groupByFieldsText"
                  :label="t('operationCore.adminAlarmRules.fieldGroupBy')"
                  :hint="t('operationCore.adminAlarmRules.fieldGroupByHint')"
                  persistent-hint
                />
              </v-col>
            </template>
            <template v-if="showScheduledFields">
              <v-col cols="12" md="6">
                <v-text-field v-model.number="form.stalenessMinutes" type="number" min="1" :label="t('operationCore.adminAlarmRules.fieldStaleness')" />
              </v-col>
            </template>
            <v-col cols="12">
              <v-text-field
                v-model="form.dedupKeyTemplate"
                :label="t('operationCore.adminAlarmRules.fieldDedupTemplate')"
                :placeholder="'{ruleId}:{key}'"
              />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">{{ t('operationCore.adminAlarmRules.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" @click="saveRule">{{ t('operationCore.adminAlarmRules.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialogOpen" max-width="440">
      <v-card class="rounded-lg">
        <v-card-title>{{ t('operationCore.adminAlarmRules.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('operationCore.adminAlarmRules.deleteConfirm', { name: deleteTarget?.name ?? '' }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialogOpen = false">{{ t('operationCore.adminAlarmRules.cancel') }}</v-btn>
          <v-btn color="error" :loading="deletingId != null" @click="confirmDelete">{{ t('operationCore.adminAlarmRules.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
