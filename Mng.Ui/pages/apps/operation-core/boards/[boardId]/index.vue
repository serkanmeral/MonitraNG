<script setup lang="ts">
import { computed } from 'vue';
import OcBoardPanel from '@/components/apps/operation-core/OcBoardPanel.client.vue';

definePageMeta({ layout: 'default' });

const route = useRoute();
const boardId = computed(() => String(route.params.boardId ?? ''));
</script>

<template>
  <OcBoardPanel :key="boardId" :board-id="boardId" />
</template>
