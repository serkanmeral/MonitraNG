<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import ReportingListColumnsPanel from '@/components/apps/reporting/ReportingListColumnsPanel.vue';
import ReportingSummaryDesignerPanel from '@/components/apps/reporting/ReportingSummaryDesignerPanel.vue';
import ReportingColumnAuthPanel from '@/components/apps/reporting/ReportingColumnAuthPanel.vue';
import ReportingReportVisibilityPanel from '@/components/apps/reporting/ReportingReportVisibilityPanel.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useDatasetStore, type FieldDefinition } from '@/stores/apps/dataset';
import type { ReportingExpandChildListTab, ReportingExpandConfig, ReportingSummaryConfig } from '@/types/apps/reporting';
import { emptyOdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { defaultReportingListConfigFromFields } from '@/utils/reportingListConfig';
import { emptyReportingSummaryConfig } from '@/utils/reportingSummary';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  expandConfig: ReportingExpandConfig;
  disabled?: boolean;
  domainKey?: string;
}>();

const { t } = useAppI18n();
const datasetStore = useDatasetStore();

const selectedIndex = ref<number | null>(null);
const detailTab = ref<'connection' | 'columns' | 'summary' | 'access'>('connection');
const addDialogOpen = ref(false);

const formTitle = ref('');
const formId = ref('');
const formDatasetName = ref('');
const formLinkField = ref('');
const formParentField = ref('__dataId');
const formEmptyMessage = ref('');
const formLimit = ref(500);

const childSchemaFields = ref<FieldDefinition[]>([]);
const childSchemaLoading = ref(false);
const childSchemaError = ref('');

const childTabs = computed(() => props.expandConfig.tabs ?? []);

const selectedTab = computed(() => {
  if (selectedIndex.value == null) return null;
  return childTabs.value[selectedIndex.value] ?? null;
});

const datasetItems = computed(() =>
  (datasetStore.datasets ?? [])
    .map((d) => ({
      value: d.name,
      title: d.title ? `${d.title} (${d.name})` : d.name,
    }))
    .sort((a, b) => a.title.localeCompare(b.title, 'tr'))
);

function ensureTabsArray(): ReportingExpandChildListTab[] {
  if (!props.expandConfig.tabs) {
    props.expandConfig.tabs = [];
  }
  return props.expandConfig.tabs;
}

function slugifyId(title: string): string {
  const base = title
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9ğüşıöç]+/gi, '_')
    .replace(/^_+|_+$/g, '')
    .slice(0, 40);
  return base || `tab_${Date.now()}`;
}

function uniqueTabId(preferred: string, skipIndex: number | null): string {
  const tabs = childTabs.value;
  let id = preferred || `tab_${Date.now()}`;
  let n = 2;
  while (tabs.some((tab, i) => i !== skipIndex && tab.id === id)) {
    id = `${preferred}_${n}`;
    n += 1;
  }
  return id;
}

function resetAddForm() {
  formTitle.value = '';
  formId.value = '';
  formDatasetName.value = '';
  formLinkField.value = '';
  formParentField.value = '__dataId';
  formEmptyMessage.value = '';
  formLimit.value = 500;
}

function selectTab(index: number) {
  selectedIndex.value = index;
  detailTab.value = 'connection';
}

function openAdd() {
  resetAddForm();
  addDialogOpen.value = true;
}

async function loadChildSchema(datasetName: string) {
  const name = datasetName?.trim();
  if (!name) {
    childSchemaFields.value = [];
    childSchemaError.value = '';
    return;
  }

  childSchemaLoading.value = true;
  childSchemaError.value = '';
  try {
    const ds = await datasetStore.fetchDatasetByName(name);
    childSchemaFields.value = ds?.fields ?? [];
    if (!childSchemaFields.value.length) {
      childSchemaError.value = t('reporting.expand.childTabs.noChildSchema');
    }
  } catch {
    childSchemaFields.value = [];
    childSchemaError.value = t('reporting.expand.childTabs.childSchemaError');
  } finally {
    childSchemaLoading.value = false;
  }
}

async function createTab() {
  const title = formTitle.value.trim();
  const datasetName = formDatasetName.value.trim();
  const linkField = formLinkField.value.trim();
  if (!title || !datasetName || !linkField) return;

  const preferredId = formId.value.trim() || slugifyId(title);
  const id = uniqueTabId(preferredId, null);
  const parentField = formParentField.value.trim() || '__dataId';
  const emptyMessage = formEmptyMessage.value.trim() || undefined;
  const limit = formLimit.value > 0 ? formLimit.value : 500;

  let listConfig = defaultReportingListConfigFromFields([]);
  try {
    const ds = await datasetStore.fetchDatasetByName(datasetName);
    if (ds?.fields?.length) {
      listConfig = defaultReportingListConfigFromFields(ds.fields);
    }
  } catch {
    /* empty columns — user can reset after schema loads */
  }

  const tabs = ensureTabsArray();
  tabs.push({
    id,
    title,
    childList: {
      datasetName,
      linkField,
      parentField,
      sort: listConfig.defaultSortBy,
      limit,
      expand: true,
      emptyMessage,
      listConfig,
      summary: emptyReportingSummaryConfig(),
    },
    fieldPolicies: emptyOdakFieldPoliciesBlob(),
    visibilityPolicies: [],
  });

  addDialogOpen.value = false;
  resetAddForm();
  selectedIndex.value = tabs.length - 1;
  detailTab.value = 'columns';
}

