<script setup lang="ts">
import { computed } from 'vue'
import { formatDistanceToNow } from 'date-fns'
import type { ChatMessage as ChatMessageType } from '@/composables/useChatbot'

const props = defineProps<{
  message: ChatMessageType
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
const locale = computed(() => {
  return i18n?.locale?.value || i18n?.global?.locale?.value || 'tr'
})

// Format timestamp
const formattedTime = computed(() => {
  try {
    return formatDistanceToNow(props.message.timestamp, {
      addSuffix: true,
      locale: locale.value === 'tr' ? require('date-fns/locale/tr') : undefined
    })
  } catch {
    return ''
  }
})

// Simple markdown-like formatting (basic support)
const formatContent = (text: string): string => {
  // Convert **bold** to <strong>
  text = text.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
  // Convert *italic* to <em>
  text = text.replace(/\*(.*?)\*/g, '<em>$1</em>')
  // Convert `code` to <code>
  text = text.replace(/`(.*?)`/g, '<code>$1</code>')
  // Convert line breaks
  text = text.replace(/\n/g, '<br>')
  return text
}

const formattedContent = computed(() => {
  if (props.message.role === 'assistant') {
    return formatContent(props.message.content)
  }
  return props.message.content
})

// Copy message to clipboard
const copyMessage = async () => {
  try {
    await navigator.clipboard.writeText(props.message.content)
    // Show toast notification (you can use a toast library here)
    console.log(t('chatbot.copied'))
  } catch (err) {
    console.error('Failed to copy:', err)
  }
}

// Get intent label
const intentLabel = computed(() => {
  if (!props.message.intent) return ''
  return t(`chatbot.intent.${props.message.intent}`)
})
</script>

<template>
  <div
    :class="[
      'd-flex mb-4',
      message.role === 'user' ? 'justify-end' : 'justify-start'
    ]"
  >
    <div
      :class="[
        'd-flex align-start gap-3',
        message.role === 'user' ? 'flex-row-reverse' : 'flex-row',
        'w-75'
      ]"
    >
      <!-- Avatar -->
      <v-avatar
        :size="32"
        :color="message.role === 'user' ? 'primary' : 'secondary'"
      >
        <span v-if="message.role === 'user'" class="text-white text-caption">
          U
        </span>
        <span v-else class="text-white text-caption">M</span>
      </v-avatar>

      <!-- Message Content -->
      <div
        :class="[
          'd-flex flex-column',
          message.role === 'user' ? 'align-end' : 'align-start'
        ]"
        style="max-width: 85%"
      >
        <!-- Timestamp and Intent -->
        <div
          :class="[
            'd-flex align-center gap-2 mb-1',
            message.role === 'user' ? 'flex-row-reverse' : 'flex-row'
          ]"
        >
          <small class="text-medium-emphasis text-caption">
            {{ formattedTime }}
          </small>
          <v-chip
            v-if="message.intent && message.role === 'assistant'"
            size="x-small"
            variant="tonal"
            color="primary"
          >
            {{ intentLabel }}
          </v-chip>
        </div>

        <!-- Message Bubble -->
        <v-sheet
          :class="[
            'rounded-lg px-4 py-3',
            message.role === 'user'
              ? 'bg-primary text-white'
              : 'bg-surface-variant'
          ]"
          elevation="1"
        >
          <!-- Markdown content for assistant, plain text for user -->
          <div
            v-if="message.role === 'assistant'"
            class="text-body-2"
            v-html="formattedContent"
          ></div>
          <p v-else class="text-body-2 mb-0">{{ message.content }}</p>
        </v-sheet>

        <!-- Documentation Sources -->
        <div
          v-if="
            message.documentationSources &&
            message.documentationSources.length > 0 &&
            message.role === 'assistant'
          "
          class="mt-2"
        >
          <v-chip
            size="small"
            variant="outlined"
            color="info"
            class="mr-1 mb-1"
            v-for="(doc, index) in message.documentationSources"
            :key="index"
          >
            <v-icon start size="16">mdi-file-document</v-icon>
            {{ doc.title }}
          </v-chip>
        </div>

        <!-- Copy Button -->
        <v-btn
          v-if="message.role === 'assistant'"
          icon
          size="x-small"
          variant="text"
          class="mt-1"
          @click="copyMessage"
        >
          <v-icon size="16">mdi-content-copy</v-icon>
        </v-btn>
      </div>
    </div>
  </div>
</template>

<style scoped>
:deep(.v-sheet) {
  word-wrap: break-word;
  overflow-wrap: break-word;
}

:deep(p) {
  margin-bottom: 0.5rem;
}

:deep(p:last-child) {
  margin-bottom: 0;
}

:deep(code) {
  background-color: rgba(0, 0, 0, 0.1);
  padding: 2px 4px;
  border-radius: 3px;
  font-size: 0.9em;
}

:deep(pre) {
  background-color: rgba(0, 0, 0, 0.05);
  padding: 8px;
  border-radius: 4px;
  overflow-x: auto;
}
</style>
