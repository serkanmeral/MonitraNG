<script setup lang="ts">
import { ref, watch } from 'vue';
import { UploadIcon, XIcon } from 'vue-tabler-icons';
import { fetchFromMngKeeper } from '@/services/apiService';

interface Props {
  currentPhotoUrl?: string | null;
  userId?: string | null;
}

const props = withDefaults(defineProps<Props>(), {
  currentPhotoUrl: null,
  userId: null,
});

const emit = defineEmits<{
  uploaded: [photoUrl: string];
}>();

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params);
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key, params);
  }
  return key;
};

const fileInput = ref<HTMLInputElement | null>(null);
const previewUrl = ref<string | null>(props.currentPhotoUrl || null);
const isUploading = ref(false);
const error = ref<string | null>(null);
const isDragging = ref(false);

// Watch for changes in currentPhotoUrl prop
watch(() => props.currentPhotoUrl, (newUrl) => {
  if (newUrl) {
    previewUrl.value = newUrl;
  }
}, { immediate: true });

// File validation
const validateFile = (file: File): string | null => {
  // Max size: 5MB
  const maxSize = 5 * 1024 * 1024; // 5MB in bytes
  if (file.size > maxSize) {
    return t('profile.photo.errors.maxSize');
  }
  
  // Allowed formats
  const allowedFormats = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp'];
  if (!allowedFormats.includes(file.type)) {
    return t('profile.photo.errors.invalidFormat');
  }
  
  // Max dimensions: 2000x2000px (check after load)
  return null;
};

// Handle file select
const handleFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files.length > 0) {
    handleFile(target.files[0]);
  }
};

// Handle drag and drop
const handleDragOver = (event: DragEvent) => {
  event.preventDefault();
  isDragging.value = true;
};

const handleDragLeave = () => {
  isDragging.value = false;
};

const handleDrop = (event: DragEvent) => {
  event.preventDefault();
  isDragging.value = false;
  
  if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
    handleFile(event.dataTransfer.files[0]);
  }
};

// Handle file
const handleFile = async (file: File) => {
  error.value = null;
  
  // Validate file
  const validationError = validateFile(file);
  if (validationError) {
    error.value = validationError;
    return;
  }
  
  // Create preview
  const reader = new FileReader();
  reader.onload = (e) => {
    previewUrl.value = e.target?.result as string;
    
    // Check image dimensions
    const img = new Image();
    img.onload = () => {
      if (img.width > 2000 || img.height > 2000) {
        error.value = t('profile.photo.errors.maxDimensions');
        previewUrl.value = null;
        return;
      }
      
      // Upload file
      uploadFile(file);
    };
    img.onerror = () => {
      error.value = t('profile.photo.errors.invalidImage');
      previewUrl.value = null;
    };
    img.src = e.target?.result as string;
  };
  reader.onerror = () => {
    error.value = t('profile.photo.errors.readError');
  };
  reader.readAsDataURL(file);
};

// Upload file to MinIO
const uploadFile = async (file: File) => {
  isUploading.value = true;
  error.value = null;
  
  try {
    if (!props.userId) {
      throw new Error('User ID is required for photo upload');
    }
    
    // Create FormData
    const formData = new FormData();
    formData.append('file', file);
    
    // Upload to MinIO via MngKeeper API
    // Endpoint: POST /api/keeper/user/{userId}/photo
    const response = await fetchFromMngKeeper(`/user/${props.userId}/photo`, 'POST', formData, {
      // Don't set Content-Type header - browser will set it with boundary for multipart/form-data
    });
    
    console.log('[PhotoUpload] Upload response:', response);
    
    // Check for photoUrl in response
    if (response && (response.photoUrl || response.url || response.fileUrl)) {
      const photoUrl = response.photoUrl || response.url || response.fileUrl;
      console.log('[PhotoUpload] Photo uploaded successfully, photoUrl:', photoUrl);
      previewUrl.value = photoUrl;
      emit('uploaded', photoUrl);
    } else {
      // If response exists but no photoUrl, check for error
      if (response && response.error) {
        throw new Error(response.error || t('profile.photo.errors.uploadFailed'));
      }
      // If response is empty or unexpected format, log it
      console.error('[PhotoUpload] Unexpected response format:', response);
      throw new Error(t('profile.photo.errors.uploadFailed'));
    }
  } catch (err: any) {
    console.error('[PhotoUpload] Error uploading photo:', err);
    console.error('[PhotoUpload] Error details:', {
      message: err.message,
      statusCode: err.statusCode,
      statusMessage: err.statusMessage,
      data: err.data,
      response: err.response,
    });
    
    // Extract error message from various possible locations
    let errorMessage = t('profile.photo.errors.uploadFailed');
    if (err.message) {
      errorMessage = err.message;
    } else if (err.data?.error) {
      errorMessage = err.data.error;
    } else if (err.data?.errorDescription) {
      errorMessage = err.data.errorDescription;
    } else if (err.statusMessage) {
      errorMessage = err.statusMessage;
    }
    
    error.value = errorMessage;
    previewUrl.value = null;
  } finally {
    isUploading.value = false;
  }
};

// Trigger file input
const triggerFileInput = () => {
  fileInput.value?.click();
};

// Clear preview
const clearPreview = () => {
  previewUrl.value = null;
  error.value = null;
  if (fileInput.value) {
    fileInput.value.value = '';
  }
};
</script>

<template>
  <div>
    <input
      ref="fileInput"
      type="file"
      accept="image/jpeg,image/jpg,image/png,image/webp"
      class="d-none"
      @change="handleFileSelect"
    />
    
    <v-btn
      variant="outlined"
      color="primary"
      size="small"
      @click="triggerFileInput"
      :loading="isUploading"
      :disabled="isUploading"
    >
      <UploadIcon size="18" class="mr-2" />
      {{ t('profile.photo.upload') }}
    </v-btn>
    
    <v-alert
      v-if="error"
      type="error"
      variant="tonal"
      density="compact"
      class="mt-2"
    >
      {{ error }}
    </v-alert>
  </div>
</template>
