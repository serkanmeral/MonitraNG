<template>
  <UModal v-model="isOpen" :prevent-close="loading">
    <UCard>
      <template #header>
        <div class="flex justify-between items-center">
          <h3 class="text-lg font-semibold">Admin Authentication Required</h3>
          <UButton
            v-if="!loading"
            color="gray"
            variant="ghost"
            icon="i-heroicons-x-mark"
            @click="handleCancel"
          />
        </div>
      </template>

      <div class="space-y-4">
        <UAlert
          v-if="error"
          color="red"
          variant="soft"
          :title="error"
          @close="error = null"
        />

        <p class="text-sm text-gray-600">
          Please provide admin credentials for domain <strong>{{ domainName }}</strong> to proceed with this operation.
        </p>
        <p class="text-xs text-gray-500 mb-4">
          Username format: <code class="bg-gray-100 px-1 py-0.5 rounded">{{ domainName }}@username</code> (e.g., {{ domainName }}@{{ domainName }}_admin)
        </p>

        <UForm
          :state="formState"
          :schema="schema"
          @submit="handleSubmit"
          class="space-y-4"
        >
          <UFormGroup label="Username" name="username" required>
            <div class="flex items-center gap-2">
              <span class="text-sm text-gray-500">{{ domainName }}@</span>
              <UInput
                v-model="formState.username"
                placeholder="username"
                :disabled="loading"
                autofocus
                class="flex-1"
              />
            </div>
            <template #hint>
              Enter username only (domain will be added automatically: {{ domainName }}@username)
            </template>
          </UFormGroup>

          <UFormGroup label="Password" name="password" required>
            <UInput
              v-model="formState.password"
              type="password"
              placeholder="Enter password"
              :disabled="loading"
            />
            <template #hint>
              Admin password for the domain
            </template>
          </UFormGroup>

          <div class="flex justify-end gap-2 pt-4">
            <UButton
              color="gray"
              variant="outline"
              @click="handleCancel"
              :disabled="loading"
            >
              Cancel
            </UButton>
            <UButton
              type="submit"
              color="primary"
              :loading="loading"
            >
              Authenticate
            </UButton>
          </div>
        </UForm>
      </div>
    </UCard>
  </UModal>
</template>

<script setup lang="ts">
import { z } from 'zod'

interface Props {
  modelValue: boolean
  domainName: string
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  authenticated: [data: { token: string; username: string }]
  cancel: []
}>()

const isOpen = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const config = useRuntimeConfig()
const loading = ref(false)
const error = ref<string | null>(null)

const formState = reactive({
  username: '',
  password: ''
})

const schema = z.object({
  username: z.string().min(1, 'Username is required'),
  password: z.string().min(1, 'Password is required')
})

const handleSubmit = async () => {
  loading.value = true
  error.value = null

  try {
    // Validate form
    schema.parse(formState)

    // Get keeper URL
    const keeperUrl = config.public.gatewayUrl 
      ? `${config.public.gatewayUrl}/keeper`
      : (config.public.keeperUrl || 'https://localhost:5001')

    // Construct username: domain@username
    const fullUsername = `${props.domainName}@${formState.username}`

    // Get token from MngKeeper
    // Note: SSL bypass is handled server-side via plugin
    // Domain is already included in username (domain@username format)
    const tokenResponse = await $fetch(`/api/keeper/auth/token`, {
      method: 'POST',
      body: {
        username: fullUsername,
        password: formState.password
      }
    }) as any

    const token = tokenResponse.accessToken
    if (!token) {
      throw new Error('Token not found in response')
    }

    // Emit authenticated event with token
    emit('authenticated', {
      token,
      username: fullUsername
    })

    // Close modal
    isOpen.value = false
  } catch (err: any) {
    if (err.errors) {
      error.value = err.errors[0]?.message || 'Validation failed'
    } else {
      error.value = err.message || 'Failed to authenticate'
    }
  } finally {
    loading.value = false
  }
}

const handleCancel = () => {
  formState.username = ''
  formState.password = ''
  error.value = null
  emit('cancel')
  isOpen.value = false
}

// Reset form when modal closes
watch(isOpen, (newValue) => {
  if (!newValue) {
    formState.username = ''
    formState.password = ''
    error.value = null
  }
})
</script>

