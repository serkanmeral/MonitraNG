<template>
  <UModal v-model="isOpen">
    <UCard>
      <template #header>
        <div class="flex justify-between items-center">
          <h3 class="text-lg font-semibold">Access Token Details</h3>
          <UButton
            color="gray"
            variant="ghost"
            icon="i-heroicons-x-mark"
            @click="isOpen = false"
          />
        </div>
      </template>

      <div class="space-y-4">
        <UTabs :items="tabs" v-model="selectedTab">
          <template #default="{ item }">
            <div class="flex items-center gap-2">
              <UIcon :name="item.icon" class="w-4 h-4" />
              <span>{{ item.label }}</span>
            </div>
          </template>

          <template #item="{ item }">
            <div v-if="item.key === 'token'" class="space-y-4">
              <div>
                <label class="text-sm font-medium text-gray-500 mb-2 block">Access Token</label>
                <div class="relative">
                  <UInput
                    :model-value="token ?? ''"
                    readonly
                    class="font-mono text-xs"
                  />
                  <UButton
                    v-if="token"
                    color="gray"
                    variant="ghost"
                    size="xs"
                    icon="i-heroicons-clipboard-document"
                    class="absolute right-2 top-1/2 -translate-y-1/2"
                    @click="copyToClipboard(token)"
                  >
                    Copy
                  </UButton>
                </div>
              </div>
              <UAlert
                color="blue"
                variant="soft"
                title="Token copied to clipboard!"
                v-if="copied"
                @close="copied = false"
              />
            </div>

            <div v-else-if="item.key === 'decoded'" class="space-y-4">
              <div v-if="decodedToken">
                <div class="mb-4">
                  <label class="text-sm font-medium text-gray-500 mb-2 block">Header</label>
                  <pre class="bg-gray-50 text-gray-900 border border-gray-200 dark:bg-gray-900 dark:text-gray-100 dark:border-gray-700 p-4 rounded-lg text-xs overflow-auto font-mono">{{ JSON.stringify(decodedToken.header, null, 2) }}</pre>
                </div>
                <div class="mb-4">
                  <label class="text-sm font-medium text-gray-500 mb-2 block">Payload</label>
                  <pre class="bg-gray-50 text-gray-900 border border-gray-200 dark:bg-gray-900 dark:text-gray-100 dark:border-gray-700 p-4 rounded-lg text-xs overflow-auto font-mono">{{ JSON.stringify(decodedToken.payload, null, 2) }}</pre>
                </div>
                <div v-if="decodedToken.signature">
                  <label class="text-sm font-medium text-gray-500 mb-2 block">Signature</label>
                  <pre class="bg-gray-50 text-gray-900 border border-gray-200 dark:bg-gray-900 dark:text-gray-100 dark:border-gray-700 p-4 rounded-lg text-xs overflow-auto font-mono break-all">{{ decodedToken.signature }}</pre>
                </div>
              </div>
              <UAlert
                v-else
                color="red"
                variant="soft"
                title="Failed to decode token"
              />
            </div>
          </template>
        </UTabs>
      </div>
    </UCard>
  </UModal>
</template>

<script setup lang="ts">
interface Props {
  modelValue: boolean
  token: string | null
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

const isOpen = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const selectedTab = ref(0)
const copied = ref(false)

const tabs = [
  {
    key: 'token',
    label: 'Token',
    icon: 'i-heroicons-key'
  },
  {
    key: 'decoded',
    label: 'Decoded',
    icon: 'i-heroicons-code-bracket'
  }
]

const decodedToken = computed(() => {
  if (!props.token) return null

  try {
    // JWT format: header.payload.signature
    const parts = props.token.split('.')
    if (parts.length !== 3) {
      return null
    }

    // Decode header and payload (base64url)
    const decodeBase64Url = (str: string): string => {
      // Replace URL-safe characters
      str = str.replace(/-/g, '+').replace(/_/g, '/')
      
      // Add padding if needed
      while (str.length % 4) {
        str += '='
      }
      
      try {
        return decodeURIComponent(
          atob(str)
            .split('')
            .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
            .join('')
        )
      } catch {
        return ''
      }
    }

    const header = JSON.parse(decodeBase64Url(parts[0]))
    const payload = JSON.parse(decodeBase64Url(parts[1]))
    const signature = parts[2]

    return {
      header,
      payload,
      signature
    }
  } catch (error) {
    console.error('Failed to decode token:', error)
    return null
  }
})

const copyToClipboard = async (text: string) => {
  try {
    await navigator.clipboard.writeText(text)
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, 2000)
  } catch (error) {
    console.error('Failed to copy to clipboard:', error)
  }
}
</script>

