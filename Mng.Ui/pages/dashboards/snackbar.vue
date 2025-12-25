<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { InfoCircleIcon, XIcon } from 'vue-tabler-icons';
import { useAuthStore } from '@/stores/auth';

const authStore = useAuthStore();
const showSnackbar = ref(false);

// Get user display name
const userDisplayName = computed(() => {
  if (!authStore.userInfo) return '';
  
  // Try to get full name from firstName + lastName (uppercase)
  if (authStore.userInfo.given_name && authStore.userInfo.family_name) {
    return `${authStore.userInfo.given_name.toUpperCase()} ${authStore.userInfo.family_name.toUpperCase()}`;
  }
  
  // Try to get name from token (if available)
  const name = authStore.userInfo.name || authStore.userInfo.given_name || authStore.userInfo.preferred_username;
  if (name) {
    // If name contains space, uppercase all parts
    if (name.includes(' ')) {
      return name.split(' ').map(part => part.toUpperCase()).join(' ');
    }
    return name.toUpperCase();
  }
  
  // Fallback to username (uppercase)
  return (authStore.userInfo.username || '').toUpperCase();
});

onMounted(() => {
  setTimeout(() => {
    showSnackbar.value = true;
  }, 1500); // Show after 1.5 seconds
});
</script>

<template>
    <v-snackbar rounded="md" color="secondary" class="mt-n3" v-model="showSnackbar" location="top right" elevation="0">
        <div class="d-flex gap-2">
            <InfoCircleIcon size="22" />
            <div class="">
                <h5 class="text-body-1">Merhaba {{ userDisplayName }}</h5>
                <p class="text-12">Hoş geldiniz!</p>
            </div>
        </div>
        <template v-slot:actions>
            <v-btn variant="text" @click="showSnackbar = false"> <XIcon /> </v-btn>
        </template>
    </v-snackbar>
</template>
