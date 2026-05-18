<script setup lang="ts">
import { computed, onMounted, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import MonitoringWidgetForm from '@/components/apps/monitoring/MonitoringWidgetForm.vue';
import { useWidgetStore, type UpdateWidgetDto } from '@/stores/apps/widget';

definePageMeta({ layout: 'default' });

const route = useRoute();
const router = useRouter();
const widgetStore = useWidgetStore();

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const id = computed(() => (route.params.id as string) ?? '');
const initial = computed(() => widgetStore.currentWidget);

const page = computed(() => ({ title: mt('monitoring.widgets.editTitle', 'Widget Düzenle') }));
const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('monitoring.pageTitle', 'Monitoring tanımları'), disabled: false, href: '/apps/monitoring' },
  { text: mt('monitoring.widgets.pageTitle', 'Monitoring Widget\'ları'), disabled: false, href: '/apps/monitoring/widgets' },
  { text: page.value.title, disabled: true, href: '#' },
]);

const isMonitoringWidget = computed(() => {
  const w = initial.value;
  if (!w) return false;
  const cfg = w.config as any;
  return cfg?.monitoring === true;
});

async function load() {
  if (!id.value) return;
  try {
    await widgetStore.fetchWidgetById(id.value);
  } catch {
    widgetStore.resetCurrent();
    router.replace('/apps/monitoring/widgets');
  }
}

onMounted(load);
watch(id, load);

async function handleUpdate(dto: UpdateWidgetDto) {
  if (!id.value) return;
  try {
    await widgetStore.updateWidget(id.value, dto);
    router.push('/apps/monitoring/widgets');
  } catch {
    /* store sets error */
  }
}

function handleCancel() {
  router.push('/apps/monitoring/widgets');
}
</script>

<template>
  <div>
    <template v-if="initial">
      <template v-if="isMonitoringWidget">
        <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
        <v-container fluid>
          <v-alert v-if="widgetStore.error" type="error" variant="tonal" dismissible class="mb-4" @click:close="widgetStore.clearError">
            {{ widgetStore.error }}
          </v-alert>
          <MonitoringWidgetForm
            :initial="initial"
            :t="mt"
            @update="handleUpdate"
            @cancel="handleCancel"
          />
        </v-container>
      </template>
      <template v-else>
        <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
        <v-container fluid>
          <v-alert type="warning" variant="tonal">
            {{ mt('monitoring.widgets.notMonitoringWidget', 'Bu widget monitoring widget değil. Genel widget düzenleyicisini kullanın.') }}
            <v-btn variant="text" color="primary" class="ml-2" :to="`/apps/widgets/${id}/edit`">
              {{ mt('monitoring.widgets.goToWidgetForm', 'Widget Düzenle') }}
            </v-btn>
          </v-alert>
        </v-container>
      </template>
    </template>
    <template v-else>
      <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
      <v-container fluid>
        <div v-if="widgetStore.loading" class="d-flex justify-center py-12">
          <v-progress-circular indeterminate color="primary" size="48" />
        </div>
        <v-alert v-else type="warning" variant="tonal">
          {{ mt('monitoring.widgets.widgetNotFound', 'Widget bulunamadı') }}
          <v-btn variant="text" color="primary" class="ml-2" @click="router.push('/apps/monitoring/widgets')">
            {{ mt('monitoring.widgets.backToList', 'Listeye dön') }}
          </v-btn>
        </v-alert>
      </v-container>
    </template>
  </div>
</template>
