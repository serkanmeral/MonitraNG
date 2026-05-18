<script setup lang="ts">
import { computed, ref } from 'vue';
import { TM_STATUS_TABLER_ICON_ENTRIES, getTmStatusTablerIconComponent } from '@/utils/tmStatusTablerIcons';

const props = defineProps<{
  modelValue: string;
  label: string;
  hint?: string;
  searchPlaceholder?: string;
  menuTitle?: string;
  clearLabel?: string;
  /** Arama sonucu boşken gösterilecek kısa metin */
  noResults?: string;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', v: string): void;
}>();

const menu = ref(false);
const search = ref('');

const currentComp = computed(() => getTmStatusTablerIconComponent(props.modelValue));

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase();
  if (!q) return [...TM_STATUS_TABLER_ICON_ENTRIES];
  return TM_STATUS_TABLER_ICON_ENTRIES.filter((e) => e.name.toLowerCase().includes(q));
});

function pick(name: string) {
  emit('update:modelValue', name);
  menu.value = false;
  search.value = '';
}

function clearIcon() {
  emit('update:modelValue', '');
}
</script>

<template>
  <div class="tm-icon-picker">
    <div class="text-caption text-medium-emphasis mb-1">{{ label }}</div>
    <div class="d-flex align-center gap-2 flex-wrap">
      <div
        class="tm-icon-picker-preview d-flex align-center justify-center rounded-lg flex-shrink-0"
        :class="{ 'tm-icon-picker-preview--empty': !modelValue }"
      >
        <component :is="currentComp" v-if="currentComp" :size="26" class="tm-icon-picker-preview-svg" />
        <v-icon v-else icon="mdi-help-circle-outline" size="26" class="text-medium-emphasis" />
      </div>
      <v-text-field
        :model-value="modelValue"
        placeholder="—"
        density="comfortable"
        variant="outlined"
        hide-details="auto"
        readonly
        class="flex-grow-1"
        style="min-width: 160px"
      />
      <v-menu v-model="menu" :close-on-content-click="false" location="bottom">
        <template #activator="{ props: menuProps }">
          <v-btn v-bind="menuProps" variant="tonal" rounded="lg" class="text-none flex-shrink-0">
            <v-icon icon="mdi-apps" start size="small" />
            {{ menuTitle }}
          </v-btn>
        </template>
        <v-card rounded="xl" min-width="min(100vw - 32px, 400px)" max-width="400" class="pa-3">
          <div class="text-subtitle-2 mb-2">{{ menuTitle }}</div>
          <v-text-field
            v-model="search"
            :placeholder="searchPlaceholder"
            prepend-inner-icon="mdi-magnify"
            density="compact"
            variant="outlined"
            hide-details
            class="mb-3"
            clearable
            @click:clear="search = ''"
          />
          <div class="tm-icon-picker-grid">
            <v-tooltip v-for="entry in filtered" :key="entry.name" location="top" :text="entry.name">
              <template #activator="{ props: tipProps }">
                <v-btn
                  v-bind="tipProps"
                  :variant="modelValue === entry.name ? 'flat' : 'tonal'"
                  :color="modelValue === entry.name ? 'primary' : undefined"
                  icon
                  size="small"
                  class="tm-icon-picker-cell"
                  @click="pick(entry.name)"
                >
                  <component :is="entry.component" :size="22" />
                </v-btn>
              </template>
            </v-tooltip>
          </div>
          <div v-if="!filtered.length" class="text-caption text-medium-emphasis py-4 text-center">
            {{ noResults || '—' }}
          </div>
          <v-btn block variant="text" size="small" class="mt-2 text-none" :disabled="!modelValue" @click="clearIcon">
            {{ clearLabel }}
          </v-btn>
        </v-card>
      </v-menu>
    </div>
    <div v-if="hint" class="text-caption text-medium-emphasis mt-1">{{ hint }}</div>
  </div>
</template>

<style scoped>
.tm-icon-picker-preview {
  width: 44px;
  height: 44px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgba(var(--v-theme-surface-variant), 0.35);
}
.tm-icon-picker-preview--empty {
  opacity: 0.85;
}
.tm-icon-picker-preview-svg {
  color: rgb(var(--v-theme-on-surface));
  opacity: 0.85;
}
.tm-icon-picker-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  max-height: 240px;
  overflow-y: auto;
  padding: 2px;
}
.tm-icon-picker-cell {
  flex-shrink: 0;
}
</style>
