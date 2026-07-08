<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiDesignerParametersStudio from '@/components/apps/document-intelligence/DiDesignerParametersStudio.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diGetTemplate } from '@/services/documentIntelligenceService';
import type { DiTemplateDetail } from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const route = useRoute();
const router = useRouter();

const templateId = computed(() => String(route.params.id ?? ''));
const template = ref<DiTemplateDetail | null>(null);
const loading = ref(true);
const error = ref<string | null>(null);

const listPath = computed(() => {
  const categoryId = typeof route.query.categoryId === 'string' ? route.query.categoryId : template.value?.categoryId;
  if (categoryId) {
    return `/apps/document-intelligence/designer?categoryId=${encodeURIComponent(categoryId)}`;
  }
  return '/apps/document-intelligence/designer';
});

const editPath = computed(() => {
  const query = route.query.categoryId ? { categoryId: String(route.query.categoryId) } : undefined;
  return {
    path: `/apps/document-intelligence/designer/${templateId.value}/edit`,
    query,
  };
});

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.designer.title'), to: listPath.value },
  {
    title: template.value?.name ?? t('documentIntelligence.designer.parameterStudio.title'),
    disabled: true,
  },
]);

async function loadTemplateMeta() {
  loading.value = true;
  error.value = null;
  try {
    template.value = await diGetTemplate(templateId.value);
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.load');
    template.value = null;
  } finally {
    loading.value = false;
  }
}

function backToList() {
  router.push(listPath.value);
}

function openEditor() {
  router.push(editPath.value);
}

onMounted(() => {
  void loadTemplateMeta();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="t('documentIntelligence.designer.parameterStudio.title')" :breadcrumbs="breadcrumbs" />

    <v-card rounded="lg" class="pa-4 mb-4">
      <div class="d-flex align-center flex-wrap ga-3">
        <div class="flex-grow-1">
          <h1 class="text-h6 font-weight-bold mb-1">
            {{ t('documentIntelligence.designer.parameterStudio.title') }}
          </h1>
          <p v-if="template" class="text-body-2 text-medium-emphasis mb-0">
            {{ template.name }}
            <v-chip v-if="template.code" size="x-small" variant="tonal" class="ms-2">{{ template.code }}</v-chip>
          </p>
        </div>
        <v-btn variant="text" class="text-none" prepend-icon="mdi-arrow-left" @click="backToList">
          {{ t('documentIntelligence.designer.backToList') }}
        </v-btn>
        <v-btn variant="tonal" class="text-none" prepend-icon="mdi-file-document-edit-outline" @click="openEditor">
          {{ t('documentIntelligence.designer.openEditor') }}
        </v-btn>
      </div>
    </v-card>

    <v-alert v-if="error" type="error" variant="tonal" class="mb-4 rounded-lg">
      {{ error }}
    </v-alert>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <DiDesignerParametersStudio
      v-if="!loading && template"
      :template-id="templateId"
      @saved="loadTemplateMeta"
    />
  </div>
</template>
