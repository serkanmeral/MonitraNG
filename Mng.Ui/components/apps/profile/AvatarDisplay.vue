<script setup lang="ts">
import { computed, ref, watch, onMounted, onUnmounted } from 'vue';
import type { User } from '@/stores/apps/user';
import { Gender } from '@/stores/apps/user';
import { useAuthStore } from '@/stores/auth';
import { getAccessToken } from '@/services/apiService';

interface Props {
  user: User | null;
  size?: number;
}

const props = withDefaults(defineProps<Props>(), {
  size: 45,
});

// Photo blob URL (loaded with Authorization header to fix 401 in production)
// <img src="url"> does not send cookies/headers; mngui nginx proxies /api/keeper with $http_authorization only → 401.
const photoBlobUrl = ref<string | null>(null);

// Avatar color based on gender
const avatarColor = computed(() => {
  if (!props.user) return 'primary';
  
  const gender = props.user.gender;
  if (gender === Gender.Male || gender === 'Male') {
    return 'info'; // Açık mavi
  }
  if (gender === Gender.Female || gender === 'Female') {
    return 'pink'; // Pembe
  }
  return 'primary'; // Mavi (NotSpecified veya default)
});

// User initials
const userInitials = computed(() => {
  if (!props.user) return 'U';
  
  // If we have firstName and lastName, use their first letters
  if (props.user.firstName && props.user.lastName) {
    const first = props.user.firstName[0]?.toUpperCase() || '';
    const last = props.user.lastName[0]?.toUpperCase() || '';
    return (first + last) || 'U';
  }
  
  // Try to get name from firstName or lastName
  const name = props.user.firstName || props.user.lastName || props.user.username || '';
  
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

// Photo URL - her zaman same-origin path (fetch 401 önlemek için)
const photoUrl = computed(() => {
  const raw = props.user?.photoUrl || null;
  if (!raw || typeof raw !== 'string') {
    return null;
  }
  let path = raw;
  try {
    if (path.startsWith('http://') || path.startsWith('https://')) {
      const u = new URL(path);
      path = u.pathname; // sadece path, aynı origin'e fetch için
    }
  } catch {
    // URL parse hatası
  }
  if (path.startsWith('/keeper/api/')) {
    path = path.replace('/keeper/api/', '/api/keeper/');
  } else if (!path.startsWith('/api/keeper/') && path.includes('/keeper/api/')) {
    path = path.replace(/\/keeper\/api\//, '/api/keeper/');
  }
  return path.startsWith('/') ? path : `/${path}`;
});

async function loadPhoto() {
  const photoPath = photoUrl.value;
  if (!photoPath) {
    photoBlobUrl.value = null;
    return;
  }
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // Token yenileme başarısız; mevcut token ile dene
  }
  const token = authStore.accessToken ?? getAccessToken();
  if (!token) {
    photoBlobUrl.value = null;
    return;
  }
  // Query'de de gönder: img/video kaynaklı istekler veya bazı ortamlarda header gitmeyebiliyor;
  // mngui nginx access_token varsa Authorization header'a taşıyor.
  const sep = photoPath.includes('?') ? '&' : '?';
  const urlWithToken = `${photoPath}${sep}access_token=${encodeURIComponent(token)}`;
  try {
    const res = await fetch(urlWithToken, {
      headers: { Authorization: `Bearer ${token}` },
      credentials: 'include',
    });
    if (!res.ok) {
      photoBlobUrl.value = null;
      return;
    }
    const blob = await res.blob();
    if (photoBlobUrl.value) URL.revokeObjectURL(photoBlobUrl.value);
    photoBlobUrl.value = URL.createObjectURL(blob);
  } catch {
    photoBlobUrl.value = null;
  }
}

watch(photoUrl, loadPhoto, { immediate: true });
onMounted(loadPhoto);
onUnmounted(() => {
  if (photoBlobUrl.value) {
    URL.revokeObjectURL(photoBlobUrl.value);
    photoBlobUrl.value = null;
  }
});
</script>

<template>
  <v-avatar 
    :size="size" 
    :color="avatarColor"
    class="text-white font-weight-bold"
  >
    <!-- Show photo only when loaded via authenticated fetch (avoids 401 from <img src>) -->
    <img 
      v-if="photoBlobUrl" 
      :src="photoBlobUrl" 
      :alt="userInitials"
      style="object-fit: cover; width: 100%; height: 100%;"
    />
    <!-- Otherwise show initials -->
    <span v-else :style="{ fontSize: `${size * 0.4}px` }">
      {{ userInitials }}
    </span>
  </v-avatar>
</template>
