<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useUserNotesStore } from '@/stores/apps/userNotes';
import { TrashIcon } from 'vue-tabler-icons';

const notesStore = useUserNotesStore();

onMounted(async () => {
  await notesStore.fetchNotes();
});

const searchValue = ref('');
const filteredNotes = computed(() => {
  return notesStore.filteredNotes(searchValue.value);
});

const handleSelectNote = (noteId: string | undefined) => {
  if (noteId) {
    notesStore.selectNote(noteId);
  }
};

const handleDeleteNote = async (noteId: string) => {
  if (!noteId) return;
  try {
    await notesStore.deleteNote(noteId);
  } catch (error) {
    // Error is handled by store
  }
};
</script>

<template>
  <div class="pa-6">
    <h4 class="text-h6 mb-4">{{ $t('notes.listing.title') || 'Tüm Notlar' }}</h4>

    <div class="mb-5">
      <v-text-field
        variant="outlined"
        v-model="searchValue"
        append-inner-icon="mdi-magnify"
        :placeholder="$t('notes.listing.searchPlaceholder') || 'Notlarda Ara'"
        hide-details
        density="compact"
      ></v-text-field>
    </div>

    <v-sheet
      v-for="note in filteredNotes"
      :key="note.__dataId"
      :class="[
        'note-sheet pa-6 pb-4 rounded-md cursor-pointer mb-4 bg-light' + note.color,
        { 'note-selected': notesStore.selectedNoteId === note.__dataId }
      ]"
      @click="handleSelectNote(note.__dataId)"
    >
      <h6 :class="'text-h6 text-truncate text-' + note.color">{{ note.title }}</h6>
      <div class="d-flex mt-3 align-center">
        <small class="text-subtitle-2 opacity-25">
          {{ note.createdAt ? new Date(note.createdAt).toLocaleDateString() : '' }}
        </small>
        <v-btn 
          icon 
          variant="text" 
          class="ml-auto" 
          size="x-small" 
          @click.stop="handleDeleteNote(note.__dataId!)"
        >
          <v-tooltip activator="parent" location="top">
            {{ $t('notes.listing.deleteNote') || 'Notu Sil' }}
          </v-tooltip>
          <TrashIcon size="18" />
        </v-btn>
      </div>
    </v-sheet>
    
    <v-sheet v-if="filteredNotes.length === 0 && !notesStore.loading">
      <v-alert type="info" :title="$t('notes.listing.noNotesTitle') || 'Bilgi'" 
        :text="$t('notes.listing.noNotesText') || 'Aradığınız not bulunamadı'">
      </v-alert>
    </v-sheet>
    
    <v-sheet v-if="notesStore.loading">
      <v-progress-linear indeterminate color="primary"></v-progress-linear>
    </v-sheet>
  </div>
</template>

<style lang="scss" scoped>
.note-sheet {
  transition: 0.1s ease-in;
  &:hover {
    transform: scale(1.02);
  }
  &.note-selected {
    border: 2px solid rgba(var(--v-theme-primary), 0.5) !important;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  }
}
</style>
