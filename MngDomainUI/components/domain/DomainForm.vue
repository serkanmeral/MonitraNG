<template>
  <div>
    <form @submit.prevent="handleSubmit" class="space-y-4">
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Domain Name <span class="text-red-500">*</span>
        </label>
        <UInput
          v-model="formState.domainName"
          placeholder="e.g., acme-corp"
          :disabled="loading"
          class="w-full"
        />
        <p class="mt-1 text-xs text-gray-500">
          Domain name must be unique and lowercase (letters, numbers, hyphens only)
        </p>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Display Name <span class="text-red-500">*</span>
        </label>
        <UInput
          v-model="formState.displayName"
          placeholder="e.g., Acme Corporation"
          :disabled="loading"
          class="w-full"
        />
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Admin Email <span class="text-red-500">*</span>
        </label>
        <UInput
          v-model="formState.adminEmail"
          type="email"
          placeholder="admin@example.com"
          :disabled="loading"
          class="w-full"
        />
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Admin Password <span class="text-red-500">*</span>
        </label>
        <UInput
          v-model="formState.adminPassword"
          type="password"
          placeholder="Enter password"
          :disabled="loading"
          class="w-full"
        />
        <p class="mt-1 text-xs text-gray-500">
          Minimum 8 characters, must contain uppercase, lowercase, number and special character
        </p>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Related Person Phone
        </label>
        <UInput
          v-model="formState.relatedPersonPhone"
          type="tel"
          placeholder="e.g., +905551234567"
          :disabled="loading"
          class="w-full"
        />
        <p class="mt-1 text-xs text-gray-500">
          Phone number for related person (for future SMS features)
        </p>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Logo
        </label>
        <div class="space-y-2">
          <input
            ref="logoFileInput"
            type="file"
            accept="image/*"
            :disabled="loading"
            @change="handleLogoFileChange"
            class="block w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-md file:border-0 file:text-sm file:font-semibold file:bg-primary-50 file:text-primary-700 hover:file:bg-primary-100"
          />
          <div v-if="logoPreview" class="mt-2">
            <img :src="logoPreview" alt="Logo preview" class="max-h-32 rounded border border-gray-200" />
            <UButton
              color="red"
              variant="ghost"
              size="xs"
              class="mt-1"
              @click="clearLogo"
            >
              Remove
            </UButton>
          </div>
        </div>
        <p class="mt-1 text-xs text-gray-500">
          Upload a logo image (PNG, JPG, etc.). Will be converted to base64.
        </p>
      </div>

      <div>
        <label class="block text-sm font-medium text-gray-700 mb-1">
          Logo URL
        </label>
        <UInput
          v-model="formState.logoUrl"
          type="url"
          placeholder="https://example.com/logo.png"
          :disabled="loading"
          class="w-full"
        />
        <p class="mt-1 text-xs text-gray-500">
          External logo URL (for offline systems when sending emails)
        </p>
      </div>

      <!-- Advanced Settings (Collapsible) -->
      <div class="mt-4">
        <UButton
          color="gray"
          variant="ghost"
          :icon="showAdvanced ? 'i-heroicons-chevron-up' : 'i-heroicons-chevron-down'"
          class="w-full justify-between"
          @click="showAdvanced = !showAdvanced"
        >
          Advanced Settings
        </UButton>
        <div v-if="showAdvanced" class="mt-4 space-y-4 pl-4 border-l-2 border-gray-200">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Max Users
            </label>
            <UInput
              v-model.number="formState.settings.maxUsers"
              type="number"
              :disabled="loading"
              class="w-full"
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Max Assets
            </label>
            <UInput
              v-model.number="formState.settings.maxAssets"
              type="number"
              :disabled="loading"
              class="w-full"
            />
          </div>

          <div class="flex items-center justify-between">
            <label class="block text-sm font-medium text-gray-700">
              Enable MQTT
            </label>
            <UToggle
              v-model="formState.settings.enableMqtt"
              :disabled="loading"
            />
          </div>
        </div>
      </div>

      <!-- Error Message -->
      <UAlert
        v-if="error"
        color="red"
        variant="soft"
        :title="error"
        class="mt-4"
      />

      <!-- Form Actions -->
      <div class="flex justify-end gap-2 pt-4">
        <UButton
          color="gray"
          variant="outline"
          @click="$emit('cancel')"
          :disabled="loading"
        >
          Cancel
        </UButton>
        <UButton
          type="submit"
          color="primary"
          :loading="loading"
        >
          Create Domain
        </UButton>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
