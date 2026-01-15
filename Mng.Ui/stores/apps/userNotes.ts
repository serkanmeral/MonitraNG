import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

export interface UserNote {
  __dataId?: string;
  userId: string;
  title: string;
  color?: string;
  createdAt?: Date | string;
  updatedAt?: Date | string;
}

interface UserNotesState {
  notes: UserNote[];
  selectedNoteId: string | null;
  loading: boolean;
  error: string | null;
}

const DATASET_NAME = '@user_notes';

export const useUserNotesStore = defineStore('userNotes', {
  state: (): UserNotesState => ({
    notes: [],
    selectedNoteId: null,
    loading: false,
    error: null,
  }),

  getters: {
    selectedNote: (state): UserNote | null => {
      if (!state.selectedNoteId) return null;
      const found = state.notes.find(note => note.__dataId === state.selectedNoteId);
      return found || null;
    },
    filteredNotes: (state) => (searchTerm: string) => {
      if (!searchTerm) return state.notes;
      const term = searchTerm.toLowerCase();
      return state.notes.filter(note => 
        note.title?.toLowerCase().includes(term)
      );
    },
  },

  actions: {
    /**
     * Fetch all notes for the current user
     */
    async fetchNotes(): Promise<void> {
      this.loading = true;
      this.error = null;

      try {
        const authStore = useAuthStore();
        const userId = authStore.userInfo?.sub || authStore.userInfo?.username;

        if (!userId) {
          throw new Error('Kullanıcı ID bulunamadı');
        }

        // Filter by userId and sort by createdAt descending
        const filter = `userId:eq:${userId}`;
        const response = await fetchFromDataGateway(
          `/api/v1/data/${DATASET_NAME}?filter=${encodeURIComponent(filter)}&sort=createdAt:desc&limit=1000`,
          'GET'
        );

        // Response might be QueryResultDto with Data array, or direct array
        const data = response?.Data || response;
        
        if (data && Array.isArray(data)) {
          this.notes = data.map((note: any) => ({
            __dataId: note.__dataId,
            userId: note.userId,
            title: note.title || '',
            color: note.color || 'primary',
            createdAt: note.createdAt ? new Date(note.createdAt) : new Date(),
            updatedAt: note.updatedAt ? new Date(note.updatedAt) : undefined,
          }));
        } else {
          this.notes = [];
        }
      } catch (error: any) {
        const statusCode = error.statusCode || error.status || error.response?.status;
        if (statusCode === 404) {
          // Dataset doesn't exist yet - this is OK, return empty array
          this.notes = [];
        } else {
          this.error = error.message || 'Notlar yüklenirken bir hata oluştu';
          throw error;
        }
      } finally {
        this.loading = false;
      }
    },

    /**
     * Add a new note
     */
    async addNote(noteData: { title: string; color?: string }): Promise<UserNote> {
      this.loading = true;
      this.error = null;

      try {
        const authStore = useAuthStore();
        const userId = authStore.userInfo?.sub || authStore.userInfo?.username;

        if (!userId) {
          throw new Error('Kullanıcı ID bulunamadı');
        }

        const now = new Date();
        const newNote: Omit<UserNote, '__dataId'> = {
          userId,
          title: noteData.title,
          color: noteData.color || 'primary',
          createdAt: now,
          updatedAt: now,
        };

        const response = await fetchFromDataGateway(
          `/api/v1/data/${DATASET_NAME}`,
          'POST',
          newNote
        );

        // Response should contain the created note with __dataId
        const createdNote: UserNote = {
          __dataId: response.__dataId || response._id,
          userId: response.userId || userId,
          title: response.title || noteData.title,
          color: response.color || noteData.color || 'primary',
          createdAt: response.createdAt ? new Date(response.createdAt) : now,
          updatedAt: response.updatedAt ? new Date(response.updatedAt) : now,
        };

        // Add to local state
        this.notes.unshift(createdNote);
        
        // Select the newly created note
        this.selectedNoteId = createdNote.__dataId || null;

        return createdNote;
      } catch (error: any) {
        this.error = error.message || 'Not eklenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Update an existing note
     */
    async updateNote(noteId: string, updates: { title?: string; color?: string }): Promise<void> {
      this.loading = true;
      this.error = null;

      try {
        const note = this.notes.find(n => n.__dataId === noteId);
        if (!note) {
          throw new Error('Not bulunamadı');
        }

        const updateData: Partial<UserNote> = {
          ...updates,
          updatedAt: new Date(),
        };

        await fetchFromDataGateway(
          `/api/v1/data/${DATASET_NAME}/${noteId}`,
          'PUT',
          updateData
        );

        // Update local state
        const index = this.notes.findIndex(n => n.__dataId === noteId);
        if (index !== -1) {
          this.notes[index] = {
            ...this.notes[index],
            ...updates,
            updatedAt: new Date(),
          };
        }
      } catch (error: any) {
        this.error = error.message || 'Not güncellenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Delete a note
     */
    async deleteNote(noteId: string): Promise<void> {
      this.loading = true;
      this.error = null;

      try {
        await fetchFromDataGateway(
          `/api/v1/data/${DATASET_NAME}/${noteId}`,
          'DELETE'
        );

        // Remove from local state
        const index = this.notes.findIndex(n => n.__dataId === noteId);
        if (index !== -1) {
          this.notes.splice(index, 1);
        }

        // Clear selection if deleted note was selected
        if (this.selectedNoteId === noteId) {
          this.selectedNoteId = null;
        }
      } catch (error: any) {
        this.error = error.message || 'Not silinirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Select a note
     */
    selectNote(noteId: string | null): void {
      this.selectedNoteId = noteId;
    },

    /**
     * Clear all notes (on logout)
     */
    clearNotes(): void {
      this.notes = [];
      this.selectedNoteId = null;
      this.error = null;
    },
  },
});
