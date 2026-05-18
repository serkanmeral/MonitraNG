<script setup lang="ts">
import { computed, onMounted } from 'vue';

definePageMeta({ layout: 'blank' });

const route = useRoute();
const issueKey = computed(() => decodeURIComponent(String(route.params.key ?? '')));

onMounted(() => {
  const q: Record<string, string> = { tab: 'history' };
  const board = route.query.board;
  if (board != null) {
    const s = Array.isArray(board) ? board[0] : board;
    if (s) q.board = String(s);
  }
  const fromQ = route.query.from;
  if (fromQ != null) {
    const s = Array.isArray(fromQ) ? fromQ[0] : fromQ;
    if (s) q.from = String(s);
  }
  void navigateTo(
    {
      path: `/apps/task-manager/issues/${encodeURIComponent(issueKey.value)}/profile`,
      query: q,
    },
    { replace: true }
  );
});
</script>

<template>
  <div class="d-flex align-center justify-center pa-12">
    <v-progress-circular indeterminate color="primary" size="48" />
  </div>
</template>
