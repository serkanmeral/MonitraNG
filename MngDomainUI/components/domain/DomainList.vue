<template>
  <div>
    <!-- Loading State -->
    <div v-if="loading" class="flex justify-center items-center py-12">
      <UIcon name="i-heroicons-arrow-path" class="w-8 h-8 animate-spin text-primary" />
    </div>

    <!-- Empty State -->
    <UCard v-else-if="domains.length === 0" class="text-center py-12">
      <UIcon name="i-heroicons-folder" class="w-16 h-16 mx-auto text-gray-400 mb-4" />
      <h3 class="text-lg font-semibold text-gray-900 mb-2">No domains found</h3>
      <p class="text-gray-600 mb-4">Get started by creating your first domain</p>
    </UCard>

    <!-- Domain Table -->
    <UCard v-else>
      <div class="overflow-x-auto">
        <table class="w-full">
          <thead>
            <tr class="border-b border-gray-200">
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Name</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Display Name</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Status</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Database</th>
              <th class="text-left py-3 px-4 font-semibold text-gray-700">Created</th>
              <th class="text-right py-3 px-4 font-semibold text-gray-700">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="domain in domains"
              :key="domain.id"
              class="border-b border-gray-100 hover:bg-gray-50"
            >
              <td class="py-3 px-4">
                <NuxtLink
                  :to="`/domains/${domain.id}`"
                  class="font-medium text-primary hover:underline"
                >
                  {{ domain.name }}
                </NuxtLink>
              </td>
              <td class="py-3 px-4 text-gray-700">{{ domain.displayName }}</td>
              <td class="py-3 px-4">
                <UBadge
                  :color="getStatusColor(domain.status)"
                  variant="soft"
                >
                  {{ domain.status }}
                </UBadge>
              </td>
              <td class="py-3 px-4 text-gray-600 text-sm">{{ domain.databaseName }}</td>
              <td class="py-3 px-4 text-gray-600 text-sm">
                {{ formatDate(domain.createdAt) }}
              </td>
              <td class="py-3 px-4">
                <div class="flex justify-end gap-2">
                  <UButton
                    color="gray"
                    variant="ghost"
                    size="sm"
                    icon="i-heroicons-pencil"
                    :to="`/domains/${domain.id}`"
                  />
                  <UButton
                    color="red"
                    variant="ghost"
                    size="sm"
                    icon="i-heroicons-trash"
                    @click="$emit('delete', domain.id)"
                  />
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Clear All Button -->
      <div v-if="domains.length > 0" class="mt-4 pt-4 border-t border-gray-200">
        <UButton
          color="red"
          variant="outline"
          @click="$emit('clearAll')"
        >
          Clear All Domains
        </UButton>
      </div>
    </UCard>
  </div>
</template>

<script setup lang="ts">
import type { Domain, DomainStatus } from '~/types/domain'

interface Props {
  domains: Domain[]
  loading: boolean
}

defineProps<Props>()

defineEmits<{
  delete: [id: string]
  clearAll: []
}>()

const getStatusColor = (status: DomainStatus): 'green' | 'yellow' | 'orange' | 'gray' | 'red' => {
  const colors: Record<DomainStatus, 'green' | 'yellow' | 'orange' | 'gray' | 'red'> = {
    Active: 'green',
    Pending: 'yellow',
    Suspended: 'orange',
    Expired: 'gray',
    Deleted: 'red',
    Failed: 'red'
  }
  return colors[status] || 'gray'
}

const formatDate = (dateString: string): string => {
  const date = new Date(dateString)
  return date.toLocaleDateString('tr-TR', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}
</script>

