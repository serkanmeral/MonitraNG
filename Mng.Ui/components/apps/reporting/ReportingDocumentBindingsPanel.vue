<script setup lang="ts">
/**
 * Designer — Belge şablonları paneli (DI template bindings).
 */
import { computed, onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { diListTemplates } from '@/services/documentIntelligenceService';
import { ReportingCatalogService } from '@/services/reportingCatalogService';
import type { DiTemplateSummary } from '@/types/apps/documentIntelligence';
import type {
  ReportingDocumentBinding,
  ReportingDocumentContextType,
} from '@/types/apps/reporting';
import {
  findReportingTemplateBindingConflict,
  newReportingDocumentBindingId,
} from '@/utils/reportingDocumentBindings';
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
  };
  emit('update:bindings', [...props.bindings, next]);
  addDialog.value = false;
}

function removeBinding(id: string) {
  emit(
    'update:bindings',
    props.bindings.filter((b) => b.id !== id)
  );
}

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

    <v-list v-else density="compact" class="border rounded">
      <v-list-item v-for="b in bindings" :key="b.id">
        <v-list-item-title>{{ b.label }}</v-list-item-title>
        <v-list-item-subtitle>
          {{ b.templateCode || b.templateId }}
          ·
          {{ t(`reporting.documentBindings.contextTypes.${b.contextType}`) }}
        </v-list-item-subtitle>
        <template #append>
          <v-btn
            icon
            variant="text"
            size="small"
            color="error"
            :disabled="disabled"
            :aria-label="t('reporting.documentBindings.remove')"
            @click="removeBinding(b.id)"
          >
            <TrashIcon size="16" />
          </v-btn>
        </template>
      </v-list-item>
    </v-list>

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
