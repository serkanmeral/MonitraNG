<script setup lang="ts">
import { onMounted } from 'vue';

/** Eski tema yolu `/apps/chats` → DG tabanlı sohbet odası */
definePageMeta({ layout: 'default' });

onMounted(() => {
  void navigateTo('/apps/chat-room', { replace: true });
});
</script>

<template>
  <div class="pa-4 text-caption text-medium-emphasis">{{ $t('chatRoom.pageTitle') }}…</div>
</template>
