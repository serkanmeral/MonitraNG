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

        // Response might be DataResponseDto { Data: [] }, { data: [] } veya doğrudan dizi
        const data = response?.Data ?? response?.data ?? response;

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

        // API yanıtı: { Data: entity } / { data: entity } (Pascal/camel) veya doğrudan entity
        const raw =
          response && typeof response === 'object' && !Array.isArray(response)
            ? (response.Data ?? response.data ?? response)
            : response;

        const entity = raw && typeof raw === 'object' ? raw : {};
        const id =
          entity.__dataId ??
          entity.DataId ??
          entity.dataId ??
          entity._id ??
          entity.id;

        const createdNote: UserNote = {
          __dataId: id ?? `temp-${Date.now()}`,
          userId: (entity.userId ?? userId) as string,
          title: (entity.title ?? noteData.title) as string,
          color: (entity.color ?? noteData.color ?? 'primary') as string,
          createdAt: entity.createdAt ? new Date(entity.createdAt) : now,
          updatedAt: entity.updatedAt ? new Date(entity.updatedAt) : now,
        };

        // Listeye ekle ve seçili yap — POST başarılı, modal kapansın, not hemen görünsün
        this.notes.unshift(createdNote);
        this.selectedNoteId = createdNote.__dataId;

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

        // Listeyi sunucudan yenile ki güncel veri görünsün
        await this.fetchNotes();
        this.selectedNoteId = noteId;
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

        const wasSelected = this.selectedNoteId === noteId;

        // Listeyi sunucudan yenile ki güncel veri görünsün
        await this.fetchNotes();

        if (wasSelected) {
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
