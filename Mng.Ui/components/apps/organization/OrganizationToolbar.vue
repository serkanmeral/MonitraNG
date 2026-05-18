<script setup lang="ts">
import { ref } from 'vue';
import { PlusIcon, RefreshIcon, SearchIcon } from 'vue-tabler-icons';

const emit = defineEmits<{
  'new-item': [];
  'new-asset': [];
  'search': [query: string];
  'refresh': [];
}>();

defineProps<{
  loading?: boolean;
  /** Ekleme butonları sadece is_manager veya is_admin için gösterilir */
  canEdit?: boolean;
}>();

const searchQuery = ref('');

function handleSearch() {
  emit('search', searchQuery.value);
}

function clearSearch() {
  searchQuery.value = '';
  emit('search', '');
}
</script>

<template>
  <v-card elevation="2">
    <v-card-text class="pa-4">
      <div class="d-flex align-center flex-wrap gap-2">
        <template v-if="canEdit">
          <v-btn color="primary" variant="flat" @click="emit('new-item')" :disabled="loading">
            <template #prepend>
              <PlusIcon size="20" />
            </template>
            Yeni Item
          </v-btn>
          <v-btn color="secondary" variant="flat" @click="emit('new-asset')" :disabled="loading">
            <template #prepend>
              <PlusIcon size="20" />
            </template>
            Yeni Asset
          </v-btn>
          <v-divider vertical class="mx-2" />
        </template>
        <v-text-field
          v-model="searchQuery"
          @update:model-value="handleSearch"
          placeholder="Ara (ad, açıklama)..."
          variant="outlined"
          density="compact"
          hide-details
          clearable
          @click:clear="clearSearch"
          style="max-width: 280px;"
        >
          <template #prepend-inner>
            <SearchIcon size="20" />
          </template>
        </v-text-field>
        <v-spacer />
        <v-btn color="default" variant="outlined" icon @click="emit('refresh')" :loading="loading">
          <RefreshIcon size="20" />
        </v-btn>
      </div>
    </v-card-text>
  </v-card>
</template>
