<script setup lang="ts">
import { computed, onMounted, watch } from 'vue';
import WidgetForm from '@/components/widgets/WidgetForm.vue';
import WidgetDesignerWizard from '@/components/widgets/designer/WidgetDesignerWizard.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useWidgetStore, type UpdateWidgetDto } from '@/stores/apps/widget';
import { isManifestWidget } from '@/utils/widgets/widgetManifestAdapter';

definePageMeta({
  layout: 'default',
});

const route = useRoute();
const router = useRouter();
const widgetStore = useWidgetStore();

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties?.$i18n;
const t = (key: string) => (i18n?.t ?? i18n?.global?.t)?.(key) ?? key;

const id = computed(() => (route.params.id as string) ?? '');
const initial = computed(() => widgetStore.currentWidget);
const forceLegacyForm = computed(() => route.query.mode === 'advanced');
const useDesigner = computed(() => {
  if (forceLegacyForm.value) return false;
  return initial.value ? isManifestWidget(initial.value) : false;
});

const page = computed(() => ({
  title: useDesigner.value
    ? t('widgets.designer.editTitle') || 'Widget Düzenle'
    : t('widgets.form.editTitle') || 'Widget Düzenle',
}));
const breadcrumbs = computed(() => [
  { text: t('widgets.breadcrumbs.home') || 'Dashboard', disabled: false, href: '/dashboards/analytical' },
  { text: t('widgets.breadcrumbs.widgets') || 'Widgets', disabled: false, href: '/apps/widgets' },
  { text: page.value.title, disabled: true, href: '#' },
]);

async function load() {
  if (!id.value) return;
  try {
    await widgetStore.fetchWidgetById(id.value);
  } catch {
    widgetStore.resetCurrent();
    router.replace('/apps/widgets');
  }
}

onMounted(load);
watch(id, load);

async function handleSubmit(dto: UpdateWidgetDto) {
  if (!id.value) return;
  try {
    await widgetStore.updateWidget(id.value, dto);
    router.push('/apps/widgets');
  } catch {
    /* store sets error */
  }
}

function handleCancel() {
  router.push('/apps/widgets');
}

const legacyFormLink = computed(() => ({
  path: `/apps/widgets/${encodeURIComponent(id.value)}/edit`,
  query: { mode: 'advanced' },
}));

const designerFormLink = computed(() => ({
  path: `/apps/widgets/${encodeURIComponent(id.value)}/edit`,
}));
</script>

<template>
  <div>
    <template v-if="initial">
      <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

      <div v-if="isManifestWidget(initial)" class="d-flex justify-end mb-3">
        <v-btn
          v-if="useDesigner"
          variant="text"
          size="small"
          prepend-icon="mdi-code-braces"
          :to="legacyFormLink"
        >
          {{ t('widgets.designer.legacyFormLink') }}
        </v-btn>
        <v-btn
          v-else
          variant="text"
          size="small"
          prepend-icon="mdi-palette"
          :to="designerFormLink"
        >
          {{ t('widgets.designer.designerFormLink') }}
        </v-btn>
      </div>

      <WidgetDesignerWizard
        v-if="useDesigner"
        mode="edit"
        :initial-widget="initial"
        :t="t"
        @submit="handleSubmit"
        @cancel="handleCancel"
      />
      <WidgetForm v-else :initial="initial" :t="t" @submit="handleSubmit" @cancel="handleCancel" />
    </template>
    <template v-else>
      <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
      <div v-if="widgetStore.loading" class="d-flex justify-center align-center py-12">
        <v-progress-circular indeterminate color="primary" size="48" />
      </div>
      <div v-else class="d-flex justify-center align-center py-12">
        <v-alert type="warning" variant="tonal">
          {{ t('widgets.view.notFound') || 'Widget bulunamadı' }}
          <v-btn variant="text" color="primary" class="ml-2" @click="router.push('/apps/widgets')">
            {{ t('widgets.view.backToList') || 'Listeye Dön' }}
          </v-btn>
        </v-alert>
      </div>
    </template>
  </div>
</template>
