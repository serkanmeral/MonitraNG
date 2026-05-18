<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import type { TmIssueType } from '@/types/apps/taskManager';
import {
  TM_STATUS_THEME_COLORS,
  isLegacyHexStatusColor,
  isTmStatusThemeColor,
} from '@/utils/taskManagerStatusColor';
import { getTmStatusTablerIconComponent } from '@/utils/tmStatusTablerIcons';
import TmStatusIconPicker from '@/components/apps/task-manager/TmStatusIconPicker.vue';

definePageMeta({ layout: 'default' });

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const store = useTaskManagerStore();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);

const dialog = ref(false);
const editId = ref<string | null>(null);
const form = ref({
  name: '',
  description: '',
  icon: '',
  color: '' as string,
});

const legacyColorWarning = ref(false);

const deleteDialog = ref(false);
const deleteTarget = ref<TmIssueType | null>(null);

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: mt('taskManager.issueTypes.title', 'Görev tipleri'), disabled: true, href: '#' },
]);

const themeColorItems = computed(() =>
  [...TM_STATUS_THEME_COLORS].map((v) => ({
    value: v,
    title: mt(`taskManager.statuses.themeColor.${v}`, v),
  }))
);

const sortedTypes = computed(() =>
  [...store.issueTypes].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }))
);

function typeIconEl(name: string | null | undefined) {
  return getTmStatusTablerIconComponent(name);
}

function openCreate() {
  editId.value = null;
  legacyColorWarning.value = false;
  form.value = { name: '', description: '', icon: '', color: 'secondary' };
  errorLocal.value = null;
  dialog.value = true;
}

function openEdit(t: TmIssueType) {
  editId.value = t.__dataId;
  const c = t.color?.trim() ?? '';
  legacyColorWarning.value = isLegacyHexStatusColor(c) || (!!c && !isTmStatusThemeColor(c));
  const themePick = isTmStatusThemeColor(c) ? c : 'secondary';
  form.value = {
    name: t.name,
    description: t.description ?? '',
    icon: t.icon ?? '',
    color: themePick,
  };
  errorLocal.value = null;
  dialog.value = true;
}

function openDelete(t: TmIssueType) {
  deleteTarget.value = t;
  deleteDialog.value = true;
}

onMounted(async () => {
  loading.value = true;
  errorLocal.value = null;
  try {
    await store.loadLookups();
  } catch (e: any) {
    errorLocal.value = e?.message ?? mt('taskManager.issueTypes.loadError', 'Görev tipleri yüklenemedi.');
  } finally {
    loading.value = false;
  }
});

