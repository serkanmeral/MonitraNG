<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  diCreateTemplateFromSource,
  diExtractMessage,
  diGetDocxStructure,
  diGetTemplate,
  diListTemplates,
  diUpdateTemplateParameters,
} from '@/services/documentIntelligenceService';
import type { DiDocxParagraph, DiTemplateDetail, DiTemplateParameter, DiTemplateSummary } from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.menuTitle'), to: '/apps/document-intelligence' },
  { title: t('documentIntelligence.designer.title'), disabled: true },
]);

const loading = ref(false);
const saving = ref(false);
const error = ref<string | null>(null);
const notify = ref<string | null>(null);

const templates = ref<DiTemplateSummary[]>([]);
const activeTemplate = ref<DiTemplateDetail | null>(null);
const paragraphs = ref<DiDocxParagraph[]>([]);
const selectedParagraph = ref<DiDocxParagraph | null>(null);
const parameters = ref<DiTemplateParameter[]>([]);

const createDialog = ref(false);
const createSourceId = ref('');
const createName = ref('');
const creating = ref(false);

const paramKey = ref('');
const paramLabel = ref('');
const paramMode = ref<'manual' | 'incremental'>('manual');
const paramFormat = ref('ODK-COC-{yy}-{0:D3}');

async function loadTemplates() {
  loading.value = true;
  error.value = null;
  try {
    const res = await diListTemplates();
    templates.value = res.items;
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('documentIntelligence.designer.errors.list'));
  } finally {
    loading.value = false;
  }
}

async function selectTemplate(id: string) {
  loading.value = true;
  error.value = null;
  selectedParagraph.value = null;
  try {
    const detail = await diGetTemplate(id);
    activeTemplate.value = detail;
    parameters.value = [...detail.parameters];
    const structure = await diGetDocxStructure(detail.sourceResourceId);
    paragraphs.value = structure.paragraphs;
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('documentIntelligence.designer.errors.load'));
    activeTemplate.value = null;
    paragraphs.value = [];
  } finally {
    loading.value = false;
  }
}

function selectParagraph(p: DiDocxParagraph) {
  selectedParagraph.value = p;
  paramLabel.value = p.text.length > 48 ? p.text.slice(0, 48) + '…' : p.text;
  if (!paramKey.value) {
    paramKey.value = suggestKey(p.text);
  }
}

function suggestKey(text: string): string {
  const cleaned = text
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-zA-Z0-9]+/g, ' ')
    .trim()
    .split(/\s+/)
    .slice(0, 4)
    .map((w, i) => (i === 0 ? w.toLowerCase() : w.charAt(0).toUpperCase() + w.slice(1).toLowerCase()))
    .join('');
  return cleaned || 'param';
}

function addParameter() {
  const key = paramKey.value.trim();
  const label = paramLabel.value.trim();
  if (!key || !label || !selectedParagraph.value) return;
  if (parameters.value.some((p) => p.key.toLowerCase() === key.toLowerCase())) {
    error.value = t('documentIntelligence.designer.errors.duplicateKey');
    return;
  }

  const param: DiTemplateParameter = {
    key,
    label,
    dataType: 'text',
    valueSourceMode: paramMode.value,
    incremental:
      paramMode.value === 'incremental'
        ? {
            format: paramFormat.value.trim() || 'ODK-COC-{yy}-{0:D3}',
            startValue: 1,
            incrementStep: 1,
            scopeKey: key,
            resetPolicy: 'yearly',
          }
        : null,
    sourceBinding: {
      regionKind: 'paragraph',
      paragraphIndex: selectedParagraph.value.index,
      originalText: selectedParagraph.value.text,
    },
  };

  parameters.value = [...parameters.value, param];
  paramKey.value = '';
  paramLabel.value = '';
  paramMode.value = 'manual';
  notify.value = t('documentIntelligence.designer.paramAdded');
}

function removeParameter(key: string) {
  parameters.value = parameters.value.filter((p) => p.key !== key);
}

async function saveParameters() {
  if (!activeTemplate.value) return;
  saving.value = true;
  error.value = null;
  notify.value = null;
  try {
    const updated = await diUpdateTemplateParameters(activeTemplate.value.id, {
      parameters: parameters.value,
    });
    activeTemplate.value = updated;
    parameters.value = [...updated.parameters];
    await loadTemplates();
    notify.value = t('documentIntelligence.designer.saved');
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('documentIntelligence.designer.errors.save'));
  } finally {
    saving.value = false;
  }
}

