<script setup lang="ts">
import { computed } from 'vue';
import DiMarkdownEditor from '@/components/apps/document-intelligence/DiMarkdownEditor.vue';
import { useAppI18n } from '@/composables/useAppI18n';

type DocSource = 'write' | 'existing';

const props = defineProps<{
  source: DocSource;
  body: string;
  resourceId: string;
  resourceName: string;
  writeHint: string;
  loading?: boolean;
  emptyLabel?: string;
}>();

const emit = defineEmits<{
  'update:source': [DocSource];
  'update:body': [string];
  pick: [];
  clear: [];
}>();

const { t } = useAppI18n();

const sourceItems = computed(() => [
  { title: t('projectManagement.decision.sourceWrite'), value: 'write' as const },
  { title: t('projectManagement.decision.sourceExisting'), value: 'existing' as const },
]);

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function onSource(value: unknown) {
  emit('update:source', value === 'existing' ? 'existing' : 'write');
}
</script>

<template>
  <div class="d-flex flex-column ga-2">
    <v-btn-toggle
      :model-value="source"
      mandatory
      density="compact"
      color="primary"
      divided
      @update:model-value="onSource"
    >
      <v-btn v-for="item in sourceItems" :key="item.value" :value="item.value" class="text-none" size="small">
        {{ item.title }}
      </v-btn>
    </v-btn-toggle>

    <template v-if="source === 'write'">
      <v-progress-linear v-if="loading" indeterminate color="primary" />
      <DiMarkdownEditor :model-value="body" compact @update:model-value="emit('update:body', $event)" />
      <div class="text-caption text-medium-emphasis">{{ writeHint }}</div>
    </template>

    <template v-else>
      <div v-if="resourceId" class="d-flex align-center ga-2 flex-wrap">
        <NuxtLink :to="resourceHref(resourceId)" class="text-decoration-none" @click.stop>
          <v-chip size="small" color="primary" variant="tonal">
            {{ resourceName || resourceId }}
          </v-chip>
        </NuxtLink>
        <v-btn size="small" variant="text" @click="emit('clear')">
          {{ t('projectManagement.decision.clearDocument') }}
        </v-btn>
      </div>
      <div v-else class="text-caption text-medium-emphasis">
        {{ emptyLabel || t('projectManagement.meeting.noMinutes') }}
      </div>
      <v-btn variant="tonal" class="text-none align-self-start" @click="emit('pick')">
        {{ t('projectManagement.pickDocument.open') }}
      </v-btn>
    </template>
  </div>
</template>