function removeTab(index: number) {
  const tabs = ensureTabsArray();
  const removed = tabs[index];
  tabs.splice(index, 1);
  if (removed && props.expandConfig.defaultTabId === removed.id) {
    props.expandConfig.defaultTabId = 'fields';
  }
  if (selectedIndex.value == null) return;
  if (selectedIndex.value === index) {
    selectedIndex.value = tabs.length ? Math.min(index, tabs.length - 1) : null;
  } else if (selectedIndex.value > index) {
    selectedIndex.value -= 1;
  }
}

function onConnectionDatasetChange(previousName: string, nextName: string) {
  const tab = selectedTab.value;
  if (!tab || previousName === nextName) return;
  // Dataset değişince sütunları yeni şemadan üret.
  void (async () => {
    await loadChildSchema(nextName);
    if (childSchemaFields.value.length) {
      tab.childList.listConfig = defaultReportingListConfigFromFields(childSchemaFields.value);
      tab.childList.sort = tab.childList.listConfig.defaultSortBy;
    }
  })();
}

function resetChildColumns() {
  const tab = selectedTab.value;
  if (!tab || !childSchemaFields.value.length) return;
  tab.childList.listConfig = defaultReportingListConfigFromFields(childSchemaFields.value);
  tab.childList.sort = tab.childList.listConfig.defaultSortBy;
}

function onChildSummaryUpdate(value: ReportingSummaryConfig) {
  const tab = selectedTab.value;
  if (!tab) return;
  tab.childList.summary = value;
}

function ensureTabAccess(tab: ReportingExpandChildListTab) {
  if (!tab.fieldPolicies) tab.fieldPolicies = emptyOdakFieldPoliciesBlob();
  if (!tab.visibilityPolicies) tab.visibilityPolicies = [];
  return tab;
}

function resetTabColumnAuth() {
  const tab = selectedTab.value;
  if (!tab) return;
  tab.fieldPolicies = emptyOdakFieldPoliciesBlob();
}

function resetTabVisibility() {
  const tab = selectedTab.value;
  if (!tab) return;
  tab.visibilityPolicies = [];
}

function ensureChildSummary(): ReportingSummaryConfig {
  const tab = selectedTab.value;
  if (!tab) return emptyReportingSummaryConfig();
  if (!tab.childList.summary) {
    tab.childList.summary = emptyReportingSummaryConfig();
  }
  return tab.childList.summary;
}

function updateSelectedDataset(value: unknown) {
  const tab = selectedTab.value;
  if (!tab) return;
  const next = typeof value === 'string' ? value : '';
  const prev = tab.childList.datasetName;
  tab.childList.datasetName = next;
  onConnectionDatasetChange(prev, next);
}

function updateSelectedParentField(value: unknown) {
  const tab = selectedTab.value;
  if (!tab) return;
  const next = typeof value === 'string' ? value.trim() : '';
  tab.childList.parentField = next || '__dataId';
}

function updateSelectedEmptyMessage(value: unknown) {
  const tab = selectedTab.value;
  if (!tab) return;
  const next = typeof value === 'string' ? value.trim() : '';
  tab.childList.emptyMessage = next || undefined;
}

function updateSelectedLimit(value: unknown) {
  const tab = selectedTab.value;
  if (!tab) return;
  const n = typeof value === 'number' ? value : Number(value);
  tab.childList.limit = Number.isFinite(n) && n > 0 ? n : 500;
}

watch(
  () => selectedTab.value?.childList.datasetName,
  (name) => {
    void loadChildSchema(name ?? '');
  },
  { immediate: true }
);

watch(childTabs, (tabs) => {
  if (!tabs.length) {
    selectedIndex.value = null;
    return;
  }
  if (selectedIndex.value == null) {
    selectedIndex.value = 0;
    return;
  }
  if (selectedIndex.value >= tabs.length) {
    selectedIndex.value = tabs.length - 1;
  }
});

