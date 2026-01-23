<script setup lang="ts">
import { computed, onMounted, watch } from 'vue';
import WidgetForm from '@/components/widgets/WidgetForm.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useWidgetStore, type UpdateWidgetDto } from '@/stores/apps/widget';

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

const page = computed(() => ({ title: t('widgets.form.editTitle') || 'Widget Düzenle' }));
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
</script>

<template>
  <div>
    <template v-if="initial">
      <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
      <WidgetForm :initial="initial" :t="t" @submit="handleSubmit" @cancel="handleCancel" />
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
