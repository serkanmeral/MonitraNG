import { ref, computed } from 'vue'
import { fetchFromMngLLM } from '@/services/apiService'

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  timestamp: Date
  intent?: string
  documentationSources?: Array<{
    title: string
    service: string
    category: string
    relevanceScore: number
  }>
}

export interface ChatResponse {
  answer: string
  intent: string
  intentConfidence: number
  documentationSources: Array<{
    title: string
    documentId: string
    service: string
    category: string
    relevanceScore: number
  }>
  sessionId: string
  metadata: Record<string, any>
}

export const useChatbot = () => {
  // Get i18n instance for legacy mode
  const nuxtApp = useNuxtApp()
  const i18n = nuxtApp.vueApp.config.globalProperties.$i18n
  const locale = computed(() => {
    return i18n?.locale?.value || i18n?.global?.locale?.value || 'tr'
  })
  
  // State
  const messages = ref<ChatMessage[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const sessionId = ref<string | null>(null)
  const isOpen = ref(false)

  // Computed
  const hasMessages = computed(() => messages.value.length > 0)
  const lastMessage = computed(() => messages.value[messages.value.length - 1])

  /**
   * Send message to chatbot
   */
  const sendMessage = async (message: string): Promise<void> => {
    if (!message.trim() || isLoading.value) {
      return
    }

    // Add user message
    const userMessage: ChatMessage = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: message.trim(),
      timestamp: new Date()
    }
    messages.value.push(userMessage)
    isLoading.value = true
    error.value = null

    try {
      // Generate session ID if not exists
      if (!sessionId.value) {
        sessionId.value = crypto.randomUUID()
      }

      // Prepare request
      const requestBody = {
        message: message.trim(),
        sessionId: sessionId.value,
        language: locale.value || 'tr',
        conversationHistory: messages.value
          .slice(0, -1) // Exclude current user message
          .map(msg => ({
            role: msg.role,
            content: msg.content,
            timestamp: msg.timestamp.toISOString()
          }))
      }

      // Call API - path format: 'v1/chatbot/chat' (server route /api/llm/[...path] will forward to MngLLM)
      const response = await fetchFromMngLLM<ChatResponse>(
        'v1/chatbot/chat',
        'POST',
        requestBody
      )

      // Add assistant message
      const assistantMessage: ChatMessage = {
        id: `assistant-${Date.now()}`,
        role: 'assistant',
        content: response.answer,
        timestamp: new Date(),
        intent: response.intent,
        documentationSources: response.documentationSources?.map(doc => ({
          title: doc.title,
          service: doc.service,
          category: doc.category,
          relevanceScore: doc.relevanceScore
        }))
      }
      messages.value.push(assistantMessage)

      // Update session ID if provided
      if (response.sessionId) {
        sessionId.value = response.sessionId
      }
    } catch (err: any) {
      error.value = err.message || 'An error occurred while sending message'
      console.error('Chatbot error:', err)
      
      // Add error message
      const errorMessage: ChatMessage = {
        id: `error-${Date.now()}`,
        role: 'assistant',
        content: error.value,
        timestamp: new Date()
      }
      messages.value.push(errorMessage)
    } finally {
      isLoading.value = false
    }
  }

  /**
   * Clear conversation session
   */
  const clearSession = async (): Promise<void> => {
    if (!sessionId.value) {
      return
    }

    try {
      await fetchFromMngLLM(
        `v1/chatbot/session/${sessionId.value}`,
        'DELETE'
      )
    } catch (err) {
      console.error('Error clearing session:', err)
    } finally {
      messages.value = []
      sessionId.value = null
      error.value = null
    }
  }

  /**
   * Toggle chatbot widget
   */
  const toggle = () => {
    isOpen.value = !isOpen.value
  }

  /**
   * Open chatbot widget
   */
  const open = () => {
    isOpen.value = true
  }

  /**
   * Close chatbot widget
   */
  const close = () => {
    isOpen.value = false
  }

  return {
    // State
    messages,
    isLoading,
    error,
    sessionId,
    isOpen,
    
    // Computed
    hasMessages,
    lastMessage,
    
    // Methods
    sendMessage,
    clearSession,
    toggle,
    open,
    close
  }
}
