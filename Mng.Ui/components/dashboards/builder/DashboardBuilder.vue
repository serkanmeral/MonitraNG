<script setup lang="ts">
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DashboardForm from '@/components/dashboards/builder/DashboardForm.vue';
import LayoutEditor from '@/components/dashboards/builder/LayoutEditor.vue';
import type { DashboardFormData } from '@/components/dashboards/builder/types';
import {
  useDashboardStore,
  defaultLayout,
  type Dashboard,
  type DashboardLayout,
  type CreateDashboardDto,
  type UpdateDashboardDto,
} from '@/stores/apps/dashboard';

const props = defineProps<{
  /** Edit modunda mevcut dashboard; yoksa create. */
  initial?: Dashboard | null;
}>();

const router = useRouter();
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties?.$i18n;
const t = (key: string) => {
  if (i18n?.t) return i18n.t(key);
  if (i18n?.global?.t) return i18n.global.t(key);
  return key;
};

const dashboardStore = useDashboardStore();
const isEdit = computed(() => !!props.initial?.__dataId || !!props.initial?.dataId);
const dashboardId = computed(() => props.initial?.__dataId ?? props.initial?.dataId ?? '');

const page = computed(() => ({
  title: isEdit.value ? t('dashboards.builder.editTitle') : t('dashboards.builder.createTitle'),
}));

const breadcrumbs = computed(() => [
  { text: t('dashboards.breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('dashboards.breadcrumbs.dashboards'), disabled: false, href: '/apps/dashboards' },
  { text: page.value.title, disabled: true, href: '#' },
]);

const form = ref<DashboardFormData>({
  name: '',
  title: '',
  description: '',
  slug: '',
  isDefault: false,
  isActive: true,
});

const layout = ref<DashboardLayout>(defaultLayout());

function initFromDashboard(d: Dashboard | null | undefined) {
  if (!d) {
    form.value = { name: '', title: '', description: '', slug: '', isDefault: false, isActive: true };
    layout.value = defaultLayout();
    return;
  }
  form.value = {
    name: d.name ?? '',
    title: d.title ?? '',
    description: d.description ?? '',
    slug: d.slug ?? '',
    isDefault: d.isDefault ?? false,
    isActive: d.isActive ?? true,
  };
  layout.value = d.layout ?? defaultLayout();
}

watch(() => props.initial, (d) => initFromDashboard(d), { immediate: true });

const canPreview = computed(() => {
  const slug = (props.initial?.slug ?? props.initial?.name ?? '').trim();
  return isEdit.value && !!slug;
});

const slugForPreview = computed(() => {
  return (props.initial?.slug ?? props.initial?.name ?? '').trim() || '';
});

function validate(): boolean {
  const name = (form.value.name ?? '').trim();
  const title = (form.value.title ?? '').trim();
  if (!name) {
    dashboardStore.error = t('dashboards.builder.validation.nameRequired');
    return false;
  }
  if (!title) {
    dashboardStore.error = t('dashboards.builder.validation.titleRequired');
    return false;
  }
  const rows = layout.value?.rows ?? [];
  if (!rows.length) {
    dashboardStore.error = t('dashboards.builder.validation.atLeastOneRow');
    return false;
  }
  dashboardStore.clearError();
  return true;
}

async function save() {
  if (!validate()) return;
  const name = (form.value.name ?? '').trim();
  const title = (form.value.title ?? '').trim();
  const slug = (form.value.slug ?? '').trim() || undefined;
  const description = (form.value.description ?? '').trim() || undefined;
  const dto: CreateDashboardDto = {
    name,
    title,
    description,
    slug,
    layout: layout.value,
    isDefault: form.value.isDefault,
    isActive: form.value.isActive,
    order: 0,
  };
  try {
    if (isEdit.value && dashboardId.value) {
      const updateDto: UpdateDashboardDto = {
        name,
        title,
        description,
        slug,
        layout: layout.value,
        isDefault: form.value.isDefault,
        isActive: form.value.isActive,
      };
      await dashboardStore.updateDashboard(dashboardId.value, updateDto);
    } else {
      await dashboardStore.createDashboard(dto);
    }
    router.push('/apps/dashboards');
  } catch {
    // store sets error
  }
}

function cancel() {
  dashboardStore.clearError();
  router.push('/apps/dashboards');
}

function preview() {
  if (!canPreview.value) return;
  const path = `/dashboards/${encodeURIComponent(slugForPreview.value)}`;
  router.push(path);
}
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-alert
      v-if="dashboardStore.error"
      type="error"
      variant="tonal"
      density="compact"
      class="mb-4"
      closable
      @click:close="dashboardStore.clearError"
    >
      {{ dashboardStore.error }}
    </v-alert>

    <v-row>
      <v-col cols="12" md="4" lg="3">
        <DashboardForm v-model="form" :disabled="dashboardStore.loading" :t="t" />
        <v-card variant="outlined">
          <v-card-text class="d-flex flex-wrap ga-2">
            <v-btn color="primary" variant="flat" :loading="dashboardStore.loading" @click="save">
              {{ t('dashboards.builder.actions.save') }}
            </v-btn>
            <v-btn variant="outlined" :disabled="dashboardStore.loading" @click="cancel">
              {{ t('dashboards.builder.actions.cancel') }}
            </v-btn>
            <v-btn
              v-if="canPreview"
              variant="tonal"
              color="secondary"
              :disabled="dashboardStore.loading"
              @click="preview"
            >
              {{ t('dashboards.builder.actions.preview') }}
            </v-btn>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" md="8" lg="9">
        <v-card variant="outlined">
          <v-card-text>
            <LayoutEditor v-model="layout" :disabled="dashboardStore.loading" :t="t" />
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>
