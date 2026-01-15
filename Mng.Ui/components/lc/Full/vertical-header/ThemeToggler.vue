<script setup lang="ts">
import { ref, watch } from 'vue';
import { useTheme } from 'vuetify';
import { useCustomizerStore } from '@/stores/customizer';
import { useUserPreferencesStore } from '@/stores/apps/userPreferences';
import { useAuthStore } from '@/stores/auth';
import { MoonFilledIcon, SunFilledIcon } from 'vue-tabler-icons';

const theme = useTheme();
const customizer = useCustomizerStore();
const preferencesStore = useUserPreferencesStore();
const authStore = useAuthStore();

// template skin color options
const themeColors = ref([
    {
        name: 'BLUE_THEME',
        bg: 'togglethemeBlue'
    },
    {
        name: 'DARK_BLUE_THEME',
        bg: 'togglethemeDarkBlue'
    }
]);

// Watch for theme changes and save to preferences
watch(() => customizer.actTheme, async (newTheme) => {
  // Only save if user is authenticated
  if (authStore.isAuthenticated) {
    try {
      await preferencesStore.savePreferences({
        theme: newTheme,
      });
    } catch (error) {
      // Silently handle errors - preference save failure shouldn't block theme change
      // Dataset might not exist yet, which is OK
    }
  }
});
</script>

<template>
    <div class="position-relative">
        <v-item-group mandatory v-model="customizer.actTheme" class="d-flex">
            <div v-for="theme in themeColors" :key="theme.name">
                <v-item v-slot="{ toggle }" :value="theme.name">
                    <v-btn icon :class="theme.bg" variant="text" color="primary" @click="toggle">
                        <SunFilledIcon v-if="theme.bg == 'togglethemeBlue'" :class="theme.bg + ' text-white'" height="24" />
                        <MoonFilledIcon v-if="theme.bg == 'togglethemeDarkBlue'" :class="theme.bg + ' text-white'" height="24" />
                    </v-btn>
                </v-item>
            </div>
        </v-item-group>
    </div>
</template>
