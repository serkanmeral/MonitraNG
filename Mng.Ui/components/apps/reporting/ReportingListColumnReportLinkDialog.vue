<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';
import {
  ReportingCatalogService,
  reportingDomainKey,
} from '@/services/reportingCatalogService';
import type { OdakHubListColumnConfig } from '@/utils/odakSiparisHubListConfig';
import type { ReportingColumnLink, ReportingColumnLinkOpenIn } from '@/utils/reportingColumnLink';
import { normalizeReportingColumnLink } from '@/utils/reportingColumnLink';

const props = defineProps<{
  modelValue: boolean;
  column: OdakHubListColumnConfig | null;
  columnLabel: string;
  domainKey?: string;
  /** Child list: parentField mapping available */
  allowParentField?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [ReportingColumnLink | undefined];
}>();

const { t } = useAppI18n();
const authStore = useAuthStore();

const resolvedDomainKey = computed(
  () =>
    props.domainKey?.trim() ||
    reportingDomainKey(authStore.userInfo?.domain_id, authStore.userInfo?.domain_name)
);

const enabled = ref(false);
const targetReportId = ref<string | null>(null);
const openIn = ref<ReportingColumnLinkOpenIn>('newTab');
const targetParamId = ref('person');
const source = ref<'rowField' | 'parentField' | 'literal'>('rowField');
const sourceField = ref('personelId');
const literal = ref('');

const catalogService = computed(() => new ReportingCatalogService(resolvedDomainKey.value));
const reportItems = computed(() =>
  catalogService.value.listReports().map((r) => ({
    value: r.id,
    title: r.title || r.id,
  }))
);

const openInItems = computed(() => [
  { value: 'newTab' as const, title: t('reporting.columns.reportLinkOpenNewTab') },
  { value: 'sameTab' as const, title: t('reporting.columns.reportLinkOpenSameTab') },
]);

const sourceItems = computed(() => {
  const items = [
    { value: 'rowField' as const, title: t('reporting.columns.reportLinkSourceRow') },
    { value: 'literal' as const, title: t('reporting.columns.reportLinkSourceLiteral') },
  ];
  if (props.allowParentField) {
    items.splice(1, 0, {
      value: 'parentField' as const,
      title: t('reporting.columns.reportLinkSourceParent'),
    });
  }
  return items;
});

const dialog = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

watch(
  () => [props.modelValue, props.column] as const,
  ([open, col]) => {
    if (!open || !col) return;
    const link = normalizeReportingColumnLink(col.reportLink);
    enabled.value = !!link;
    targetReportId.value = link?.targetReportId ?? null;
    openIn.value = link?.openIn ?? 'newTab';
    const m = link?.paramMappings?.[0];
    targetParamId.value = m?.targetParamId ?? 'person';
    source.value = m?.source ?? 'rowField';
    sourceField.value = m?.field ?? col.fieldName;
    literal.value = m?.literal ?? '';
  }
);

function close() {
  dialog.value = false;
}

function clearAndSave() {
  emit('save', undefined);
  close();
}

function save() {
  if (!enabled.value) {
    emit('save', undefined);
    close();
    return;
  }
  const id = targetReportId.value?.trim();
  if (!id) return;
  const mapping = {
    targetParamId: targetParamId.value.trim() || 'person',
    source: source.value,
    ...(source.value === 'literal'
      ? { literal: literal.value }
      : { field: sourceField.value.trim() || props.column?.fieldName || '' }),
  };
  const link: ReportingColumnLink = {
    targetReportId: id,
    openIn: openIn.value,
    paramMappings: [mapping],
  };
  emit('save', link);
  close();
}
</script>

<template>
  <v-dialog v-model="dialog" max-width="520" persistent>
    <v-card>
      <v-card-title class="text-subtitle-1">
        {{ t('reporting.columns.reportLinkTitle') }}
        <span v-if="columnLabel" class="text-medium-emphasis text-body-2 ml-1">— {{ columnLabel }}</span>
      </v-card-title>
      <v-card-text>
        <p class="text-caption text-medium-emphasis mb-3">
          {{ t('reporting.columns.reportLinkHint') }}
        </p>
        <v-switch
          v-model="enabled"
          :label="t('reporting.columns.reportLinkEnabled')"
          color="primary"
          density="compact"
          hide-details
          class="mb-3"
        />
        <template v-if="enabled">
          <v-autocomplete
            v-model="targetReportId"
            :items="reportItems"
            item-title="title"
            item-value="value"
            :label="t('reporting.columns.reportLinkTarget')"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-select
            v-model="openIn"
            :items="openInItems"
            item-title="title"
            item-value="value"
            :label="t('reporting.columns.reportLinkOpenIn')"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-text-field
            v-model="targetParamId"
            :label="t('reporting.columns.reportLinkTargetParam')"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-select
            v-model="source"
            :items="sourceItems"
            item-title="title"
            item-value="value"
            :label="t('reporting.columns.reportLinkSource')"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-text-field
            v-if="source !== 'literal'"
            v-model="sourceField"
            :label="t('reporting.columns.reportLinkSourceField')"
            density="compact"
            variant="outlined"
            hide-details
          />
          <v-text-field
            v-else
            v-model="literal"
            :label="t('reporting.columns.reportLinkLiteral')"
            density="compact"
            variant="outlined"
            hide-details
          />
        </template>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-btn v-if="column?.reportLink" variant="text" color="error" @click="clearAndSave">
          {{ t('reporting.columns.reportLinkClear') }}
        </v-btn>
        <v-spacer />
        <v-btn variant="text" @click="close">{{ t('reporting.actions.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" :disabled="enabled && !targetReportId" @click="save">
          {{ t('reporting.actions.applyFormat') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
