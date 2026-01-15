import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';
import { useLocaleStore } from '@/stores/locale';
import { useCustomizerStore } from '@/stores/customizer';
import type { SupportedLocale } from '@/stores/locale';

export interface UserPreferences {
  userId: string;
  locale?: SupportedLocale;
  theme?: string;
  // Future preferences can be added here
  // timezone?: string;
  // dateFormat?: string;
  // notifications?: {
  //   email?: boolean;
  //   inApp?: boolean;
  // };
}

interface UserPreferencesState {
  preferences: UserPreferences | null;
  loading: boolean;
  error: string | null;
}

const DATASET_NAME = '@user_preferences';

export const useUserPreferencesStore = defineStore('userPreferences', {
  state: (): UserPreferencesState => ({
    preferences: null,
    loading: false,
    error: null,
  }),

  getters: {
    currentPreferences: (state): UserPreferences | null => state.preferences,
  },

  actions: {
    /**
     * Load user preferences from dataset
     */
    async loadPreferences(userId: string): Promise<UserPreferences | null> {
      this.loading = true;
      this.error = null;

      try {
        // Query dataset for user preferences
        // Filter format: "userId:eq:value" or "userId:value" (simple equality)
        const filter = `userId:eq:${userId}`;
        const response = await fetchFromDataGateway(
          `/api/v1/data/${DATASET_NAME}?filter=${encodeURIComponent(filter)}&limit=1`,
          'GET'
        );

        // Response might be QueryResultDto with Data array, or direct array
        const data = response?.Data || response;
        
        if (data && Array.isArray(data) && data.length > 0) {
          const pref = data[0];
          this.preferences = {
            userId: pref.userId || userId,
            locale: pref.locale,
            theme: pref.theme,
          };
          return this.preferences;
        }

        // No preferences found
        this.preferences = null;
        return null;
      } catch (error: any) {
        // 404 means dataset doesn't exist yet - this is OK, return null
        // Other errors are also OK - we'll use default preferences
        const statusCode = error.statusCode || error.status || error.response?.status;
        if (statusCode === 404) {
          // Dataset doesn't exist yet - this is expected for first-time users
          // Don't log as error, just return null
          this.preferences = null;
          return null;
        }
        
        // For other errors, log but don't throw
        // Don't set error message to avoid console noise
        this.preferences = null;
        return null;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Save user preferences to dataset
     */
    async savePreferences(preferences: Partial<UserPreferences>): Promise<boolean> {
      this.loading = true;
      this.error = null;

      try {
        const authStore = useAuthStore();
        const userId = authStore.userInfo?.sub || authStore.userInfo?.username;

        if (!userId) {
          throw new Error('Kullanıcı ID bulunamadı');
        }

        // Check if preferences already exist
        const existing = await this.loadPreferences(userId);

        if (existing) {
          // Update existing preferences
          // Find the dataId from the existing record
          const filter = `userId:eq:${userId}`;
          const findResponse = await fetchFromDataGateway(
            `/api/v1/data/${DATASET_NAME}?filter=${encodeURIComponent(filter)}&limit=1`,
            'GET'
          );

          // Response might be QueryResultDto with Data array, or direct array
          const findData = findResponse?.Data || findResponse;
          
          if (findData && Array.isArray(findData) && findData.length > 0) {
            const existingRecord = findData[0];
            const dataId = existingRecord.__dataId;

            // Update the record
            const updateData = {
              userId,
              locale: preferences.locale ?? existing.locale,
              theme: preferences.theme ?? existing.theme,
            };

            await fetchFromDataGateway(
              `/api/v1/data/${DATASET_NAME}/${dataId}`,
              'PUT',
              updateData
            );

            this.preferences = updateData as UserPreferences;
            return true;
          }
        }

        // Create new preferences
        const newPreferences: UserPreferences = {
          userId,
          locale: preferences.locale,
          theme: preferences.theme,
        };

        await fetchFromDataGateway(
          `/api/v1/data/${DATASET_NAME}`,
          'POST',
          newPreferences
        );

        this.preferences = newPreferences;
        return true;
      } catch (error: any) {
        const statusCode = error.statusCode || error.status || error.response?.status;
        if (statusCode === 404) {
          // Dataset doesn't exist yet - user needs to create it first
          this.error = 'Dataset henüz oluşturulmamış. Lütfen önce @user_preferences dataset\'ini oluşturun.';
        } else {
          this.error = error.message || 'Tercihler kaydedilirken bir hata oluştu';
        }
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Apply preferences to UI (locale and theme stores)
     */
    applyPreferences(preferences: UserPreferences | null) {
      if (!preferences) return;

      const localeStore = useLocaleStore();
      const customizerStore = useCustomizerStore();

      // Apply locale
      if (preferences.locale && localeStore.availableLocales.includes(preferences.locale)) {
        localeStore.setLocale(preferences.locale, false); // Don't save to localStorage, we'll use dataset
      }

      // Apply theme
      if (preferences.theme) {
        customizerStore.SET_THEME(preferences.theme);
      }
    },

    /**
     * Clear preferences (on logout)
     */
    clearPreferences() {
      this.preferences = null;
      this.error = null;
    },
  },
});
