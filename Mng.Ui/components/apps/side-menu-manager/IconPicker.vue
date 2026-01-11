<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { getTablerIconList, popularMdiIcons, getTablerIconComponent, getMdiIconClass } from '@/utils/icons/iconUtils';
import { SearchIcon } from 'vue-tabler-icons';

const props = defineProps<{
  iconType: 'mdi' | 'tabler';
  iconName: string;
}>();

const emit = defineEmits<{
  'icon-select': [iconName: string, iconType: 'mdi' | 'tabler'];
}>();

const currentIconType = ref<'mdi' | 'tabler'>(props.iconType || 'tabler');
const searchQuery = ref('');
const showDialog = ref(false);

// Update currentIconType when prop changes
watch(() => props.iconType, (newType) => {
  if (newType) {
    currentIconType.value = newType;
  }
});

// Computed: Filtered icons based on search
const filteredTablerIcons = computed(() => {
  if (!searchQuery.value.trim()) {
    return getTablerIconList().slice(0, 100); // Limit to first 100 for performance
  }
  
  const query = searchQuery.value.toLowerCase().trim();
  return getTablerIconList().filter((icon) => 
    icon.toLowerCase().includes(query)
  ).slice(0, 100);
});

const filteredMdiIcons = computed(() => {
  if (!searchQuery.value.trim()) {
    return popularMdiIcons.slice(0, 100); // Limit to first 100 for performance
  }
  
  const query = searchQuery.value.toLowerCase().trim();
  return popularMdiIcons.filter((icon) => 
    icon.toLowerCase().includes(query)
  ).slice(0, 100);
});

const currentIcons = computed(() => {
  return currentIconType.value === 'tabler' ? filteredTablerIcons.value : filteredMdiIcons.value;
});

// Handle icon selection
const selectIcon = (iconName: string) => {
  emit('icon-select', iconName, currentIconType.value);
  showDialog.value = false;
  searchQuery.value = '';
};

// Get icon component for preview (used in template)
const getIconPreviewComponent = (iconName: string) => {
  if (currentIconType.value === 'tabler') {
    return getTablerIconComponent(iconName);
  }
  return null;
};

// Toggle icon type
const toggleIconType = () => {
  currentIconType.value = currentIconType.value === 'tabler' ? 'mdi' : 'tabler';
  searchQuery.value = '';
};

// Format icon name for display
const formatIconName = (iconName: string): string => {
  return iconName
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, (str) => str.toUpperCase())
    .trim();
};
</script>

<template>
  <div class="icon-picker">
      <!-- Current Icon Display -->
    <div class="d-flex align-center gap-2 mb-2">
      <v-text-field
        :model-value="iconName || 'Icon seçilmedi'"
        label="Seçili Icon"
        variant="outlined"
        density="compact"
        readonly
        @click="showDialog = true"
        style="cursor: pointer;"
      >
        <template #append-inner>
          <SearchIcon size="20" style="cursor: pointer;" @click="showDialog = true" />
        </template>
      </v-text-field>

      <v-btn
        variant="outlined"
        @click="showDialog = true"
      >
        Icon Seç
      </v-btn>
    </div>

    <!-- Icon Preview (if selected) -->
    <div v-if="iconName" class="d-flex align-center gap-2 mb-2">
      <span class="text-caption text-medium-emphasis">Preview:</span>
      <div class="icon-preview">
        <component
          v-if="iconType === 'tabler' && getTablerIconComponent(iconName)"
          :is="getTablerIconComponent(iconName)"
          size="24"
        />
        <i
          v-else-if="iconType === 'mdi'"
          :class="getMdiIconClass(iconName)"
          style="font-size: 24px;"
        ></i>
        <span v-else class="text-caption text-medium-emphasis">{{ iconName }}</span>
      </div>
      <span class="text-caption">{{ iconType === 'tabler' ? 'Tabler' : 'MDI' }}</span>
    </div>

    <!-- Icon Picker Dialog -->
    <v-dialog v-model="showDialog" max-width="800" scrollable>
      <v-card>
        <v-card-title class="d-flex align-center">
          <span>Icon Seç</span>
          <v-spacer></v-spacer>
          <v-chip
            :color="currentIconType === 'tabler' ? 'primary' : 'secondary'"
            variant="flat"
            class="mr-2"
          >
            {{ currentIconType === 'tabler' ? 'Tabler Icons' : 'Material Icons' }}
          </v-chip>
          <v-btn
            icon
            variant="text"
            @click="showDialog = false"
          >
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>

        <v-divider></v-divider>

        <v-card-text>
          <!-- Search and Type Toggle -->
          <div class="d-flex align-center gap-2 mb-4">
            <v-text-field
              v-model="searchQuery"
              label="Icon ara..."
              variant="outlined"
              density="compact"
              prepend-inner-icon="SearchIcon"
              clearable
              hide-details
            >
              <template #prepend-inner>
                <SearchIcon size="20" />
              </template>
            </v-text-field>

            <v-btn-toggle
              v-model="currentIconType"
              variant="outlined"
              mandatory
              @update:model-value="toggleIconType"
            >
              <v-btn value="tabler" size="small">
                Tabler
              </v-btn>
              <v-btn value="mdi" size="small">
                MDI
              </v-btn>
            </v-btn-toggle>
          </div>

          <!-- Icons Grid -->
          <div class="icons-grid">
            <div
              v-for="icon in currentIcons"
              :key="icon"
              class="icon-item"
              :class="{ 'icon-item-selected': icon === iconName && currentIconType === iconType }"
              @click="selectIcon(icon)"
            >
              <div class="icon-item-preview">
                <component
                  v-if="currentIconType === 'tabler' && getIconPreviewComponent(icon)"
                  :is="getIconPreviewComponent(icon)"
                  size="24"
                />
                <i
                  v-else-if="currentIconType === 'mdi'"
                  :class="getMdiIconClass(icon)"
                  style="font-size: 24px;"
                ></i>
                <span v-else class="text-caption">?</span>
              </div>
              <div class="icon-item-name">
                {{ formatIconName(icon) }}
              </div>
            </div>
          </div>

          <!-- Empty State -->
          <div v-if="currentIcons.length === 0" class="text-center pa-8 text-medium-emphasis">
            Arama sonucu bulunamadı
          </div>
        </v-card-text>

        <v-divider></v-divider>

        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="showDialog = false">
            Kapat
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.icon-picker {
  width: 100%;
}

.icon-preview {
  padding: 8px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 4px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 48px;
  min-height: 48px;
}

.icons-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
  gap: 8px;
  max-height: 400px;
  overflow-y: auto;
}

.icon-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 12px 8px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.icon-item:hover {
  background-color: rgba(var(--v-theme-primary), 0.08);
  border-color: rgba(var(--v-theme-primary), 0.5);
}

.icon-item-selected {
  background-color: rgba(var(--v-theme-primary), 0.12);
  border-color: rgb(var(--v-theme-primary));
}

.icon-item-preview {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 32px;
  margin-bottom: 4px;
}

.icon-item-name {
  font-size: 10px;
  text-align: center;
  word-break: break-word;
  color: rgba(var(--v-theme-on-surface), 0.7);
}
</style>
