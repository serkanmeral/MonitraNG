<script setup lang="ts">
import { computed, watch } from 'vue';
import { useUserNotesStore } from '@/stores/apps/userNotes';
import { CheckIcon } from 'vue-tabler-icons';
import AddUserNote from './AddUserNote.vue';

const notesStore = useUserNotesStore();

// Color options
const colorVariation = [
  { id: 1, color: 'warning' },
  { id: 2, color: 'secondary' },
  { id: 3, color: 'error' },
  { id: 4, color: 'success' },
  { id: 5, color: 'primary' },
  { id: 6, color: 'info' },
];

const selectedNote = computed(() => {
  return notesStore.selectedNote;
});

const noteTitle = computed({
  get: () => {
    const note = notesStore.selectedNote;
    return note?.title || '';
  },
  set: async (value: string) => {
    const note = notesStore.selectedNote;
    if (note?.__dataId) {
      await notesStore.updateNote(note.__dataId, { title: value });
    }
  }
});

// Debounce title updates
let updateTimeout: NodeJS.Timeout | null = null;
watch(noteTitle, (newValue, oldValue) => {
  if (updateTimeout) {
    clearTimeout(updateTimeout);
  }
  updateTimeout = setTimeout(async () => {
    const note = notesStore.selectedNote;
    if (note?.__dataId && newValue !== note.title && newValue !== oldValue) {
      await notesStore.updateNote(note.__dataId, { title: newValue });
    }
  }, 500);
});
</script>

<template>
  <v-sheet>
    <v-sheet class="py-3 pl-6 pr-4 d-flex align-center">
      <h4 class="text-h6">{{ $t('notes.content.editTitle') || 'Notu Düzenle' }}</h4>
      <div class="ml-auto"><AddUserNote /></div>
    </v-sheet>
    <v-divider></v-divider>
    
    <v-sheet v-if="selectedNote">
      <v-sheet class="pa-6">
        <h4 class="text-h6 mb-4">{{ $t('notes.content.changeTitle') || 'Başlığı Değiştir' }}</h4>
        <v-textarea 
          variant="outlined" 
          name="Note" 
          v-model="noteTitle"
          :placeholder="$t('notes.content.titlePlaceholder') || 'Not içeriğinizi buraya yazın...'"
          rows="10"
        ></v-textarea>

        <h4 class="text-h6 mt-4 mb-4">{{ $t('notes.content.changeColor') || 'Not Rengini Değiştir' }}</h4>
        <div class="d-flex gap-3 align-center">
          <v-btn
            icon
            v-for="btcolor in colorVariation"
            :key="btcolor.id"
            size="x-small"
            :color="btcolor.color"
            @click="notesStore.updateNote(selectedNote.__dataId!, { color: btcolor.color })"
          >
            <CheckIcon width="16" v-if="selectedNote.color === btcolor.color" />
          </v-btn>
        </div>
      </v-sheet>
    </v-sheet>
    
    <v-sheet v-else class="pa-6">
      <v-alert 
        type="info" 
        :title="$t('notes.content.noNoteSelectedTitle') || 'Bilgi'" 
        :text="$t('notes.content.noNoteSelectedText') || 'Lütfen bir not seçin'">
      </v-alert>
    </v-sheet>
  </v-sheet>
</template>
