<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted } from 'vue'
import { useChatbot } from '@/composables/useChatbot'
import ChatMessage from './ChatMessage.vue'
import ChatInput from './ChatInput.vue'

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
const {
  messages,
  isLoading,
  error,
  isOpen,
  hasMessages,
  sendMessage,
  clearSession,
  toggle,
  open,
  close
} = useChatbot()

const messagesContainer = ref<HTMLElement | null>(null)
const isMinimized = ref(false)

// Auto-scroll to bottom when new message arrives
watch(
  () => messages.value.length,
  () => {
    nextTick(() => {
      scrollToBottom()
    })
  }
)

const scrollToBottom = () => {
  if (messagesContainer.value) {
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
  }
}

const handleSend = async (message: string) => {
  await sendMessage(message)
  nextTick(() => {
    scrollToBottom()
  })
}

const handleClear = async () => {
  await clearSession()
}

const handleToggle = () => {
  toggle()
  if (isOpen.value) {
    nextTick(() => {
      scrollToBottom()
    })
  }
}

onMounted(() => {
  // Auto-scroll on mount
  nextTick(() => {
    scrollToBottom()
  })
})
</script>

<template>
  <!-- Floating Button -->
  <v-btn
    v-if="!isOpen"
    icon
    size="large"
    color="primary"
    class="chatbot-fab"
    style="position: fixed !important; bottom: 24px !important; right: 24px !important; z-index: 9999 !important; width: 64px; height: 64px; border-radius: 50%; box-shadow: 0 4px 12px rgba(0,0,0,0.3);"
    @click="handleToggle"
  >
    <v-icon size="32">mdi-robot</v-icon>
    <v-tooltip activator="parent" location="top">
      {{ t('chatbot.title') }}
    </v-tooltip>
  </v-btn>

  <!-- Chat Widget -->
  <v-card
    v-if="isOpen"
    class="chatbot-widget"
    elevation="8"
    :style="{
      position: 'fixed',
      bottom: '24px',
      right: '24px',
      width: '400px',
      maxWidth: 'calc(100vw - 48px)',
      height: isMinimized ? '64px' : '600px',
      maxHeight: 'calc(100vh - 48px)',
      zIndex: 9999,
      display: 'flex',
      flexDirection: 'column',
      transition: 'height 0.3s ease'
    }"
  >
      <!-- Header -->
    <v-card-title class="d-flex align-center justify-space-between bg-primary text-white pa-3">
      <div class="d-flex align-center gap-2">
        <v-avatar size="32" color="white">
          <span class="text-primary text-caption">M</span>
        </v-avatar>
        <div>
          <div class="text-subtitle-1 font-weight-bold">{{ t('chatbot.title') }}</div>
          <div class="text-caption opacity-75">{{ t('chatbot.subtitle') }}</div>
        </div>
      </div>
      <div class="d-flex align-center gap-1">
        <v-btn
          v-if="hasMessages"
          icon
          variant="text"
          size="small"
          class="text-white"
          @click="handleClear"
        >
          <v-icon>mdi-delete-outline</v-icon>
          <v-tooltip activator="parent" location="bottom">
            {{ t('chatbot.clear') }}
          </v-tooltip>
        </v-btn>
        <v-btn
          icon
          variant="text"
          size="small"
          class="text-white"
          @click="isMinimized = !isMinimized"
        >
          <v-icon>{{ isMinimized ? 'mdi-window-maximize' : 'mdi-window-minimize' }}</v-icon>
        </v-btn>
        <v-btn
          icon
          variant="text"
          size="small"
          class="text-white"
          @click="handleToggle"
        >
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </div>
    </v-card-title>

    <!-- Messages Container -->
    <div
      v-if="!isMinimized"
      ref="messagesContainer"
      class="flex-grow-1 overflow-y-auto pa-4"
      style="min-height: 0"
    >
      <!-- No Messages -->
      <div
        v-if="!hasMessages"
        class="d-flex align-center justify-center h-100 text-center"
      >
        <div>
          <v-icon size="64" color="grey-lighten-1" class="mb-4">mdi-robot-outline</v-icon>
          <p class="text-body-1 text-medium-emphasis">
            {{ t('chatbot.noMessages') }}
          </p>
        </div>
      </div>

      <!-- Messages List -->
      <div v-else>
        <ChatMessage
          v-for="message in messages"
          :key="message.id"
          :message="message"
        />
      </div>

      <!-- Typing Indicator -->
      <div
        v-if="isLoading"
        class="d-flex align-start gap-3 mb-4"
      >
        <v-avatar size="32" color="secondary">
          <span class="text-white text-caption">M</span>
        </v-avatar>
        <div class="d-flex flex-column">
          <v-sheet class="bg-surface-variant rounded-lg px-4 py-3">
            <div class="d-flex align-center gap-1">
              <v-progress-circular
                indeterminate
                size="16"
                width="2"
                color="primary"
              ></v-progress-circular>
              <span class="text-body-2 text-medium-emphasis">
                {{ t('chatbot.typing') }}
              </span>
            </div>
          </v-sheet>
        </div>
      </div>
    </div>

    <!-- Input -->
    <div v-if="!isMinimized">
      <ChatInput
        :disabled="isLoading"
        :loading="isLoading"
        @send="handleSend"
      />
    </div>

    <!-- Error Message -->
    <v-alert
      v-if="error"
      type="error"
      variant="tonal"
      density="compact"
      class="ma-2"
      closable
      @click:close="error = null"
    >
      {{ error }}
    </v-alert>
  </v-card>
</template>

<style scoped>
.chatbot-widget {
  animation: slideUp 0.3s ease-out;
}

@keyframes slideUp {
  from {
    transform: translateY(20px);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

.chatbot-fab {
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0%,
  100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.05);
  }
}

/* Scrollbar styling */
:deep(.overflow-y-auto) {
  scrollbar-width: thin;
  scrollbar-color: rgba(var(--v-border-color), var(--v-border-opacity)) transparent;
}

:deep(.overflow-y-auto::-webkit-scrollbar) {
  width: 6px;
}

:deep(.overflow-y-auto::-webkit-scrollbar-track) {
  background: transparent;
}

:deep(.overflow-y-auto::-webkit-scrollbar-thumb) {
  background-color: rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 3px;
}

:deep(.overflow-y-auto::-webkit-scrollbar-thumb:hover) {
  background-color: rgba(var(--v-border-color), 0.8);
}
</style>
