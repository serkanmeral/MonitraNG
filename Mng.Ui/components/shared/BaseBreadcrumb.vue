<script setup lang="ts">
import { computed } from 'vue';

/** Eski tema `text` alanı + Vuetify 3 `title` beklenir */
const props = defineProps<{
  title?: string;
  breadcrumbs?: Array<Record<string, unknown>>;
  icon?: string;
}>();

const breadcrumbItems = computed(() => {
  const raw = props.breadcrumbs;
  if (!raw || !Array.isArray(raw)) return [];
  return raw.map((b) => ({
    title: String(b.title ?? b.text ?? ''),
    disabled: Boolean(b.disabled),
    href: typeof b.href === 'string' ? b.href : undefined,
  }));
});
</script>

<template>
  <div class="mt-3 mb-6">
    <div class="d-flex justify-space-between">
      <div class="d-flex py-0 align-center">
        <div>
          <h3 class="text-h3 mb-2">{{ title }}</h3>
          <v-breadcrumbs v-if="breadcrumbItems.length" :items="breadcrumbItems" class="text-h6 font-weight-regular pa-0 ml-n1">
            <template #divider>
              <v-icon>mdi-chevron-right</v-icon>
            </template>
          </v-breadcrumbs>
        </div>
      </div>
    </div>
  </div>
</template>

<style lang="scss">
.page-breadcrumb {
  .v-toolbar {
    background: transparent;
  }
}
</style>
