<script setup lang="ts">
import { ref, shallowRef, computed } from 'vue';
import { useCustomizerStore } from '@/stores/customizer';
import { useAuthStore } from '@/stores/auth';
import sidebarItems from './sidebarItem';
import { PowerIcon } from 'vue-tabler-icons';

const customizer = useCustomizerStore();
const authStore = useAuthStore();
const sidebarMenu = shallowRef(sidebarItems);

// Get user display name
const userDisplayName = computed(() => {
  if (!authStore.userInfo) return 'Kullanıcı';
  
  // Try to get full name from firstName + lastName
  if (authStore.userInfo.given_name && authStore.userInfo.family_name) {
    return `${authStore.userInfo.given_name} ${authStore.userInfo.family_name}`;
  }
  
  // Try to get name from token (if available)
  const name = authStore.userInfo.name || authStore.userInfo.given_name || authStore.userInfo.preferred_username;
  if (name) return name;
  
  // Fallback to username
  return authStore.userInfo.username || 'Kullanıcı';
});

// Get user initials for avatar
const userInitials = computed(() => {
  if (!authStore.userInfo) return 'U';
  
  // If we have firstName and lastName, use their first letters
  if (authStore.userInfo.given_name && authStore.userInfo.family_name) {
    const first = authStore.userInfo.given_name[0]?.toUpperCase() || '';
    const last = authStore.userInfo.family_name[0]?.toUpperCase() || '';
    return (first + last) || 'U';
  }
  
  // Try to get name from token
  const name = authStore.userInfo.name || authStore.userInfo.given_name || authStore.userInfo.preferred_username || authStore.userInfo.username || '';
  
  // If name contains space, get first letters of first and last word
  if (name.includes(' ')) {
    const parts = name.trim().split(' ').filter(p => p.length > 0);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return parts[0][0].toUpperCase();
  }
  
  // If single word, get first 2 letters
  if (name.length >= 2) {
    return name.substring(0, 2).toUpperCase();
  }
  
  return name[0]?.toUpperCase() || 'U';
});

// Logout handler
const handleLogout = async () => {
  await authStore.logout();
  navigateTo('/auth/login');
};
</script>

<template>
    <v-navigation-drawer
        left
        v-model="customizer.Sidebar_drawer"
        elevation="0"
        rail-width="75"
        app
        class="leftSidebar"
        :rail="customizer.mini_sidebar"
        expand-on-hover width="270"
    >
        <!-- ---------------------------------------------- -->
        <!---Navigation -->
        <!-- ---------------------------------------------- -->
        <perfect-scrollbar class="scrollnavbar">
            <div class="profile">
                <div class="profile-pic profile-pic py-7 px-3">
                    <v-avatar size="45" color="primary">
                        <span class="text-white font-weight-bold">{{ userInitials }}</span>
                    </v-avatar>
                </div>
                <div class="profile-name d-flex align-center px-3">
                    <h5 class="text-white font-weight-medium">{{ userDisplayName }}</h5>
                    <div class="ml-auto profile-logout">
                        <v-btn 
                            variant="text" 
                            icon 
                            rounded="md" 
                            color="white" 
                            @click="handleLogout"
                        >
                            <PowerIcon size="22"/>
                            <v-tooltip activator="parent" location="top">Çıkış Yap</v-tooltip>
                        </v-btn>
                    </div>
                </div>
            </div>
            <v-list class="py-5 px-4 bg-muted" density="compact">
                <!---Menu Loop -->
                <template v-for="(item, i) in sidebarMenu">
                    <!---Item Sub Header -->
                    <LcFullVerticalSidebarNavGroup :item="item" v-if="item.header" :key="item.title" />
                    <!---If Has Child -->
                    <LcFullVerticalSidebarNavCollapse class="leftPadding" :item="item" :level="0" v-else-if="item.children" />
                    <!---Single Item-->
                    <LcFullVerticalSidebarNavItem :item="item" v-else class="leftPadding" />
                    <!---End Single Item-->
                </template>
            </v-list>
        </perfect-scrollbar>
    </v-navigation-drawer>
</template>
