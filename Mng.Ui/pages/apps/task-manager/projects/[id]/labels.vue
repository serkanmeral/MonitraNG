<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useAuthStore } from '@/stores/auth';
import type { TmLabel } from '@/types/apps/taskManager';

definePageMeta({ layout: 'default' });

const route = useRoute();
const projectId = computed(() => String(route.params.id ?? ''));

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
const auth = useAuthStore();
const canManage = computed(() => auth.isManager);

const project = computed(() => store.projects.find((p) => p.__dataId === projectId.value));
const labels = computed(() =>
  [...store.labels].filter((l) => l.projectId === projectId.value).sort((a, b) => a.name.localeCompare(b.name, 'tr'))
);

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);

const dialog = ref(false);
const editId = ref<string | null>(null);
const form = ref({ name: '', color: '#5eead4' });

const deleteDialog = ref(false);
const deleteTarget = ref<TmLabel | null>(null);

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: mt('taskManager.projectsListTitle', 'Projeler'), disabled: false, href: '/apps/task-manager/projects' },
  {
    text: project.value?.name ?? '…',
    disabled: false,
    href: project.value ? `/apps/task-manager/projects/${project.value.__dataId}` : '#',
  },
  { text: mt('taskManager.projectLabelsTitle', 'Etiketler'), disabled: true, href: '#' },
]);

function openCreate() {
  editId.value = null;
  form.value = { name: '', color: '#5eead4' };
  errorLocal.value = null;
  dialog.value = true;
}

function openEdit(l: TmLabel) {
  editId.value = l.__dataId;
  form.value = { name: l.name, color: (l.color ?? '#5eead4').trim() || '#5eead4' };
  errorLocal.value = null;
  dialog.value = true;
}

function openDelete(l: TmLabel) {
  deleteTarget.value = l;
  deleteDialog.value = true;
}

onMounted(async () => {
  loading.value = true;
  errorLocal.value = null;
  try {
    await store.loadLookups();
    await store.loadProjects();
    if (projectId.value) await store.loadLabels(projectId.value);
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
});

async function submitForm() {
  if (!canManage.value || !projectId.value || !form.value.name.trim()) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    if (editId.value) {
      await store.updateLabel(editId.value, projectId.value, {
        name: form.value.name.trim(),
        color: form.value.color?.trim() || null,
      });
    } else {
      await store.createLabel(projectId.value, form.value.name.trim(), form.value.color?.trim() || undefined);
    }
    dialog.value = false;
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!canManage.value || !deleteTarget.value || !projectId.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await store.deleteLabel(deleteTarget.value.__dataId, projectId.value);
    deleteDialog.value = false;
    deleteTarget.value = null;
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb
      :title="mt('taskManager.projectLabelsTitle', 'Etiketler')"
      :breadcrumbs="breadcrumbs"
    />

    <v-alert v-if="!project" type="warning" variant="tonal" class="mb-4">
      {{ mt('taskManager.projectNotFound', 'Proje bulunamadı.') }}
    </v-alert>

    <template v-else>
      <p class="text-body-2 text-medium-emphasis mb-4">
        {{ mt('taskManager.projectLabelsIntro', 'Bu projeye özel etiketler. Görevlerde yalnızca burada tanımlanan etiketler seçilebilir.') }}
      </p>

      <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
        {{ errorLocal }}
      </v-alert>

      <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
        <h2 class="text-h6 font-weight-bold mb-0">{{ mt('taskManager.projectLabelsListTitle', 'Etiket listesi') }}</h2>
        <v-btn v-if="canManage" color="primary" rounded="lg" class="text-none" @click="openCreate">
          {{ mt('taskManager.projectLabelsNew', 'Yeni etiket') }}
        </v-btn>
      </div>

      <v-skeleton-loader v-if="loading" type="table" />
      <v-card v-else class="tm-panel pa-0 overflow-hidden" rounded="xl" flat>
        <v-table density="comfortable">
          <thead>
            <tr>
              <th class="text-left">{{ mt('taskManager.labelName', 'Etiket adı') }}</th>
              <th class="text-left">{{ mt('taskManager.projectLabelsColor', 'Renk') }}</th>
              <th v-if="canManage" class="text-end" style="width: 160px">{{ mt('taskManager.tableColumnActions', 'İşlem') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="l in labels" :key="l.__dataId">
              <td>
                <v-chip size="small" :color="l.color || undefined" variant="flat" class="font-weight-medium">
                  {{ l.name }}
                </v-chip>
              </td>
              <td>
                <span class="text-caption font-mono">{{ l.color || '—' }}</span>
              </td>
              <td v-if="canManage" class="text-end">
                <v-btn size="small" variant="text" class="text-none" @click="openEdit(l)">
                  {{ mt('taskManager.projectLabelsEditAction', 'Düzenle') }}
                </v-btn>
                <v-btn size="small" variant="text" color="error" class="text-none" @click="openDelete(l)">
                  {{ mt('taskManager.delete', 'Sil') }}
                </v-btn>
              </td>
            </tr>
            <tr v-if="!labels.length">
              <td :colspan="canManage ? 3 : 2" class="text-medium-emphasis text-body-2">
                {{ mt('taskManager.projectLabelsEmpty', 'Henüz etiket yok.') }}
              </td>
            </tr>
          </tbody>
        </v-table>
      </v-card>

      <v-alert v-if="!canManage" type="info" variant="tonal" density="comfortable" class="mt-4">
        {{ mt('taskManager.projectLabelsReadOnlyHint', 'Etiket eklemek veya düzenlemek için yönetici rolü gerekir.') }}
      </v-alert>
    </template>

    <v-dialog v-model="dialog" max-width="480">
      <v-card rounded="xl">
        <v-card-title>
          {{
            editId
              ? mt('taskManager.projectLabelsEditTitle', 'Etiketi düzenle')
              : mt('taskManager.projectLabelsNew', 'Yeni etiket')
          }}
        </v-card-title>
        <v-card-text>
          <v-text-field
            v-model="form.name"
            :label="mt('taskManager.labelName', 'Etiket adı')"
            density="comfortable"
            variant="outlined"
            hide-details="auto"
            class="mb-3"
          />
          <v-text-field
            v-model="form.color"
            :label="mt('taskManager.projectLabelsColor', 'Renk (#hex)')"
            density="comfortable"
            variant="outlined"
            hide-details="auto"
            placeholder="#5eead4"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="!form.name.trim()" @click="submitForm">
            {{ mt('taskManager.save', 'Kaydet') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.projectLabelsDeleteTitle', 'Etiket silinsin mi?') }}</v-card-title>
        <v-card-text>
          {{ mt('taskManager.projectLabelsDeleteBody', 'Görevlerde seçili olsa bile etiket kaydı kaldırılır; görev alanını kontrol edin.') }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="confirmDelete">{{ mt('taskManager.delete', 'Sil') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
