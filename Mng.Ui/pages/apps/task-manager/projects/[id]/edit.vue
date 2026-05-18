<script setup lang="ts">
import { computed } from 'vue';
import ProjectEditorForm from '@/components/apps/task-manager/ProjectEditorForm.vue';

definePageMeta({ layout: 'default' });

const route = useRoute();
const projectId = computed(() => String(route.params.id ?? ''));
</script>

<template>
  <ProjectEditorForm mode="edit" :project-id="projectId" />
</template>
