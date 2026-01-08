<template>
  <UModal v-model="isOpen" :prevent-close="loading">
    <UCard>
      <template #header>
        <div class="flex justify-between items-center">
          <h3 class="text-lg font-semibold">Create Test Users</h3>
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
          How many test users would you like to create?
        </p>

        <UForm
          :state="formState"
          :schema="schema"
          @submit="handleSubmit"
          class="space-y-4"
        >
          <UFormGroup label="Number of Users" name="userCount" required>
            <UInput
              v-model.number="formState.userCount"
              type="number"
              placeholder="Enter number (e.g., 10)"
              :disabled="loading"
              :min="1"
              :max="100"
              autofocus
            />
            <template #hint>
              Enter a number between 1 and 100
            </template>
          </UFormGroup>

          <UFormGroup label="Default Password" name="password" required>
            <UInput
              v-model="formState.password"
              type="password"
              placeholder="Enter default password"
              :disabled="loading"
            />
            <template #hint>
              All users will be created with this password
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
              Create Users
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
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirmed: [data: { userCount: number; password: string }]
  cancel: []
}>()

const isOpen = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const loading = ref(false)
const error = ref<string | null>(null)

const formState = reactive({
  userCount: 5,
  password: 'Test123!'
})

const schema = z.object({
  userCount: z.number().min(1, 'At least 1 user is required').max(100, 'Maximum 100 users allowed'),
  password: z.string().min(6, 'Password must be at least 6 characters')
})

const handleSubmit = async () => {
  loading.value = true
  error.value = null

  try {
    schema.parse(formState)
    emit('confirmed', {
      userCount: formState.userCount,
      password: formState.password
    })
    isOpen.value = false
  } catch (err: any) {
    if (err.errors) {
      error.value = err.errors[0]?.message || 'Validation failed'
    } else {
      error.value = err.message || 'Failed to submit'
    }
  } finally {
    loading.value = false
  }
}

const handleCancel = () => {
  formState.userCount = 5
  formState.password = 'Test123!'
  error.value = null
  emit('cancel')
  isOpen.value = false
}

watch(isOpen, (newValue) => {
  if (!newValue) {
    formState.userCount = 5
    formState.password = 'Test123!'
    error.value = null
  }
})
</script>