async function submitForm() {
  if (!form.value.name.trim()) return;
  saving.value = true;
  errorLocal.value = null;
  const desc = form.value.description.trim() || null;
  try {
    if (editId.value) {
      await store.updateIssueType(editId.value, {
        name: form.value.name,
        description: desc,
        icon: form.value.icon.trim() || null,
        color: form.value.color.trim() ? form.value.color.trim() : null,
      });
    } else {
      await store.createIssueType({
        name: form.value.name,
        description: desc,
        icon: form.value.icon.trim() || null,
        color: form.value.color.trim() ? form.value.color.trim() : null,
      });
    }
    dialog.value = false;
  } catch (e: any) {
    errorLocal.value = e?.message ?? mt('taskManager.issueTypes.saveError', 'Kaydedilemedi.');
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await store.deleteIssueType(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
  } catch (e: any) {
    errorLocal.value =
      e?.message ?? mt('taskManager.issueTypes.deleteError', 'Silinemedi (görevlerde kullanılıyor olabilir).');
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb :title="mt('taskManager.issueTypes.title', 'Görev tipleri')" :breadcrumbs="breadcrumbs" />

    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <div class="tm-hero d-flex flex-column flex-md-row flex-wrap align-start align-md-center justify-space-between gap-4 mb-6">
      <div>
        <h1 class="tm-hero-title text-h4">{{ mt('taskManager.issueTypes.title', 'Görev tipleri') }}</h1>
        <p class="tm-hero-sub mb-0">
          {{ mt('taskManager.issueTypes.subtitle', 'Task, Bug, Story vb. ortak tanımlar; görev oluştururken ve detayda seçilir.') }}
        </p>
      </div>
      <div class="d-flex flex-wrap gap-2">
        <v-btn variant="tonal" size="large" rounded="lg" class="text-none" to="/apps/task-manager/statuses">
          <v-icon icon="mdi-format-list-checks" start />
          {{ mt('taskManager.statuses.title', 'Durum havuzu') }}
        </v-btn>
        <v-btn variant="tonal" size="large" rounded="lg" class="text-none" to="/apps/task-manager/priorities">
          <v-icon icon="mdi-priority-high" start />
          {{ mt('taskManager.priorities.title', 'Öncelikler') }}
        </v-btn>
        <v-btn variant="tonal" size="large" rounded="lg" class="text-none" to="/apps/task-manager">
          <v-icon icon="mdi-arrow-left" start />
          {{ mt('taskManager.issueTypes.backToTasks', 'Görevlere dön') }}
        </v-btn>
        <v-btn color="primary" size="large" rounded="lg" class="text-none" @click="openCreate">
          <v-icon icon="mdi-plus" start />
          {{ mt('taskManager.issueTypes.newType', 'Yeni tip') }}
        </v-btn>
      </div>
    </div>

    <v-card class="tm-panel" rounded="xl" flat>
      <v-data-table
        :headers="[
          { title: mt('taskManager.issueTypes.colName', 'Ad'), key: 'name', sortable: true },
          { title: mt('taskManager.issueTypes.colDescription', 'Açıklama'), key: 'description', sortable: false },
          { title: mt('taskManager.issueTypes.colIcon', 'İkon'), key: 'icon', sortable: false },
          { title: mt('taskManager.issueTypes.colColor', 'Renk'), key: 'color', sortable: false },
          { title: mt('taskManager.issueTypes.colActions', 'İşlemler'), key: 'actions', sortable: false, align: 'end' },
        ]"
        :items="sortedTypes"
        :loading="loading"
        class="tm-issue-types-table"
      >
        <template #[`item.name`]="{ item }">
          <span class="font-weight-medium">{{ item.name }}</span>
        </template>
        <template #[`item.description`]="{ item }">
          <span class="text-body-2 text-medium-emphasis text-truncate d-inline-block" style="max-width: 280px" :title="item.description || ''">
            {{ item.description || '—' }}
          </span>
        </template>
        <template #[`item.icon`]="{ item }">
          <div v-if="item.icon" class="d-flex align-center gap-2">
            <component
              v-if="typeIconEl(item.icon)"
              :is="typeIconEl(item.icon)"
              :size="20"
              class="flex-shrink-0 tm-issue-type-icon-cell"
            />
            <v-icon v-else icon="mdi-help-circle-outline" size="20" class="text-medium-emphasis flex-shrink-0" />
            <code class="text-caption text-medium-emphasis text-truncate" style="max-width: 160px">{{ item.icon }}</code>
          </div>
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <template #[`item.color`]="{ item }">
          <v-chip
            v-if="item.color && isTmStatusThemeColor(item.color)"
            :color="item.color"
            size="small"
            variant="tonal"
            rounded="lg"
            class="text-none"
          >
            {{ item.color }}
          </v-chip>
          <div v-else-if="item.color && isLegacyHexStatusColor(item.color)" class="d-flex align-center gap-2 flex-wrap">
            <div
              class="tm-swatch rounded-lg flex-shrink-0"
              :style="{ backgroundColor: item.color, boxShadow: 'inset 0 0 0 1px rgba(0,0,0,.12)' }"
            />
            <span class="text-caption text-medium-emphasis">{{ item.color }}</span>
            <v-chip size="x-small" variant="outlined" density="compact">{{ mt('taskManager.statuses.legacyHexTag', 'Eski #hex') }}</v-chip>
          </div>
          <span v-else-if="item.color" class="text-caption text-medium-emphasis">{{ item.color }}</span>
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <template #[`item.actions`]="{ item }">
          <v-btn icon variant="text" size="small" @click="openEdit(item)">
            <v-icon icon="mdi-pencil-outline" />
          </v-btn>
          <v-btn icon variant="text" size="small" color="error" @click="openDelete(item)">
            <v-icon icon="mdi-delete-outline" />
          </v-btn>
        </template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="dialog" max-width="520">
      <v-card rounded="xl">
        <v-card-title class="text-h6">
          {{ editId ? mt('taskManager.issueTypes.editType', 'Tipi düzenle') : mt('taskManager.issueTypes.newType', 'Yeni tip') }}
        </v-card-title>
        <v-card-text>
          <v-alert v-if="legacyColorWarning" type="info" variant="tonal" density="compact" class="mb-4">
            {{ mt('taskManager.statuses.legacyColorHint', 'Kayıtta tema anahtarı dışı renk var; kaydedince seçtiğiniz tema rengine güncellenir.') }}
          </v-alert>
          <v-text-field v-model="form.name" :label="mt('taskManager.issueTypes.fieldName', 'Tip adı')" density="comfortable" required />
          <v-textarea
            v-model="form.description"
            class="mt-3"
            :label="mt('taskManager.issueTypes.fieldDescription', 'Açıklama')"
            :hint="mt('taskManager.issueTypes.fieldDescriptionHint', 'İsteğe bağlı.')"
            rows="2"
            auto-grow
            density="comfortable"
            variant="outlined"
          />
          <TmStatusIconPicker
            v-model="form.icon"
            class="mt-3"
            :label="mt('taskManager.issueTypes.fieldIcon', 'İkon')"
            :hint="mt('taskManager.statuses.iconHint', 'Listeden seçin; Tabler bileşen adı saklanır.')"
            :search-placeholder="mt('taskManager.statuses.iconSearch', 'İkon ara…')"
            :menu-title="mt('taskManager.statuses.iconMenuTitle', 'İkon seç')"
            :clear-label="mt('taskManager.statuses.iconClear', 'İkonu kaldır')"
            :no-results="mt('taskManager.statuses.iconNoMatch', 'Eşleşen ikon yok')"
          />
          <v-select
            v-model="form.color"
            class="mt-3"
            :items="themeColorItems"
            item-title="title"
            item-value="value"
            clearable
            :label="mt('taskManager.issueTypes.fieldColor', 'Tema rengi')"
            :hint="mt('taskManager.statuses.colorHint', 'primary, info, warning, success vb.')"
            persistent-hint
            density="comfortable"
          />
        </v-card-text>
        <v-card-actions class="px-6 pb-4">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="primary" rounded="lg" :loading="saving" :disabled="!form.name.trim()" @click="submitForm">
            {{ mt('taskManager.save', 'Kaydet') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.issueTypes.deleteTitle', 'Tip silinsin mi?') }}</v-card-title>
        <v-card-text>
          {{ mt('taskManager.issueTypes.deleteBody', 'Bu tipe bağlı görev varsa silme başarısız olabilir.') }}
          <div v-if="deleteTarget" class="mt-2 font-weight-medium">{{ deleteTarget.name }}</div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="confirmDelete">{{ mt('taskManager.delete', 'Sil') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.tm-swatch {
  width: 22px;
  height: 22px;
}
.tm-issue-type-icon-cell {
  color: rgba(var(--v-theme-on-surface), 0.75);
}
</style>
