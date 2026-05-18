<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useAuthStore } from '@/stores/auth';
import type { TmFieldDefinition } from '@/types/apps/taskManager';
import {
  TM_FIELD_KEY_PATTERN,
  TM_POOL_FIELD_TYPE_VALUES,
  effectiveFieldCardinality,
} from '@/utils/taskManagerFieldDefinitions';

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
const auth = useAuthStore();
const canManage = computed(() => auth.isManager);

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);

const dialog = ref(false);
const editId = ref<string | null>(null);
const form = ref({
  key: '',
  label: '',
  fieldType: 'text',
  scope: 'pool' as 'core' | 'pool',
  description: '',
  sortOrder: '',
  cardinality: 'single' as 'single' | 'multi',
  optionsJson: '',
});

const deleteDialog = ref(false);
const deleteTarget = ref<TmFieldDefinition | null>(null);

const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('taskManager.pageTitle', 'Görevler'), disabled: false, href: '/apps/task-manager' },
  { text: mt('taskManager.fieldPool.title', 'Alan havuzu'), disabled: true, href: '#' },
]);

const rows = computed(() => store.sortedFieldDefinitions);

const tableHeaders = computed(() => {
  const base = [
    { title: mt('taskManager.fieldPool.colKey', 'Anahtar'), key: 'key', sortable: true },
    { title: mt('taskManager.fieldPool.colLabel', 'Görünen ad'), key: 'label', sortable: true },
    { title: mt('taskManager.fieldPool.colType', 'Veri tipi'), key: 'fieldType', sortable: true },
    { title: mt('taskManager.fieldPool.colCardinality', 'Seçim'), key: 'cardinality', sortable: true },
    { title: mt('taskManager.fieldPool.colScope', 'Kapsam'), key: 'scope', sortable: true },
    { title: mt('taskManager.fieldPool.colOptions', 'Seçenekler (JSON)'), key: 'optionsJson', sortable: false },
    { title: mt('taskManager.fieldPool.colDescription', 'Açıklama'), key: 'description', sortable: false },
  ];
  if (canManage.value) {
    base.push({
      title: mt('taskManager.fieldPool.colActions', 'İşlemler'),
      key: 'actions',
      sortable: false,
    });
  }
  return base;
});

const fieldTypeItems = computed(() =>
  [...TM_POOL_FIELD_TYPE_VALUES].map((v) => ({ title: v, value: v }))
);

const scopeItems = computed(() => [
  { title: mt('taskManager.fieldPool.scopeCore', 'Temel (tüm projeler)'), value: 'core' },
  { title: mt('taskManager.fieldPool.scopePool', 'Havuz (projede seçilebilir)'), value: 'pool' },
]);

const cardinalityItems = computed(() => [
  { title: mt('taskManager.fieldPool.cardinalitySingle', 'Tek'), value: 'single' },
  { title: mt('taskManager.fieldPool.cardinalityMulti', 'Çoklu'), value: 'multi' },
]);

function scopeLabel(scope: string): string {
  const s = (scope || '').toLowerCase();
  if (s === 'core') return mt('taskManager.fieldPool.scopeCore', 'Temel (tüm projeler)');
  if (s === 'pool') return mt('taskManager.fieldPool.scopePool', 'Havuz (projede seçilebilir)');
  return scope;
}

function scopeColor(scope: string): string {
  const s = (scope || '').toLowerCase();
  if (s === 'core') return 'primary';
  if (s === 'pool') return 'secondary';
  return 'default';
}

function cardinalityLabel(fd: TmFieldDefinition): string {
  const c = effectiveFieldCardinality(fd);
  return c === 'multi'
    ? mt('taskManager.fieldPool.cardinalityMulti', 'Çoklu')
    : mt('taskManager.fieldPool.cardinalitySingle', 'Tek');
}

const formValid = computed(() => {
  if (!form.value.label.trim() || !form.value.fieldType.trim()) return false;
  if (!editId.value) {
    const k = form.value.key.trim();
    if (!k || !TM_FIELD_KEY_PATTERN.test(k)) return false;
  }
  return true;
});

