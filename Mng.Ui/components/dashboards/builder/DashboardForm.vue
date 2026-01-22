<script setup lang="ts">
/** Sol panel: Temel bilgiler formu (name, title, description, slug, isDefault, isActive) */
import type { DashboardFormData } from './types';

const props = defineProps<{
  modelValue: DashboardFormData;
  disabled?: boolean;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: DashboardFormData];
}>();

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
  </div>
</template>
