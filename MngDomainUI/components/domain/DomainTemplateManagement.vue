<template>
  <div class="space-y-6">
    <!-- Template List -->
    <UCard>
      <template #header>
        <div class="flex justify-between items-center">
          <h3 class="text-lg font-semibold">Templates</h3>
          <UButton
            color="primary"
            icon="i-heroicons-plus"
            @click="showCreateModal = true"
          >
            Create Template
          </UButton>
        </div>
      </template>

      <div v-if="loadingTemplates" class="flex justify-center py-8">
        <UIcon name="i-heroicons-arrow-path" class="w-6 h-6 animate-spin text-primary" />
      </div>

      <div v-else-if="templates.length === 0" class="text-center py-8 text-gray-500">
        <p>No templates found for this domain.</p>
        <p class="text-sm mt-2">Create a template to get started.</p>
      </div>

      <div v-else class="space-y-4">
        <div
          v-for="template in templates"
          :key="template.id"
          class="border rounded-lg p-4 hover:bg-gray-50 transition-colors"
        >
          <div class="flex justify-between items-start">
            <div class="flex-1">
              <div class="flex items-center gap-2 mb-2">
                <h4 class="font-semibold text-lg">{{ template.name }}</h4>
                <UBadge color="gray" variant="soft" size="sm">
                  {{ template.totalDocumentCount }} documents
                </UBadge>
              </div>
              <p v-if="template.description" class="text-sm text-gray-600 mb-2">
                {{ template.description }}
              </p>
              <div class="flex flex-wrap gap-2 text-xs text-gray-500">
                <span>{{ template.collections.length }} collections</span>
                <span>•</span>
                <span>Created: {{ formatDate(template.createdAt) }}</span>
                <span v-if="template.updatedAt">•</span>
                <span v-if="template.updatedAt">Updated: {{ formatDate(template.updatedAt) }}</span>
              </div>
              <div class="mt-2">
                <p class="text-xs font-medium text-gray-700 mb-1">Collections:</p>
                <div class="flex flex-wrap gap-1">
                  <UBadge
                    v-for="collection in template.collections"
                    :key="collection.collectionName"
                    color="blue"
                    variant="outline"
                    size="xs"
                  >
                    {{ collection.collectionName }}
                    <span class="ml-1 text-gray-500">({{ collection.documentCount }})</span>
                  </UBadge>
                </div>
              </div>
            </div>
            <div class="flex gap-2 ml-4">
              <UButton
                color="gray"
                variant="ghost"
                icon="i-heroicons-pencil"
                size="sm"
                @click="editTemplate(template)"
              />
              <UButton
                color="red"
                variant="ghost"
                icon="i-heroicons-trash"
                size="sm"
                @click="confirmDeleteTemplate(template)"
              />
            </div>
          </div>
        </div>
      </div>
    </UCard>

    <!-- Create/Edit Template Modal -->
    <UModal v-model="showCreateModal" :ui="{ width: 'max-w-4xl' }">
      <UCard>
        <template #header>
          <div class="flex justify-between items-center">
            <h3 class="text-lg font-semibold">
              {{ editingTemplate ? 'Edit Template' : 'Create Template' }}
            </h3>
            <UButton
              color="gray"
              variant="ghost"
              icon="i-heroicons-x-mark"
              @click="closeModal"
            />
          </div>
        </template>

        <div class="space-y-6">
          <!-- Template Info -->
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">
                Template Name <span class="text-red-500">*</span>
              </label>
              <UInput
                v-model="templateForm.name"
                placeholder="e.g., default, minimal, full"
                :disabled="!!editingTemplate || loading"
                class="w-full"
              />
              <p class="mt-1 text-xs text-gray-500">
                Unique template name (cannot be changed after creation)
              </p>
            </div>

            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">
                Description
              </label>
              <UTextarea
                v-model="templateForm.description"
                placeholder="Template description..."
                :disabled="loading"
                class="w-full"
                :rows="2"
              />
            </div>
          </div>

          <!-- Collection Selection -->
          <div>
            <div class="flex justify-between items-center mb-4">
              <label class="block text-sm font-medium text-gray-700">
                Select Collections <span class="text-red-500">*</span>
              </label>
              <div class="flex gap-2">
                <UButton
                  color="gray"
                  variant="outline"
                  size="xs"
                  @click="selectAllCollections"
                >
                  Select All
                </UButton>
                <UButton
                  color="gray"
                  variant="outline"
                  size="xs"
                  @click="deselectAllCollections"
                >
                  Deselect All
                </UButton>
              </div>
            </div>

            <div v-if="loadingCollections" class="flex justify-center py-4">
              <UIcon name="i-heroicons-arrow-path" class="w-5 h-5 animate-spin text-primary" />
            </div>

            <div v-else-if="availableCollections.length === 0" class="text-center py-4 text-gray-500">
              <p class="text-sm">No collections found in this domain.</p>
            </div>

            <div v-else class="space-y-2 max-h-96 overflow-y-auto border rounded-lg p-4">
              <div
                v-for="collection in availableCollections"
                :key="collection.name"
                class="flex items-center justify-between p-3 border rounded hover:bg-gray-50 transition-colors"
              >
                <div class="flex items-center gap-3 flex-1">
                  <UCheckbox
                    v-model="selectedCollections"
                    :value="collection.name"
                    :disabled="loading"
                    @update:model-value="updateCollectionSelection"
                  />
                  <div class="flex-1">
                    <div class="flex items-center gap-2">
                      <span class="font-medium">{{ collection.name }}</span>
                      <UBadge color="gray" variant="soft" size="xs">
                        {{ collection.documentCount }} documents
                      </UBadge>
                    </div>
                    <p v-if="collection.hasIndexes" class="text-xs text-gray-500 mt-1">
                      Has indexes
                    </p>
                  </div>
                </div>
                <div v-if="selectedCollections.includes(collection.name)" class="ml-4">
                  <UCheckbox
                    :model-value="collectionSelections[collection.name]?.includeIndexes ?? true"
                    @update:model-value="(val) => updateIndexSelection(collection.name, val)"
                    label="Include Indexes"
                  />
                </div>
              </div>
            </div>

            <p v-if="selectedCollections.length > 0" class="mt-2 text-sm text-gray-600">
              {{ selectedCollections.length }} collection(s) selected
            </p>
          </div>

          <!-- Error Message -->
          <UAlert
            v-if="error"
            color="red"
            variant="soft"
            :title="error"
            @close="error = null"
          />

          <!-- Actions -->
          <div class="flex justify-end gap-2 pt-4 border-t">
            <UButton
              color="gray"
              variant="outline"
              @click="closeModal"
              :disabled="loading"
            >
              Cancel
            </UButton>
            <UButton
              color="primary"
              @click="handleSubmit"
              :loading="loading"
              :disabled="!canSubmit"
            >
              {{ editingTemplate ? 'Update Template' : 'Create Template' }}
            </UButton>
          </div>
        </div>
      </UCard>
    </UModal>

    <!-- Delete Confirmation Modal -->
    <UModal v-model="showDeleteModal">
      <UCard>
        <template #header>
          <h3 class="text-lg font-semibold">Delete Template</h3>
        </template>
        <div class="space-y-4">
          <p>
            Are you sure you want to delete template <strong>{{ templateToDelete?.name }}</strong>?
          </p>
          <p class="text-sm text-gray-600">
            This action cannot be undone. The template content will be removed from MinIO.
          </p>
          <div class="flex justify-end gap-2 pt-4">
            <UButton
              color="gray"
              variant="outline"
              @click="showDeleteModal = false"
              :disabled="deleting"
            >
              Cancel
            </UButton>
            <UButton
              color="red"
              @click="handleDelete"
              :loading="deleting"
            >
              Delete
            </UButton>
          </div>
        </div>
      </UCard>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import type { Template, SelectedCollectionDto, CollectionInfo } from '~/composables/useTemplate'