const emit = defineEmits<{
  success: []
  cancel: []
}>()

const { createDomain } = useDomain()
const loading = ref(false)
const error = ref<string | null>(null)
const logoFileInput = ref<HTMLInputElement | null>(null)
const logoPreview = ref<string | null>(null)

const formState = reactive({
  domainName: '',
  displayName: '',
  adminEmail: '',
  adminPassword: '',
  relatedPersonPhone: '',
  logo: '',
  logoUrl: '',
  settings: {
    maxUsers: 100,
    maxAssets: 1000,
    enableMqtt: false
  }
})

const showAdvanced = ref(false)

const handleLogoFileChange = (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  
  if (!file) {
    return
  }

  // Validate file type
  if (!file.type.startsWith('image/')) {
    error.value = 'Please select an image file'
    return
  }

  // Validate file size (max 5MB)
  const maxSize = 5 * 1024 * 1024 // 5MB
  if (file.size > maxSize) {
    error.value = 'Image size must be less than 5MB'
    return
  }

  // Read file as base64
  const reader = new FileReader()
  reader.onload = (e) => {
    const result = e.target?.result as string
    formState.logo = result
    logoPreview.value = result
    error.value = null
  }
  reader.onerror = () => {
    error.value = 'Failed to read image file'
  }
  reader.readAsDataURL(file)
}

const clearLogo = () => {
  formState.logo = ''
  logoPreview.value = null
  if (logoFileInput.value) {
    logoFileInput.value.value = ''
  }
}

const validateForm = (): boolean => {
  error.value = null

  if (!formState.domainName || formState.domainName.length < 3) {
    error.value = 'Domain name must be at least 3 characters'
    return false
  }

  if (!/^[a-z0-9-]+$/.test(formState.domainName)) {
    error.value = 'Domain name can only contain lowercase letters, numbers, and hyphens'
    return false
  }

  if (!formState.displayName) {
    error.value = 'Display name is required'
    return false
  }

  if (!formState.adminEmail || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formState.adminEmail)) {
    error.value = 'Invalid email address'
    return false
  }

  if (!formState.adminPassword || formState.adminPassword.length < 8) {
    error.value = 'Password must be at least 8 characters'
    return false
  }

  const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9])/
  if (!passwordRegex.test(formState.adminPassword)) {
    error.value = 'Password must contain uppercase, lowercase, number and special character'
    return false
  }

  return true
}

const handleSubmit = async () => {
  if (!validateForm()) {
    return
  }

  loading.value = true
  error.value = null

  try {
    await createDomain({
      domainName: formState.domainName,
      displayName: formState.displayName,
      adminEmail: formState.adminEmail,
      adminPassword: formState.adminPassword,
      relatedPersonPhone: formState.relatedPersonPhone || undefined,
      logo: formState.logo || undefined,
      logoUrl: formState.logoUrl || undefined,
      settings: {
        maxUsers: formState.settings.maxUsers,
        maxAssets: formState.settings.maxAssets,
        enableMqtt: formState.settings.enableMqtt
      }
    })

    // Reset form
    formState.domainName = ''
    formState.displayName = ''
    formState.adminEmail = ''
    formState.adminPassword = ''
    formState.relatedPersonPhone = ''
    formState.logo = ''
    formState.logoUrl = ''
    logoPreview.value = null
    if (logoFileInput.value) {
      logoFileInput.value.value = ''
    }
    formState.settings = {
      maxUsers: 100,
      maxAssets: 1000,
      enableMqtt: false
    }

    emit('success')
  } catch (err: any) {
    error.value = err.message || 'Failed to create domain'
    console.error('Domain creation error:', err)
  } finally {
    loading.value = false
  }
}
</script>
