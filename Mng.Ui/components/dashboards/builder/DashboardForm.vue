<script setup lang="ts">
/** Sol panel: Temel bilgiler formu (name, title, description, slug, isDefault, isActive, permissions) */
import { computed } from 'vue';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import type { DashboardFormData } from './types';

const props = defineProps<{
  modelValue: DashboardFormData;
  disabled?: boolean;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: DashboardFormData];
}>();

// Selected groups for view permissions
const viewGroups = computed({
  get: () => {
    return props.modelValue.permissions?.view?.groups || [];
  },
  set: (value: string[]) => {
    const permissions = props.modelValue.permissions || {};
    if (value.length > 0) {
      permissions.view = { groups: value };
    } else {
      permissions.view = undefined;
    }
    emit('update:modelValue', { ...props.modelValue, permissions });
  },
});

// Selected groups for edit permissions
const editGroups = computed({
  get: () => {
    return props.modelValue.permissions?.edit?.groups || [];
  },
  set: (value: string[]) => {
    const permissions = props.modelValue.permissions || {};
    if (value.length > 0) {
      permissions.edit = { groups: value };
    } else {
      permissions.edit = undefined;
    }
    emit('update:modelValue', { ...props.modelValue, permissions });
  },
});

const form = computed({
  get: () => props.modelValue,
  set: (v: DashboardFormData) => emit('update:modelValue', v),
});

function update<K extends keyof DashboardFormData>(key: K, value: DashboardFormData[K]) {
  emit('update:modelValue', { ...props.modelValue, [key]: value });
}

const lbl = (key: string) => props.t?.(`dashboards.builder.form.${key}`) ?? key;
</script>

<template>
  <div class="dashboard-form">
    <v-card variant="outlined" class="mb-4">
      <v-card-title class="text-subtitle-1 font-weight-medium">
        {{ t?.('dashboards.builder.form.sectionTitle') ?? 'Temel bilgiler' }}
      </v-card-title>
      <v-card-text>
        <v-text-field
          :model-value="form.name"
          :label="lbl('name')"
          :disabled="disabled"
          variant="outlined"
          density="compact"
          hide-details="auto"
          class="mb-3"
          @update:model-value="(v) => update('name', v ?? '')"
        />
        <v-text-field
          :model-value="form.title"
          :label="lbl('title')"
          :disabled="disabled"
          variant="outlined"
          density="compact"
          hide-details="auto"
          class="mb-3"
          @update:model-value="(v) => update('title', v ?? '')"
        />
        <v-textarea
          :model-value="form.description"
          :label="lbl('description')"
          :disabled="disabled"
          variant="outlined"
          density="compact"
          hide-details="auto"
          rows="2"
          class="mb-3"
          @update:model-value="(v) => update('description', v ?? '')"
        />
        <v-text-field
          :model-value="form.slug"
          :label="lbl('slug')"
          :hint="t?.('dashboards.builder.form.slugHint') ?? 'Boş bırakılırsa name kullanılır'"
          :disabled="disabled"
          variant="outlined"
          density="compact"
          persistent-hint
          hide-details="auto"
          class="mb-3"
          @update:model-value="(v) => update('slug', v ?? '')"
        />
        <v-checkbox
          :model-value="form.isDefault"
          :label="lbl('isDefault')"
          :disabled="disabled"
          hide-details
          density="compact"
          color="primary"
          @update:model-value="(v) => update('isDefault', !!v)"
        />
        <v-checkbox
          :model-value="form.isActive"
          :label="lbl('isActive')"
          :disabled="disabled"
          hide-details
          density="compact"
          color="primary"
          @update:model-value="(v) => update('isActive', !!v)"
        />
      </v-card-text>
    </v-card>

    <!-- Permissions -->
    <v-card variant="outlined">
      <v-card-title class="text-subtitle-1 font-weight-medium">
        <v-icon class="mr-2">mdi-shield-account</v-icon>
        {{ t?.('dashboards.builder.form.permissions') || 'Yetkilendirme' }}
      </v-card-title>
      <v-card-text>
        <v-alert type="info" variant="tonal" density="compact" class="mb-4">
          {{ t?.('dashboards.builder.form.permissionsHint') || 'Dashboard\'ı görüntüleyebilecek ve düzenleyebilecek grupları seçin. Hiçbir grup seçilmezse, tüm kullanıcılar erişebilir. Admin kullanıcılar her zaman tüm dashboard\'ları görebilir ve düzenleyebilir.' }}
        </v-alert>

        <!-- View Permissions -->
        <div class="mb-4">
          <div class="text-caption text-medium-emphasis mb-2">
            {{ t?.('dashboards.builder.form.viewPermissions') || 'Görüntüleme Yetkisi' }}
          </div>
          <MngDirectoryPickerField
            v-model="viewGroups"
            entity="group"
            group-value-key="name"
            multiple
            :label="t?.('dashboards.builder.form.viewGroups') || 'Görüntüleme Grupları'"
            density="compact"
            :disabled="disabled"
          />
        </div>

        <!-- Edit Permissions -->
        <div>
          <div class="text-caption text-medium-emphasis mb-2">
            {{ t?.('dashboards.builder.form.editPermissions') || 'Düzenleme Yetkisi' }}
          </div>
          <MngDirectoryPickerField
            v-model="editGroups"
            entity="group"
            group-value-key="name"
            multiple
            :label="t?.('dashboards.builder.form.editGroups') || 'Düzenleme Grupları'"
            density="compact"
            :disabled="disabled"
          />
        </div>
      </v-card-text>
    </v-card>
  </div>
</template>
