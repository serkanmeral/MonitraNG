<script setup lang="ts">
import { computed } from 'vue';
import WidgetDesignerWizard from '@/components/widgets/designer/WidgetDesignerWizard.vue';
import WidgetForm from '@/components/widgets/WidgetForm.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useWidgetStore, type CreateWidgetDto } from '@/stores/apps/widget';

definePageMeta({
  layout: 'default',
});

const router = useRouter();
const route = useRoute();
const widgetStore = useWidgetStore();

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties?.$i18n;
const t = (key: string) => (i18n?.t ?? i18n?.global?.t)?.(key) ?? key;

const useLegacyForm = computed(() => route.query.mode === 'advanced');
const initialTemplateId = computed(() => {
  const raw = route.query.templateId;
  return typeof raw === 'string' ? raw : null;
});

const page = computed(() => ({
  title: useLegacyForm.value
    ? t('widgets.form.createTitle') || 'Yeni Widget Oluştur'
    : t('widgets.designer.createTitle') || 'Widget Tasarımcısı',
}));
const breadcrumbs = computed(() => [
  { text: t('widgets.breadcrumbs.home') || 'Dashboard', disabled: false, href: '/dashboards/analytical' },
  { text: t('widgets.breadcrumbs.widgets') || 'Widgets', disabled: false, href: '/apps/widgets' },
  { text: page.value.title, disabled: true, href: '#' },
]);

async function handleSubmit(dto: CreateWidgetDto) {
  try {
    await widgetStore.createWidget(dto);
    router.push('/apps/widgets');
  } catch {
    /* store sets error */
  }
}

function handleCancel() {
  router.push('/apps/widgets');
}
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <div v-if="!useLegacyForm" class="d-flex justify-end mb-3">
      <v-btn
        variant="text"
        size="small"
        prepend-icon="mdi-code-braces"
        :to="{ path: '/apps/widgets/new', query: { mode: 'advanced' } }"
      >
        {{ t('widgets.designer.legacyFormLink') }}
      </v-btn>
    </div>

    <WidgetDesignerWizard
      v-if="!useLegacyForm"
      :initial-template-id="initialTemplateId"
      :t="t"
      @submit="handleSubmit"
      @cancel="handleCancel"
    />
    <WidgetForm v-else :t="t" @submit="handleSubmit" @cancel="handleCancel" />
  </div>
</template>
