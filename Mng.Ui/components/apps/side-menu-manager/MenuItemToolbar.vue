<script setup lang="ts">
import { ref } from 'vue';
import { PlusIcon, RefreshIcon, SearchIcon } from 'vue-tabler-icons';

const emit = defineEmits<{
  'new-header': [];
  'new-item': [];
  'search': [query: string];
  'refresh': [];
}>();

defineProps<{
  loading?: boolean;
}>();

const searchQuery = ref('');
const searchInput = ref<HTMLInputElement | null>(null);

const handleSearch = (value: string) => {
  searchQuery.value = value;
  emit('search', value);
};

const clearSearch = () => {
  searchQuery.value = '';
  emit('search', '');
};
</script>

<template>
  <v-card elevation="2">
    <v-card-text class="pa-4">
      <div class="d-flex align-center flex-wrap gap-2">
        <!-- New Item Buttons -->
        <v-btn
          color="primary"
          variant="flat"
          prepend-icon="PlusIcon"
          @click="emit('new-item')"
          :disabled="loading"
        >
          <template #prepend>
            <PlusIcon size="20" />
          </template>
          Yeni Menu Item
        </v-btn>

        <v-btn
          color="secondary"
          variant="flat"
          prepend-icon="PlusIcon"
          @click="emit('new-header')"
          :disabled="loading"
        >
          <template #prepend>
            <PlusIcon size="20" />
          </template>
          Yeni Header
        </v-btn>

        <v-divider vertical class="mx-2"></v-divider>

        <!-- Search Input -->
        <v-text-field
          v-model="searchQuery"
          @input="handleSearch($event.target.value)"
          @click:clear="clearSearch"
          placeholder="Menu item ara..."
          prepend-inner-icon="SearchIcon"
          variant="outlined"
          density="compact"
          clearable
          hide-details
          style="max-width: 300px;"
        >
          <template #prepend-inner>
            <SearchIcon size="20" />
          </template>
        </v-text-field>

        <v-spacer></v-spacer>

        <!-- Refresh Button -->
        <v-btn
          color="default"
          variant="outlined"
          icon
          @click="emit('refresh')"
          :loading="loading"
        >
          <RefreshIcon size="20" />
          <template #loader>
            <v-progress-circular size="20" indeterminate></v-progress-circular>
          </template>
        </v-btn>
      </div>
    </v-card-text>
  </v-card>
</template>
