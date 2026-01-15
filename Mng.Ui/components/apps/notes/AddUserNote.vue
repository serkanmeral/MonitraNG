<script setup lang="ts">
import { ref } from 'vue';
import { useUserNotesStore } from '@/stores/apps/userNotes';
import { CheckIcon } from 'vue-tabler-icons';

const notesStore = useUserNotesStore();

const dialog = ref(false);
const title = ref('');
const color = ref('primary');

// Color options
const colorVariation = [
  { id: 1, color: 'warning' },
  { id: 2, color: 'secondary' },
  { id: 3, color: 'error' },
  { id: 4, color: 'success' },
  { id: 5, color: 'primary' },
  { id: 6, color: 'info' },
];

function setColor(selectedColor: string) {
  color.value = selectedColor;
}

async function addNote() {
  if (!title.value.trim()) {
    return;
  }

  try {
    await notesStore.addNote({
      title: title.value.trim(),
      color: color.value,
    });
    
    // Reset form
    dialog.value = false;
    title.value = '';
    color.value = 'primary';
  } catch (error) {
    // Error is handled by store
  }
}

function closeDialog() {
  dialog.value = false;
  title.value = '';
  color.value = 'primary';
}
</script>

<template>
  <v-sheet>
    <v-btn color="primary" @click="dialog = true">
      {{ $t('notes.add.button') || 'Not Ekle' }}
    </v-btn>

    <v-dialog v-model="dialog" max-width="500" @update:model-value="!dialog && closeDialog()">
      <v-card>
        <v-card-text>
          <h4 class="text-h6 mb-4">{{ $t('notes.add.title') || 'Not Ekle' }}</h4>
          <v-textarea 
            variant="outlined" 
            name="Note" 
            v-model="title"
            :placeholder="$t('notes.add.placeholder') || 'Not içeriğinizi buraya yazın...'"
            rows="5"
          ></v-textarea>
          
          <h4 class="text-h6 mt-4 mb-4">{{ $t('notes.add.selectColor') || 'Not Rengi Seç' }}</h4>
          <div class="d-flex gap-3 align-center">
            <v-btn
              icon
              v-for="btcolor in colorVariation"
              :key="btcolor.id"
              size="x-small"
              :color="btcolor.color"
              @click="setColor(btcolor.color)"
            >
              <CheckIcon width="16" v-if="color === btcolor.color" />
            </v-btn>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-btn color="primary" @click="addNote" :disabled="!title.trim() || notesStore.loading">
            {{ $t('notes.add.save') || 'Kaydet' }}
          </v-btn>
          <v-btn color="primary" variant="text" @click="closeDialog">
            {{ $t('notes.add.cancel') || 'İptal' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-sheet>
</template>