import { useTemplate } from '~/composables/useTemplate'

const props = defineProps<{
  domainId: string
  domainName: string
}>()

const { getTemplatesByDomain, createTemplate, updateTemplate, deleteTemplate, getDomainCollections } = useTemplate()

const templates = ref<Template[]>([])
const loadingTemplates = ref(false)
const loadingCollections = ref(false)
const availableCollections = ref<CollectionInfo[]>([])
const showCreateModal = ref(false)
const showDeleteModal = ref(false)
const editingTemplate = ref<Template | null>(null)
const templateToDelete = ref<Template | null>(null)
const loading = ref(false)
const deleting = ref(false)
const error = ref<string | null>(null)

const templateForm = reactive({
  name: '',
  description: '',
})

const selectedCollections = ref<string[]>([])
const collectionSelections = ref<Record<string, { includeIndexes: boolean }>>({})

// Fetch templates on mount
onMounted(async () => {
  await fetchTemplates()
  await fetchCollections()
})

const fetchTemplates = async () => {
  loadingTemplates.value = true
  try {
    templates.value = await getTemplatesByDomain(props.domainId)
  } catch (err: any) {
    console.error('Failed to fetch templates:', err)
    const errorMessage = typeof err === 'string' ? err : err?.message || err?.data?.message || err?.statusMessage || 'Failed to fetch templates'
    error.value = errorMessage
    templates.value = []
  } finally {
    loadingTemplates.value = false
  }
}