async function submitCreate() {
  const sourceResourceId = createSourceId.value.trim();
  if (!sourceResourceId) return;
  creating.value = true;
  error.value = null;
  try {
    const created = await diCreateTemplateFromSource({
      sourceResourceId,
      name: createName.value.trim() || undefined,
    });
    createDialog.value = false;
    createSourceId.value = '';
    createName.value = '';
    await loadTemplates();
    await selectTemplate(created.id);
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('documentIntelligence.designer.errors.create'));
  } finally {
    creating.value = false;
  }
}

onMounted(async () => {
  await loadTemplates();
  const tid = typeof route.query.templateId === 'string' ? route.query.templateId : '';
  const sid = typeof route.query.sourceId === 'string' ? route.query.sourceId : '';
  if (tid) {
    await selectTemplate(tid);
  } else if (sid) {
    createSourceId.value = sid;
    createDialog.value = true;
  }
});

watch(
  () => route.query.templateId,
  async (tid) => {
    if (typeof tid === 'string' && tid) await selectTemplate(tid);
  }
);
</script>

<template>
  <div>
    <BaseBreadcrumb :title="t('documentIntelligence.designer.title')" :breadcrumbs="breadcrumbs" />

    <div class="d-flex align-center justify-space-between mb-4 ga-2 flex-wrap">
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('documentIntelligence.designer.subtitle') }}
      </p>
      <div class="d-flex ga-2">
        <v-btn variant="text" class="text-none" to="/apps/document-intelligence">
          {{ t('documentIntelligence.designer.backToResources') }}
        </v-btn>
        <v-btn color="primary" variant="flat" class="text-none" @click="createDialog = true">
          {{ t('documentIntelligence.designer.newFromTemplate') }}
        </v-btn>
      </div>
    </div>

    <v-alert v-if="error" type="error" variant="tonal" closable class="mb-3 rounded-lg" @click:close="error = null">
      {{ error }}
    </v-alert>
    <v-alert v-if="notify" type="success" variant="tonal" closable class="mb-3 rounded-lg" @click:close="notify = null">
      {{ notify }}
    </v-alert>

    <v-row>
      <v-col cols="12" md="3">
        <v-card variant="outlined" rounded="lg">
          <v-card-title class="text-subtitle-2 font-weight-bold">
            {{ t('documentIntelligence.designer.templateList') }}
          </v-card-title>
          <v-divider />
          <v-progress-linear v-if="loading && !activeTemplate" indeterminate color="primary" />
          <v-list density="compact" class="py-0">
            <v-list-item
              v-for="tpl in templates"
              :key="tpl.id"
              :active="activeTemplate?.id === tpl.id"
              rounded="lg"
              @click="selectTemplate(tpl.id)"
            >
              <v-list-item-title class="text-body-2">{{ tpl.name }}</v-list-item-title>
              <v-list-item-subtitle class="text-caption">
                {{ tpl.parameterCount }} {{ t('documentIntelligence.designer.paramCount') }}
              </v-list-item-subtitle>
            </v-list-item>
            <v-list-item v-if="!loading && !templates.length">
              <v-list-item-title class="text-body-2 text-medium-emphasis">
                {{ t('documentIntelligence.designer.noTemplates') }}
              </v-list-item-title>
            </v-list-item>
          </v-list>
        </v-card>
      </v-col>

      <v-col cols="12" md="5">
        <v-card variant="outlined" rounded="lg" min-height="420">
          <v-card-title class="text-subtitle-2 font-weight-bold d-flex align-center justify-space-between">
            <span>{{ t('documentIntelligence.designer.sourceText') }}</span>
            <v-chip v-if="activeTemplate" size="x-small" variant="tonal">{{ activeTemplate.sourceFileName }}</v-chip>
          </v-card-title>
          <v-divider />
          <v-card-text class="pa-0">
            <div v-if="!activeTemplate" class="pa-6 text-body-2 text-medium-emphasis text-center">
              {{ t('documentIntelligence.designer.pickTemplate') }}
            </div>
            <v-list v-else density="compact" class="py-0 di-para-list">
              <v-list-item
                v-for="p in paragraphs"
                :key="p.index"
                :active="selectedParagraph?.index === p.index"
                class="di-para-item"
                rounded="0"
                @click="selectParagraph(p)"
              >
                <template #prepend>
                  <v-chip size="x-small" variant="outlined" class="mr-2">P{{ p.index }}</v-chip>
                </template>
                <v-list-item-title class="text-body-2 text-wrap">{{ p.text }}</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-card-text>
        </v-card>
      </v-col>

      <v-col cols="12" md="4">
        <v-card variant="outlined" rounded="lg" class="mb-4">
          <v-card-title class="text-subtitle-2 font-weight-bold">
            {{ t('documentIntelligence.designer.addParam') }}
          </v-card-title>
          <v-divider />
          <v-card-text>
            <v-text-field
              v-model="paramKey"
              :label="t('documentIntelligence.designer.paramKey')"
              density="comfortable"
              variant="outlined"
              hide-details
              class="mb-3"
              :disabled="!selectedParagraph"
            />
            <v-text-field
              v-model="paramLabel"
              :label="t('documentIntelligence.designer.paramLabel')"
              density="comfortable"
              variant="outlined"
              hide-details
              class="mb-3"
              :disabled="!selectedParagraph"
            />
            <v-select
              v-model="paramMode"
              :items="[
                { value: 'manual', title: t('documentIntelligence.designer.modeManual') },
                { value: 'incremental', title: t('documentIntelligence.designer.modeIncremental') },
              ]"
              item-title="title"
              item-value="value"
              :label="t('documentIntelligence.designer.valueSource')"
              density="comfortable"
              variant="outlined"
              hide-details
              class="mb-3"
              :disabled="!selectedParagraph"
            />
            <v-text-field
              v-if="paramMode === 'incremental'"
              v-model="paramFormat"
              :label="t('documentIntelligence.designer.incrementalFormat')"
              hint="ODK-COC-{yy}-{0:D3}"
              persistent-hint
              density="comfortable"
              variant="outlined"
              class="mb-3"
              :disabled="!selectedParagraph"
            />
            <v-btn
              color="primary"
              variant="flat"
              class="text-none"
              block
              :disabled="!selectedParagraph"
              @click="addParameter"
            >
              {{ t('documentIntelligence.designer.addParamBtn') }}
            </v-btn>
          </v-card-text>
        </v-card>

        <v-card variant="outlined" rounded="lg">
          <v-card-title class="text-subtitle-2 font-weight-bold d-flex align-center justify-space-between">
            <span>{{ t('documentIntelligence.designer.paramList') }}</span>
            <v-btn
              color="primary"
              size="small"
              variant="flat"
              class="text-none"
              :loading="saving"
              :disabled="!activeTemplate"
              @click="saveParameters"
            >
              {{ t('documentIntelligence.designer.save') }}
            </v-btn>
          </v-card-title>
          <v-divider />
          <v-list density="compact">
            <v-list-item v-for="p in parameters" :key="p.key">
              <v-list-item-title class="text-body-2 font-weight-medium">{{ p.label }}</v-list-item-title>
              <v-list-item-subtitle class="text-caption">
                {{ p.key }} · {{ p.valueSourceMode }}
                <span v-if="p.incremental?.format"> · {{ p.incremental.format }}</span>
              </v-list-item-subtitle>
              <template #append>
                <v-btn icon size="x-small" variant="text" color="error" @click="removeParameter(p.key)">
                  <v-icon icon="mdi-close" size="18" />
                </v-btn>
              </template>
            </v-list-item>
            <v-list-item v-if="!parameters.length">
              <v-list-item-title class="text-body-2 text-medium-emphasis">
                {{ t('documentIntelligence.designer.noParams') }}
              </v-list-item-title>
            </v-list-item>
          </v-list>
        </v-card>
      </v-col>
    </v-row>

    <v-dialog v-model="createDialog" max-width="520">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.designer.newFromTemplate') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('documentIntelligence.designer.createHint') }}
          </p>
          <v-text-field
            v-model="createSourceId"
            :label="t('documentIntelligence.designer.sourceResourceId')"
            density="comfortable"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-text-field
            v-model="createName"
            :label="t('documentIntelligence.designer.templateName')"
            density="comfortable"
            variant="outlined"
            hide-details
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="createDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="creating"
            :disabled="!createSourceId.trim()"
            @click="submitCreate"
          >
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.di-para-list {
  max-height: 520px;
  overflow: auto;
}

.di-para-item {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.di-para-item:hover {
  background: rgba(var(--v-theme-primary), 0.06);
}
</style>
