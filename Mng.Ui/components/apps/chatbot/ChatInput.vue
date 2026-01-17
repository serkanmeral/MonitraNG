<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{
  disabled?: boolean
  loading?: boolean
}>()

const emit = defineEmits<{
  send: [message: string]
}>()

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp()
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params)
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key, params)
  }
  return key
}
const message = ref('')
const maxLength = 2000

const canSend = computed(() => {
  return message.value.trim().length > 0 && !props.disabled && !props.loading
})

const characterCount = computed(() => {
  return message.value.length
})

const handleSend = () => {
  if (canSend.value) {
    emit('send', message.value)
    message.value = ''
  }
}

const handleKeyDown = (event: KeyboardEvent) => {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    handleSend()
  }
}
</script>

<template>
  <div class="chat-input pa-3 bg-surface">
    <div class="d-flex align-center gap-2">
      <!-- Input Field -->
      <v-textarea
        v-model="message"
        :placeholder="t('chatbot.placeholder')"
        :disabled="disabled || loading"
        :maxlength="maxLength"
        variant="outlined"
        density="compact"
        rows="1"
        auto-grow
        hide-details
        class="flex-grow-1"
        @keydown="handleKeyDown"
      ></v-textarea>

      <!-- Character Count -->
      <div
        v-if="characterCount > maxLength * 0.8"
        class="text-caption text-medium-emphasis"
      >
        {{ characterCount }}/{{ maxLength }}
      </div>

      <!-- Send Button -->
      <v-btn
        :disabled="!canSend"
        :loading="loading"
        color="primary"
        icon
        size="large"
        @click="handleSend"
      >
        <v-icon>mdi-send</v-icon>
      </v-btn>
    </div>
  </div>
</template>

<style scoped>
.chat-input {
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
