<script setup lang="ts">
import { computed } from 'vue';
import WidgetForm from '@/components/widgets/WidgetForm.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useWidgetStore, type CreateWidgetDto } from '@/stores/apps/widget';

definePageMeta({
  layout: 'default',
});

const router = useRouter();
const widgetStore = useWidgetStore();

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties?.$i18n;
const t = (key: string) => (i18n?.t ?? i18n?.global?.t)?.(key) ?? key;

const page = computed(() => ({ title: t('widgets.form.createTitle') || 'Yeni Widget Oluştur' }));
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
    <WidgetForm :t="t" @submit="handleSubmit" @cancel="handleCancel" />
  </div>
</template>
