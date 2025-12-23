<script setup lang="ts">
import { MailIcon } from "vue-tabler-icons";
import { profileDD } from "@/_mockApis/headerData";
import { useAuthStore } from "@/stores/auth";
import { computed } from "vue";

const authStore = useAuthStore();

// Get user info from store
const userInfo = computed(() => authStore.userInfo);

// Get user display name
const userDisplayName = computed(() => {
  if (!userInfo.value) return 'Kullanıcı';
  
  // Try to get full name from firstName + lastName
  if (userInfo.value.given_name && userInfo.value.family_name) {
    return `${userInfo.value.given_name} ${userInfo.value.family_name}`;
  }
  
  // Try to get name from token (if available)
  const name = userInfo.value.name || userInfo.value.given_name || userInfo.value.preferred_username;
  if (name) return name;
  
  // Fallback to username
  return userInfo.value.username || 'Kullanıcı';
});

// Get user initials for avatar
const userInitials = computed(() => {
  if (!userInfo.value) return 'U';
  
  // If we have firstName and lastName, use their first letters
  if (userInfo.value.given_name && userInfo.value.family_name) {
    const first = userInfo.value.given_name[0]?.toUpperCase() || '';
    const last = userInfo.value.family_name[0]?.toUpperCase() || '';
    return (first + last) || 'U';
  }
  
  // Try to get name from token
  const name = userInfo.value.name || userInfo.value.given_name || userInfo.value.preferred_username || userInfo.value.username || '';
  
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

// Get user email
const userEmail = computed(() => {
  return userInfo.value?.email || '';
});

// Logout handler
const logOut = async function(){
  await authStore.logout();
  return navigateTo('/auth/login');
}

</script>

<template>
  <!-- ---------------------------------------------- -->
  <!-- notifications DD -->
  <!-- ---------------------------------------------- -->
  <v-menu :close-on-content-click="false">
    <template v-slot:activator="{ props }">
      <v-btn variant="text" v-bind="props" icon>
        <v-avatar size="35" color="primary">
          <span class="text-white font-weight-bold text-caption">{{ userInitials }}</span>
        </v-avatar>
      </v-btn>
    </template>
    <v-sheet rounded="md" width="360" elevation="10">
      <div class="px-8 pt-6">
        <h6 class="text-h5 font-weight-medium">Kullanıcı Profili</h6>
        <div class="d-flex align-center mt-4 pb-6">
          <v-avatar size="80" color="primary">
            <span class="text-white font-weight-bold text-h5">{{ userInitials }}</span>
          </v-avatar>
          <div class="ml-3">
            <h6 class="text-h6 mb-n1">{{ userDisplayName }}</h6>
            <span class="text-subtitle-1 font-weight-regular textSecondary" v-if="userInfo?.isAdmin"
              >Yönetici</span
            >
            <span class="text-subtitle-1 font-weight-regular textSecondary" v-else
              >Kullanıcı</span
            >
            <div class="d-flex align-center mt-1" v-if="userEmail">
              <MailIcon size="18" stroke-width="1.5" />
              <span
                class="text-subtitle-1 font-weight-regular textSecondary ml-2"
                >{{ userEmail }}</span
              >
            </div>
            <div class="d-flex align-center mt-1" v-if="userInfo?.domain_name">
              <span class="text-caption text-medium-emphasis">Domain: {{ userInfo.domain_name }}</span>
            </div>
          </div>
        </div>
        <v-divider></v-divider>
      </div>
      <perfect-scrollbar style="height: calc(100vh - 240px); max-height: 240px">
        <v-list class="py-0 theme-list" lines="two">
          <v-list-item
            v-for="item in profileDD"
            :key="item.title"
            class="py-4 px-8 custom-text-primary"
            :to="item.href"
          >
            <template v-slot:prepend>
              <v-avatar
                size="48"
                color="lightprimary"
                rounded="md"
              >
                <img
                  :src="item.avatar"
                  width="24"
                  height="24"
                  :alt="item.avatar"
                />
              </v-avatar>
            </template>
            <div>
              <h6 class="text-subtitle-1 font-weight-semibold mb-2 custom-title">
                {{ item.title }}
              </h6>
            </div>
            <p class="text-subtitle-1 font-weight-regular textSecondary">
              {{ item.subtitle }}
            </p>
          </v-list-item>
        </v-list>
      </perfect-scrollbar>
      <div class="pt-4 pb-6 px-8 text-center">
        <v-btn color="primary" variant="outlined" block @click="logOut"
          >Çıkış Yap</v-btn
        >
      </div>
    </v-sheet>
  </v-menu>
</template>