onMounted(async () => {
  if (!datasetStore.datasets?.length) {
    try {
      await datasetStore.fetchAllDatasets();
    } catch {
      /* designer already loads datasets in most flows */
    }
  }
  if (childTabs.value.length && selectedIndex.value == null) {
    selectedIndex.value = 0;
  }
});
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t('reporting.expand.childTabs.hint') }}
    </v-alert>

    <div class="d-flex justify-space-between align-center mb-3">
      <div class="text-subtitle-2">{{ t('reporting.expand.childTabs.title') }}</div>
      <v-btn
        size="small"
        color="primary"
        variant="tonal"
        :disabled="disabled || !expandConfig.enabled"
        @click="openAdd"
      >
        <PlusIcon size="16" class="mr-1" />
        {{ t('reporting.expand.childTabs.add') }}
      </v-btn>
    </div>

    <v-alert
      v-if="!expandConfig.enabled"
      type="warning"
      variant="tonal"
      density="compact"
      class="mb-3"
    >
      {{ t('reporting.expand.childTabs.needEnabled') }}
    </v-alert>

    <v-alert
      v-else-if="!childTabs.length"
      type="info"
      variant="tonal"
      density="compact"
    >
      {{ t('reporting.expand.childTabs.empty') }}
    </v-alert>

    <template v-else>
      <v-list density="compact" class="border rounded mb-4">
        <v-list-item
          v-for="(tab, index) in childTabs"
          :key="tab.id"
          :active="selectedIndex === index"
          :disabled="disabled"
          color="primary"
          @click="selectTab(index)"
        >
          <v-list-item-title>{{ tab.title }}</v-list-item-title>
          <v-list-item-subtitle>
            {{ tab.childList.datasetName }} · {{ tab.childList.linkField }} ←
            {{ tab.childList.parentField || '__dataId' }}
          </v-list-item-subtitle>
          <template #append>
            <v-btn
              icon
              variant="text"
              size="small"
              color="error"
              :disabled="disabled || !expandConfig.enabled"
              :aria-label="t('reporting.expand.childTabs.remove')"
              @click.stop="removeTab(index)"
            >
              <TrashIcon size="16" />
            </v-btn>
          </template>
        </v-list-item>
      </v-list>

      <template v-if="selectedTab">
        <v-tabs v-model="detailTab" density="compact" color="primary" class="mb-3">
          <v-tab value="connection">{{ t('reporting.expand.childTabs.detailConnection') }}</v-tab>
          <v-tab value="columns">{{ t('reporting.expand.childTabs.detailColumns') }}</v-tab>
          <v-tab value="summary">{{ t('reporting.expand.childTabs.detailSummary') }}</v-tab>
          <v-tab value="access">{{ t('reporting.expand.childTabs.detailAccess') }}</v-tab>
        </v-tabs>

        <v-window v-model="detailTab">
          <v-window-item value="connection">
            <v-text-field
              v-model="selectedTab.title"
              :label="t('reporting.expand.childTabs.formTitle')"
              :disabled="disabled || !expandConfig.enabled"
              density="compact"
              variant="outlined"
              hide-details
              class="mb-3"
            />
            <v-text-field
              v-model="selectedTab.id"
              :label="t('reporting.expand.childTabs.formId')"
              :hint="t('reporting.expand.childTabs.formIdHint')"
              :disabled="disabled || !expandConfig.enabled"
              persistent-hint
              density="compact"
              variant="outlined"
              class="mb-3"
            />
            <v-autocomplete
              :model-value="selectedTab.childList.datasetName"
              :items="datasetItems"
              item-title="title"
              item-value="value"
              :label="t('reporting.expand.childTabs.formDataset')"
              :loading="datasetStore.loading"
              :disabled="disabled || !expandConfig.enabled"
              density="compact"
              variant="outlined"
              hide-details
              class="mb-3"
              @update:model-value="updateSelectedDataset"
            />
            <v-text-field
              v-model="selectedTab.childList.linkField"
              :label="t('reporting.expand.childTabs.formLinkField')"
              :hint="t('reporting.expand.childTabs.formLinkFieldHint')"
              :disabled="disabled || !expandConfig.enabled"
              persistent-hint
              density="compact"
              variant="outlined"
              class="mb-3"
            />
            <v-text-field
              :model-value="selectedTab.childList.parentField ?? '__dataId'"
              :label="t('reporting.expand.childTabs.formParentField')"
              :hint="t('reporting.expand.childTabs.formParentFieldHint')"
              :disabled="disabled || !expandConfig.enabled"
              persistent-hint
              density="compact"
              variant="outlined"
              class="mb-3"
              @update:model-value="updateSelectedParentField"
            />
            <v-text-field
              :model-value="selectedTab.childList.emptyMessage ?? ''"
              :label="t('reporting.expand.childTabs.formEmptyMessage')"
              :disabled="disabled || !expandConfig.enabled"
              density="compact"
              variant="outlined"
              hide-details
              class="mb-3"
              @update:model-value="updateSelectedEmptyMessage"
            />
            <v-text-field
              :model-value="selectedTab.childList.limit ?? 500"
              type="number"
              min="1"
              :label="t('reporting.expand.childTabs.formLimit')"
              :disabled="disabled || !expandConfig.enabled"
              density="compact"
              variant="outlined"
              hide-details
              @update:model-value="updateSelectedLimit"
            />
          </v-window-item>

          <v-window-item value="columns">
            <v-progress-linear v-if="childSchemaLoading" indeterminate class="mb-3" />
            <v-alert
              v-else-if="childSchemaError"
              type="warning"
              variant="tonal"
              density="compact"
              class="mb-3"
            >
              {{ childSchemaError }}
            </v-alert>
            <ReportingListColumnsPanel
              v-if="selectedTab.childList.listConfig"
              :list-config="selectedTab.childList.listConfig"
              :fields="childSchemaFields"
              :disabled="
                disabled || !expandConfig.enabled || !childSchemaFields.length || childSchemaLoading
              "
              :domain-key="domainKey"
              allow-parent-field
              @reset="resetChildColumns"
            />
          </v-window-item>

          <v-window-item value="summary">
            <ReportingSummaryDesignerPanel
              :summary="ensureChildSummary()"
              :fields="childSchemaFields"
              :disabled="disabled || !expandConfig.enabled || childSchemaLoading"
              @update:summary="onChildSummaryUpdate"
            />
          </v-window-item>

          <v-window-item value="access">
            <template v-if="selectedTab">
              <p class="text-body-2 text-medium-emphasis mb-3">
                {{ t('reporting.expand.childTabs.accessHint') }}
              </p>
              <v-card variant="outlined" class="mb-4 pa-3">
                <div class="text-subtitle-2 mb-2">
                  {{ t('reporting.expand.childTabs.tabVisibilityTitle') }}
                </div>
                <ReportingReportVisibilityPanel
                  :visibility-policies="ensureTabAccess(selectedTab).visibilityPolicies!"
                  :disabled="disabled || !expandConfig.enabled"
                  @reset="resetTabVisibility"
                />
              </v-card>
              <v-card variant="outlined" class="pa-3">
                <div class="text-subtitle-2 mb-2">
                  {{ t('reporting.tabs.columnAuth') }}
                </div>
                <ReportingColumnAuthPanel
                  :field-policies="ensureTabAccess(selectedTab).fieldPolicies!"
                  :fields="childSchemaFields"
                  :disabled="
                    disabled || !expandConfig.enabled || !childSchemaFields.length || childSchemaLoading
                  "
                  @reset="resetTabColumnAuth"
                />
              </v-card>
            </template>
          </v-window-item>
        </v-window>
      </template>
    </template>

    <v-dialog v-model="addDialogOpen" max-width="560" persistent>
      <v-card>
        <v-card-title class="text-subtitle-1">
          {{ t('reporting.expand.childTabs.addTitle') }}
        </v-card-title>
        <v-card-text>
          <v-text-field
            v-model="formTitle"
            :label="t('reporting.expand.childTabs.formTitle')"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-text-field
            v-model="formId"
            :label="t('reporting.expand.childTabs.formId')"
            :hint="t('reporting.expand.childTabs.formIdHint')"
            persistent-hint
            density="compact"
            variant="outlined"
            class="mb-3"
          />
          <v-autocomplete
            v-model="formDatasetName"
            :items="datasetItems"
            item-title="title"
            item-value="value"
            :label="t('reporting.expand.childTabs.formDataset')"
            :loading="datasetStore.loading"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-text-field
            v-model="formLinkField"
            :label="t('reporting.expand.childTabs.formLinkField')"
            :hint="t('reporting.expand.childTabs.formLinkFieldHint')"
            persistent-hint
            density="compact"
            variant="outlined"
            class="mb-3"
          />
          <v-text-field
            v-model="formParentField"
            :label="t('reporting.expand.childTabs.formParentField')"
            :hint="t('reporting.expand.childTabs.formParentFieldHint')"
            persistent-hint
            density="compact"
            variant="outlined"
            class="mb-3"
          />
          <v-text-field
            v-model="formEmptyMessage"
            :label="t('reporting.expand.childTabs.formEmptyMessage')"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-text-field
            v-model.number="formLimit"
            type="number"
            min="1"
            :label="t('reporting.expand.childTabs.formLimit')"
            density="compact"
            variant="outlined"
            hide-details
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="addDialogOpen = false">
            {{ t('reporting.expand.childTabs.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :disabled="!formTitle.trim() || !formDatasetName.trim() || !formLinkField.trim()"
            @click="createTab"
          >
            {{ t('reporting.expand.childTabs.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