function openCreate() {
  editId.value = null;
  form.value = {
    key: '',
    label: '',
    fieldType: 'text',
    scope: 'pool',
    description: '',
    sortOrder: '',
    cardinality: 'single',
    optionsJson: '',
  };
  errorLocal.value = null;
  dialog.value = true;
}

function openEdit(fd: TmFieldDefinition) {
  editId.value = fd.__dataId;
  form.value = {
    key: fd.key,
    label: fd.label,
    fieldType: fd.fieldType || 'text',
    scope: fd.scope === 'core' ? 'core' : 'pool',
    description: fd.description ?? '',
    sortOrder: fd.sortOrder != null && !Number.isNaN(Number(fd.sortOrder)) ? String(fd.sortOrder) : '',
    cardinality: effectiveFieldCardinality(fd),
    optionsJson: fd.optionsJson?.trim() ? fd.optionsJson : '',
  };
  errorLocal.value = null;
  dialog.value = true;
}

function openDelete(fd: TmFieldDefinition) {
  deleteTarget.value = fd;
  deleteDialog.value = true;
}

async function submitForm() {
  if (!formValid.value) return;
  const oj = form.value.optionsJson.trim();
  if (oj) {
    try {
      JSON.parse(oj);
    } catch {
      errorLocal.value = mt('taskManager.fieldPool.invalidOptionsJson', 'Seçenekler geçerli bir JSON olmalıdır.');
      return;
    }
  }

  saving.value = true;
  errorLocal.value = null;
  try {
    const sortNum = form.value.sortOrder.trim() === '' ? null : Number(form.value.sortOrder);
    const sortOrder = sortNum != null && !Number.isNaN(sortNum) ? sortNum : null;

    if (editId.value) {
      await store.updateFieldDefinition(editId.value, {
        label: form.value.label,
        fieldType: form.value.fieldType,
        scope: form.value.scope,
        description: form.value.description.trim() || null,
        sortOrder,
        cardinality: form.value.cardinality,
        optionsJson: form.value.optionsJson.trim() || null,
      });
    } else {
      await store.createFieldDefinition({
        key: form.value.key.trim(),
        label: form.value.label,
        fieldType: form.value.fieldType,
        scope: form.value.scope,
        description: form.value.description.trim() || null,
        sortOrder,
        cardinality: form.value.cardinality,
        optionsJson: form.value.optionsJson.trim() || null,
      });
    }
    dialog.value = false;
  } catch (e: any) {
    errorLocal.value = e?.message ?? mt('taskManager.fieldPool.saveError', 'Kaydedilemedi.');
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await store.deleteFieldDefinition(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
  } catch (e: any) {
    errorLocal.value = e?.message ?? mt('taskManager.fieldPool.deleteError', 'Silinemedi.');
    deleteDialog.value = false;
  } finally {
    deleting.value = false;
  }
}

onMounted(async () => {
  loading.value = true;
  errorLocal.value = null;
  try {
    await store.loadFieldDefinitions();
  } catch (e: any) {
    errorLocal.value =
      e?.message ?? mt('taskManager.fieldPool.loadError', 'Alan tanımları yüklenemedi. Dataset ve setup script kontrol edin.');
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <div class="tm-flow">
    <BaseBreadcrumb :title="mt('taskManager.fieldPool.title', 'Alan havuzu')" :breadcrumbs="breadcrumbs" />

    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <div class="tm-hero d-flex flex-column flex-md-row flex-wrap align-start align-md-center justify-space-between gap-4 mb-6">
      <div>
        <h1 class="tm-hero-title text-h4">{{ mt('taskManager.fieldPool.title', 'Alan havuzu') }}</h1>
        <p class="tm-hero-sub mb-0">
          {{
            canManage
              ? mt(
                  'taskManager.fieldPool.subtitleCrud',
                  'tm_issues ile uyumlu alan meta tanımları. Yöneticiler kayıt ekleyebilir, düzenleyebilir ve silebilir; anahtar düzenlemede değişmez.'
                )
              : mt(
                  'taskManager.fieldPool.subtitle',
                  'tm_issues şemasına karşılık gelen tanımlar. Proje seçimleri ve formlar bu havuzdan beslenir.'
                )
          }}
        </p>
      </div>
      <div class="d-flex flex-wrap gap-2">
        <v-btn variant="tonal" size="large" rounded="lg" class="text-none" to="/apps/task-manager/priorities">
          <v-icon icon="mdi-priority-high" start />
          {{ mt('taskManager.priorities.title', 'Öncelikler') }}
        </v-btn>
        <v-btn variant="tonal" size="large" rounded="lg" class="text-none" to="/apps/task-manager">
          <v-icon icon="mdi-arrow-left" start />
          {{ mt('taskManager.fieldPool.backToTasks', 'Görevlere dön') }}
        </v-btn>
        <v-btn v-if="canManage" color="primary" size="large" rounded="lg" class="text-none" @click="openCreate">
          <v-icon icon="mdi-plus" start />
          {{ mt('taskManager.fieldPool.newField', 'Yeni alan') }}
        </v-btn>
      </div>
    </div>

    <v-card class="tm-panel" rounded="xl" flat>
      <v-data-table
        :headers="tableHeaders"
        :items="rows"
        :loading="loading"
        density="comfortable"
        class="tm-field-pool-table"
      >
        <template #[`item.key`]="{ item }">
          <code class="text-body-2">{{ item.key }}</code>
        </template>
        <template #[`item.label`]="{ item }">
          <span class="font-weight-medium">{{ item.label }}</span>
        </template>
        <template #[`item.fieldType`]="{ item }">
          <v-chip size="small" variant="tonal" rounded="lg">{{ item.fieldType }}</v-chip>
        </template>
        <template #[`item.cardinality`]="{ item }">
          <v-chip size="small" variant="outlined" rounded="lg">{{ cardinalityLabel(item) }}</v-chip>
        </template>
        <template #[`item.scope`]="{ item }">
          <v-chip :color="scopeColor(item.scope)" size="small" variant="tonal" rounded="lg">
            {{ scopeLabel(item.scope) }}
          </v-chip>
        </template>
        <template #[`item.optionsJson`]="{ item }">
          <span
            class="text-body-2 text-medium-emphasis text-truncate d-inline-block"
            style="max-width: 220px; font-family: ui-monospace, monospace"
            :title="item.optionsJson || ''"
          >
            {{ item.optionsJson?.trim() ? item.optionsJson : '—' }}
          </span>
        </template>
        <template #[`item.description`]="{ item }">
          <span class="text-body-2 text-medium-emphasis text-truncate d-inline-block" style="max-width: 360px" :title="item.description || ''">
            {{ item.description || '—' }}
          </span>
        </template>
        <template v-if="canManage" #[`item.actions`]="{ item }">
          <v-btn icon variant="text" size="small" @click="openEdit(item)">
            <v-icon icon="mdi-pencil-outline" />
          </v-btn>
          <v-btn icon variant="text" size="small" color="error" @click="openDelete(item)">
            <v-icon icon="mdi-delete-outline" />
          </v-btn>
        </template>
      </v-data-table>
    </v-card>

    <v-alert v-if="!loading && !errorLocal && rows.length === 0" type="info" variant="tonal" class="mt-4" rounded="lg">
      {{ mt('taskManager.fieldPool.empty', 'Kayıt yok. setup-task-manager-datasets.ps1 ile tm_field_definitions seed çalıştırın.') }}
    </v-alert>

    <v-dialog v-model="dialog" max-width="560">
      <v-card rounded="xl">
        <v-card-title class="text-h6">
          {{ editId ? mt('taskManager.fieldPool.editField', 'Alanı düzenle') : mt('taskManager.fieldPool.newField', 'Yeni alan') }}
        </v-card-title>
        <v-card-text>
          <v-text-field
            v-model="form.key"
            :disabled="!!editId"
            :label="mt('taskManager.fieldPool.fieldKey', 'Alan anahtarı (tm_issues)')"
            :hint="mt('taskManager.fieldPool.fieldKeyHint', 'Örn. customField_1 — oluştururken benzersiz; düzenlemede değişmez.')"
            density="comfortable"
            variant="outlined"
            persistent-hint
            class="mb-2"
          />
          <v-text-field
            v-model="form.label"
            :label="mt('taskManager.fieldPool.fieldLabel', 'Görünen ad')"
            density="comfortable"
            variant="outlined"
            required
          />
          <v-select
            v-model="form.fieldType"
            class="mt-3"
            :items="fieldTypeItems"
            item-title="title"
            item-value="value"
            :label="mt('taskManager.fieldPool.fieldType', 'Veri tipi')"
            density="comfortable"
            variant="outlined"
          />
          <v-select
            v-model="form.scope"
            class="mt-3"
            :items="scopeItems"
            item-title="title"
            item-value="value"
            :label="mt('taskManager.fieldPool.fieldScope', 'Kapsam')"
            density="comfortable"
            variant="outlined"
          />
          <v-select
            v-model="form.cardinality"
            class="mt-3"
            :items="cardinalityItems"
            item-title="title"
            item-value="value"
            :label="mt('taskManager.fieldPool.fieldCardinality', 'Seçim')"
            :hint="mt('taskManager.fieldPool.fieldCardinalityHint', 'Kişi, grup veya etiket gibi alanlarda çoklu seçim için multi.')"
            density="comfortable"
            variant="outlined"
            persistent-hint
          />
          <v-text-field
            v-model="form.sortOrder"
            class="mt-3"
            type="number"
            :label="mt('taskManager.fieldPool.fieldSortOrder', 'Liste sırası')"
            :hint="mt('taskManager.fieldPool.fieldSortOrderHint', 'Boş bırakılabilir; küçük önce gelir.')"
            density="comfortable"
            variant="outlined"
            persistent-hint
            clearable
          />
          <v-textarea
            v-model="form.optionsJson"
            class="mt-3"
            :label="mt('taskManager.fieldPool.fieldOptionsJson', 'Seçenekler (JSON)')"
            :hint="mt('taskManager.fieldPool.fieldOptionsJsonHint')"
            rows="3"
            auto-grow
            density="comfortable"
            variant="outlined"
            persistent-hint
          />
          <v-textarea
            v-model="form.description"
            class="mt-3"
            :label="mt('taskManager.fieldPool.fieldDescription', 'Açıklama')"
            rows="2"
            auto-grow
            density="comfortable"
            variant="outlined"
          />
        </v-card-text>
        <v-card-actions class="px-6 pb-4">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="primary" rounded="lg" :loading="saving" :disabled="!formValid" @click="submitForm">
            {{ mt('taskManager.save', 'Kaydet') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="460">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.fieldPool.deleteTitle', 'Alan silinsin mi?') }}</v-card-title>
        <v-card-text>
          <v-alert
            v-if="deleteTarget && String(deleteTarget.scope).toLowerCase() === 'core'"
            type="warning"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ mt('taskManager.fieldPool.deleteBodyCore', 'Bu temel (core) bir alan; silmek mevcut görevlerde veya projelerde beklenmeyen sonuçlara yol açabilir.') }}
          </v-alert>
          <span v-else>{{ mt('taskManager.fieldPool.deleteBody', 'Bu alan tanımı silinecek. Görevlerde extraFields ile kullanılıyorsa veri tutarlılığını kontrol edin.') }}</span>
          <div v-if="deleteTarget" class="mt-2">
            <code class="text-body-2">{{ deleteTarget.key }}</code>
            <span class="mx-1">—</span>
            <span class="font-weight-medium">{{ deleteTarget.label }}</span>
          </div>
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
