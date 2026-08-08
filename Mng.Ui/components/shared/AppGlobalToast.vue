<script setup lang="ts">
import { useAppToast } from '@/composables/useAppToast';

const { items, dismiss, onAction } = useAppToast();
</script>

<template>
  <div class="app-toast-stack" aria-live="polite" aria-relevant="additions">
    <TransitionGroup name="app-toast">
      <div
        v-for="item in items"
        :key="item.id"
        class="app-toast-stack__item"
      >
        <v-alert
          :color="item.color"
          variant="flat"
          density="comfortable"
          border="start"
          closable
          class="app-toast-stack__alert"
          @click:close="dismiss(item.id)"
        >
          <div class="d-flex align-start justify-space-between ga-2">
            <div class="d-flex flex-column ga-1 flex-grow-1">
              <div class="text-subtitle-2 font-weight-bold">{{ item.title }}</div>
              <div
                v-if="item.message && item.message !== item.title"
                class="text-body-2"
              >
                {{ item.message }}
              </div>
            </div>
            <v-btn
              v-if="item.deepLink"
              size="small"
              variant="text"
              class="text-none flex-shrink-0"
              @click="onAction(item.id)"
            >
              Görüntüle
            </v-btn>
          </div>
        </v-alert>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.app-toast-stack {
  position: fixed;
  top: 72px;
  right: 16px;
  z-index: 4000;
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: min(380px, calc(100vw - 32px));
  pointer-events: none;
}

.app-toast-stack__item {
  pointer-events: auto;
}

.app-toast-stack__alert {
  box-shadow: 0 8px 24px rgba(15, 23, 42, 0.18);
}

.app-toast-enter-active,
.app-toast-leave-active {
  transition: opacity 0.18s ease, transform 0.18s ease;
}

.app-toast-enter-from {
  opacity: 0;
  transform: translateX(16px);
}

.app-toast-leave-to {
  opacity: 0;
  transform: translateX(16px);
}

.app-toast-move {
  transition: transform 0.18s ease;
}
</style>
