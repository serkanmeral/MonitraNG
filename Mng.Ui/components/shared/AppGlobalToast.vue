<script setup lang="ts">
import { useAppToast } from '@/composables/useAppToast';

const { active, visible, dismiss, onAction } = useAppToast();
</script>

<template>
  <v-snackbar
    v-model="visible"
    :color="active?.color ?? 'secondary'"
    location="top right"
    :timeout="active?.timeout ?? 6000"
    multi-line
    @update:model-value="(v: boolean) => { if (!v) dismiss(); }"
  >
    <div class="d-flex flex-column ga-1">
      <div class="text-subtitle-2 font-weight-bold">{{ active?.title }}</div>
      <div v-if="active?.message && active.message !== active.title" class="text-body-2">
        {{ active.message }}
      </div>
    </div>
    <template v-if="active?.deepLink" #actions>
      <v-btn variant="text" class="text-none" @click="onAction">
        Görüntüle
      </v-btn>
    </template>
  </v-snackbar>
</template>
