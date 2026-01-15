<script setup lang="ts">
import { computed } from 'vue';
import type { User } from '@/stores/apps/user';
import { Gender } from '@/stores/apps/user';

interface Props {
  user: User | null;
  size?: number;
}

const props = withDefaults(defineProps<Props>(), {
  size: 45,
});

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

// Photo URL - normalize to frontend proxy format
const photoUrl = computed(() => {
  const url = props.user?.photoUrl || null;
  if (!url) {
    return null;
  }
  
  // Normalize URL: if it starts with /keeper/api/, convert to /api/keeper/
  // Backend returns /keeper/api/user/{userId}/photo
  // Frontend proxy expects /api/keeper/user/{userId}/photo
  let normalizedUrl = url;
  if (url.startsWith('/keeper/api/')) {
    normalizedUrl = url.replace('/keeper/api/', '/api/keeper/');
  }
  
  return normalizedUrl;
});

// Image error handler
const handleImageError = (event: Event) => {
  // Silently handle image load errors
  // Image will fallback to initials display
};

// Image load handler
const handleImageLoad = () => {
  // Image loaded successfully
};
</script>

<template>
  <v-avatar 
    :size="size" 
    :color="avatarColor"
    class="text-white font-weight-bold"
  >
    <!-- If photoUrl exists, show photo -->
    <img 
      v-if="photoUrl" 
      :src="photoUrl" 
      :alt="userInitials"
      style="object-fit: cover; width: 100%; height: 100%;"
      @error="handleImageError"
      @load="handleImageLoad"
    />
    <!-- Otherwise show initials -->
    <span v-else :style="{ fontSize: `${size * 0.4}px` }">
      {{ userInitials }}
    </span>
  </v-avatar>
</template>