const fetchCollections = async () => {
  loadingCollections.value = true
  try {
    availableCollections.value = await getDomainCollections(props.domainId)
  } catch (err: any) {
    console.error('Failed to fetch collections:', err)
    const errorMessage = typeof err === 'string' ? err : err?.message || err?.data?.message || 'Failed to fetch collections'
    error.value = errorMessage
    // Set empty array on error to prevent UI issues
    availableCollections.value = []
  } finally {
    loadingCollections.value = false
  }
}

const selectAllCollections = () => {
  selectedCollections.value = availableCollections.value.map(c => c.name)
  updateCollectionSelection()
}

const deselectAllCollections = () => {
  selectedCollections.value = []
  collectionSelections.value = {}
}

const updateCollectionSelection = () => {
  // Update collectionSelections for selected collections
  selectedCollections.value.forEach(name => {
    if (!collectionSelections.value[name]) {
      collectionSelections.value[name] = { includeIndexes: true }
    }
  })
  // Remove unselected collections
  Object.keys(collectionSelections.value).forEach(name => {
    if (!selectedCollections.value.includes(name)) {
      delete collectionSelections.value[name]
    }
  })
}

const updateIndexSelection = (collectionName: string, includeIndexes: boolean) => {
  if (collectionSelections.value[collectionName]) {
    collectionSelections.value[collectionName].includeIndexes = includeIndexes
  }
}

const canSubmit = computed(() => {
  return templateForm.name.trim() !== '' && selectedCollections.value.length > 0 && !loading.value
})

const handleSubmit = async () => {
  if (!canSubmit.value) return

  loading.value = true
  error.value = null

  try {
    const collections: SelectedCollectionDto[] = selectedCollections.value.map(name => ({
      collectionName: name,
      includeIndexes: collectionSelections.value[name]?.includeIndexes ?? true
    }))

    if (editingTemplate.value) {
      await updateTemplate(editingTemplate.value.name, {
        description: templateForm.description || undefined,
        collections
      })
    } else {
      await createTemplate({
        name: templateForm.name,
        description: templateForm.description || undefined,
        sourceDomainId: props.domainId,
        collections
      })
    }

    await fetchTemplates()
    closeModal()
  } catch (err: any) {
    error.value = err.message || 'Failed to save template'
    console.error('Failed to save template:', err)
  } finally {
    loading.value = false
  }
}

const editTemplate = (template: Template) => {
  editingTemplate.value = template
  templateForm.name = template.name
  templateForm.description = template.description || ''
  selectedCollections.value = template.collections.map(c => c.collectionName)
  collectionSelections.value = {}
  template.collections.forEach(c => {
    collectionSelections.value[c.collectionName] = {
      includeIndexes: c.includeIndexes
    }
  })
  showCreateModal.value = true
}

const confirmDeleteTemplate = (template: Template) => {
  templateToDelete.value = template
  showDeleteModal.value = true
}

const handleDelete = async () => {
  if (!templateToDelete.value) return

  deleting.value = true
  try {
    await deleteTemplate(templateToDelete.value.name)
    await fetchTemplates()
    showDeleteModal.value = false
    templateToDelete.value = null
  } catch (err: any) {
    error.value = err.message || 'Failed to delete template'
    console.error('Failed to delete template:', err)
  } finally {
    deleting.value = false
  }
}

const closeModal = () => {
  showCreateModal.value = false
  editingTemplate.value = null
  templateForm.name = ''
  templateForm.description = ''
  selectedCollections.value = []
  collectionSelections.value = {}
  error.value = null
}

const formatDate = (dateString: string): string => {
  const date = new Date(dateString)
  return date.toLocaleDateString('tr-TR', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}
</script>
