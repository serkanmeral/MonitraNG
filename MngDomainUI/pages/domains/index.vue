<template>
  <div>
    <div class="flex justify-between items-center mb-6">
      <div>
        <h1 class="text-3xl font-bold text-gray-900">Domain Management</h1>
        <p class="text-gray-600 mt-1">Manage your domains</p>
      </div>
      <div class="flex gap-2">
        <UButton
          color="gray"
          variant="outline"
          @click="refreshDomains"
          :loading="domainStore.loading"
        >
          Refresh
        </UButton>
        <UButton
          color="primary"
          @click="showCreateModal = true"
        >
          Create Domain
        </UButton>
      </div>
    </div>

    <!-- Error Message -->
    <UAlert
      v-if="domainStore.error"
      color="red"
      variant="soft"
      :title="domainStore.error"
      class="mb-4"
      @close="domainStore.clearError"
    />

    <!-- Domain List -->
    <DomainList
      :domains="domainStore.domains"
      :loading="domainStore.loading"
      @refresh="refreshDomains"
      @delete="handleDelete"
      @clearAll="showClearAllModal = true"
    />

    <!-- Create Domain Modal -->
    <UModal v-model="showCreateModal">
      <UCard>
        <template #header>
          <h3 class="text-lg font-semibold">Create New Domain</h3>
        </template>
        <DomainForm @success="handleCreateSuccess" @cancel="showCreateModal = false" />
      </UCard>
    </UModal>

    <!-- Clear All Domains Modal -->
    <DomainClearAllDomainsModal
      v-model="showClearAllModal"
      @confirmed="handleClearAll"
    />
  </div>
</template>

<script setup lang="ts">
import { useDomainStore } from '~/stores/domain'

definePageMeta({
  layout: 'default',
  middleware: 'auth'
})

const domainStore = useDomainStore()
const showCreateModal = ref(false)
const showClearAllModal = ref(false)

// Fetch domains on mount
onMounted(() => {
  refreshDomains()
})

const refreshDomains = async () => {
  try {
    await domainStore.fetchDomains()
  } catch (error) {
    console.error('Failed to fetch domains:', error)
  }
}

const handleCreateSuccess = () => {
  showCreateModal.value = false
  refreshDomains()
}

const handleDelete = async (id: string) => {
  try {
    const { deleteDomain } = useDomain()
    await deleteDomain(id)
    await refreshDomains()
  } catch (error: any) {
    console.error('Failed to delete domain:', error)
  }
}

const handleClearAll = async () => {
  try {
    const { clearAllDomains } = useDomain()
    const result = await clearAllDomains()
    
    // Show success message
    if (result.success) {
      // Close modal
      showClearAllModal.value = false
      
      // Refresh domains list (may be empty now)
      await refreshDomains()
      
      // Show success notification (you might want to use a toast library)
      console.log('✅ Clear all domains completed:', result.message)
      console.log('Results:', result.results)
    } else {
      console.error('❌ Clear all domains failed:', result)
    }
  } catch (error: any) {
    console.error('Failed to clear all domains:', error)
    showClearAllModal.value = false
  }
}
</script>

