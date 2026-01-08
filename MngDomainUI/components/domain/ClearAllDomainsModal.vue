<template>
  <UModal v-model="isOpen">
    <UCard>
      <template #header>
        <div class="flex justify-between items-center">
          <h3 class="text-lg font-semibold text-red-600">Clear All Domains</h3>
          <UButton
            color="gray"
            variant="ghost"
            icon="i-heroicons-x-mark"
            @click="isOpen = false"
          />
        </div>
      </template>

      <div class="space-y-4">
        <UAlert
          color="red"
          variant="soft"
          title="⚠️ DİKKAT: Bu işlem geri alınamaz!"
          description="Bu işlem Keycloak realm'lerini (master hariç) ve MinIO bucket'larını temizleyecektir. MongoDB database'leri temizlenmeyecektir."
        />

        <div class="space-y-2">
          <p class="text-sm font-medium text-gray-700">Temizlenecek Veriler:</p>
          <ul class="list-disc list-inside space-y-1 text-sm text-gray-600 ml-2">
            <li>Keycloak: Master dışındaki <strong>TÜM realm'ler</strong></li>
            <li>MinIO: <strong>TÜM bucket'lar</strong></li>
            <li class="text-gray-500">MongoDB: <span class="line-through">Temizlenmeyecek (manuel yapılmalı)</span></li>
          </ul>
        </div>

        <div class="bg-yellow-50 border border-yellow-200 rounded-lg p-3">
          <p class="text-sm text-yellow-800">
            <strong>Not:</strong> MongoDB database'lerini manuel olarak temizlemeniz gerekecektir.
          </p>
        </div>

        <div class="flex justify-end gap-2 pt-4 border-t border-gray-200">
          <UButton
            color="gray"
            variant="outline"
            @click="isOpen = false"
            :disabled="loading"
          >
            Cancel
          </UButton>
          <UButton
            color="red"
            @click="handleConfirm"
            :loading="loading"
          >
            Clear All Domains
          </UButton>
        </div>
      </div>
    </UCard>
  </UModal>
</template>

<script setup lang="ts">
interface Props {
  modelValue: boolean
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirmed: []
}>()

const isOpen = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const loading = ref(false)

const handleConfirm = async () => {
  loading.value = true
  try {
    emit('confirmed')
    // Modal will be closed by parent component after successful operation
  } catch (error) {
    console.error('Clear all domains error:', error)
  } finally {
    loading.value = false
  }
}
</script>

