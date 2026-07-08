<script setup lang="ts">
import { ref } from 'vue';
import DiEditorSessionsPanel from '@/components/apps/document-intelligence/DiEditorSessionsPanel.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  LayoutSidebarLeftExpandIcon,
} from 'vue-tabler-icons';

defineProps<{
  treeCollapsed?: boolean;
  searchQuery?: string;
  showRefresh?: boolean;
  refreshLoading?: boolean;
  canViewEditorSessions?: boolean;
  showPermissions?: boolean;
  canCreate?: boolean;
  canUpload?: boolean;
}>();

const emit = defineEmits<{
  'update:searchQuery': [value: string];
  'toggle-tree': [];
  'search-input': [value: string | null];
  'search-enter': [];
  'search-clear': [];
  refresh: [];
  permissions: [];
  'new-folder': [];
  'new-page': [];
  'new-native-document': [];
  'new-native-sheet': [];
  'new-native-presentation': [];
  'generate-from-template': [];
  upload: [];
}>();

const { t } = useAppI18n();
const newMenuOpen = ref(false);

type NewMenuAction =
  | 'new-folder'
  | 'new-page'
  | 'new-native-document'
  | 'new-native-sheet'
  | 'new-native-presentation'
  | 'generate-from-template';

function onNewMenuAction(action: NewMenuAction) {
  newMenuOpen.value = false;
  switch (action) {
    case 'new-folder':
      emit('new-folder');
      break;
    case 'new-page':
      emit('new-page');
      break;
    case 'new-native-document':
      emit('new-native-document');
      break;
    case 'new-native-sheet':
      emit('new-native-sheet');
      break;
    case 'new-native-presentation':
      emit('new-native-presentation');
      break;
    case 'generate-from-template':
      emit('generate-from-template');
      break;
  }
}
</script>

<template>
  <div class="di-browse-toolbar d-flex align-center ga-2 px-4 py-2 border-b flex-wrap">
    <v-btn
      v-if="treeCollapsed"
      icon
      size="small"
      variant="text"
      :title="t('documentIntelligence.expand')"
      @click="emit('toggle-tree')"
    >
      <LayoutSidebarLeftExpandIcon size="18" />
    </v-btn>

    <v-text-field
      :model-value="searchQuery"
      density="compact"
      variant="solo-filled"
      flat
      hide-details
      clearable
      prepend-inner-icon="mdi-magnify"
      :placeholder="t('documentIntelligence.searchPlaceholder')"
      class="di-browse-toolbar__search flex-grow-1"
      @update:model-value="(value) => { emit('update:searchQuery', value ?? ''); emit('search-input', value); }"
      @keydown.enter="emit('search-enter')"
      @click:clear="emit('search-clear')"
    />

    <v-btn
      v-if="showRefresh"
      icon
      size="small"
      variant="text"
      :title="t('documentIntelligence.refresh')"
      :loading="refreshLoading"
      @click="emit('refresh')"
    >
      <v-icon>mdi-refresh</v-icon>
    </v-btn>

    <v-spacer />

    <DiEditorSessionsPanel v-if="canViewEditorSessions" compact class="flex-shrink-0" />

    <div class="di-browse-toolbar__actions d-flex align-center ga-2 flex-shrink-0">
      <v-btn
        v-if="showPermissions"
        icon
        size="small"
        variant="text"
        color="primary"
        :title="t('documentIntelligence.permissions.menuTitle')"
        @click="emit('permissions')"
      >
        <v-icon>mdi-shield-account-outline</v-icon>
      </v-btn>

      <v-btn
        v-if="canUpload"
        size="small"
        variant="outlined"
        color="primary"
        class="text-none"
        prepend-icon="mdi-upload"
        @click="emit('upload')"
      >
        {{ t('documentIntelligence.uploadFile') }}
      </v-btn>

      <v-menu v-if="canCreate" v-model="newMenuOpen" location="bottom end">
        <template #activator="{ props: menuProps }">
          <v-btn
            v-bind="menuProps"
            color="primary"
            variant="flat"
            size="small"
            class="text-none di-browse-toolbar__new-btn"
            prepend-icon="mdi-plus"
            append-icon="mdi-chevron-down"
          >
            {{ t('documentIntelligence.browseToolbar.new') }}
          </v-btn>
        </template>

        <v-list density="compact" min-width="260" class="py-1">
          <v-list-subheader class="text-uppercase">
            {{ t('documentIntelligence.browseToolbar.groupFolder') }}
          </v-list-subheader>
          <v-list-item
            prepend-icon="mdi-folder-plus-outline"
            :title="t('documentIntelligence.newFolder')"
            rounded="lg"
            @click="onNewMenuAction('new-folder')"
          />

          <v-divider class="my-1" />

          <v-list-subheader class="text-uppercase">
            {{ t('documentIntelligence.browseToolbar.groupContent') }}
          </v-list-subheader>
          <v-list-item
            prepend-icon="mdi-book-plus-outline"
            :title="t('documentIntelligence.newPage')"
            rounded="lg"
            @click="onNewMenuAction('new-page')"
          />
          <v-list-item
            prepend-icon="mdi-file-document-plus-outline"
            :title="t('documentIntelligence.generateFromTemplate.menu')"
            rounded="lg"
            @click="onNewMenuAction('generate-from-template')"
          />

          <v-divider class="my-1" />

          <v-list-subheader class="text-uppercase">
            {{ t('documentIntelligence.browseToolbar.groupOffice') }}
          </v-list-subheader>
          <v-list-item
            prepend-icon="mdi-file-word-box"
            :title="t('documentIntelligence.newNativeDocument')"
            rounded="lg"
            @click="onNewMenuAction('new-native-document')"
          />
          <v-list-item
            prepend-icon="mdi-file-excel-box"
            :title="t('documentIntelligence.newNativeSheet')"
            rounded="lg"
            @click="onNewMenuAction('new-native-sheet')"
          />
          <v-list-item
            prepend-icon="mdi-file-powerpoint-box"
            :title="t('documentIntelligence.newNativePresentation')"
            rounded="lg"
            @click="onNewMenuAction('new-native-presentation')"
          />
        </v-list>
      </v-menu>
    </div>
  </div>
</template>

<style scoped>
.di-browse-toolbar {
  background: rgba(var(--v-theme-surface-variant), 0.35);
  min-height: 52px;
}
.di-browse-toolbar__search {
  max-width: 420px;
  min-width: 180px;
}
.di-browse-toolbar__new-btn :deep(.v-btn__append) {
  margin-inline-start: 2px;
  opacity: 0.85;
}
</style>
