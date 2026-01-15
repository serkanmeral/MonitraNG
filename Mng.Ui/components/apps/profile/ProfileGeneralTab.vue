<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import type { User, Gender } from '@/stores/apps/user';
import PhotoUpload from '@/components/apps/profile/PhotoUpload.vue';
import AvatarDisplay from '@/components/apps/profile/AvatarDisplay.vue';
import * as yup from 'yup';

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

const authStore = useAuthStore();
const userStore = useUserStore();

// Snackbar için basit bir fonksiyon (şimdilik)
const showSnackbar = (message: string, type: 'success' | 'error' = 'success') => {
  // TODO: Vuetify snackbar kullanılacak
  console.log(`[${type.toUpperCase()}] ${message}`);
  // Geçici olarak alert kullanıyoruz
  if (type === 'error') {
    alert(message);
  }
};

// Form data
const formData = ref({
  firstName: '',
  lastName: '',
  title: '',
  department: '',
  gender: 'NotSpecified' as Gender | 'NotSpecified' | 'Male' | 'Female',
  phoneNumber: '',
  photoUrl: null as string | null,
});

// Loading state
const isLoading = ref(false);
const isEditing = ref(false);

// Gender options
const genderOptions = [
  { value: 'NotSpecified', title: t('profile.gender.notSpecified') },
  { value: 'Male', title: t('profile.gender.male') },
  { value: 'Female', title: t('profile.gender.female') },
];

// Validation schema
const schema = computed(() => yup.object({
  firstName: yup.string()
    .required(t('profile.validation.firstNameRequired'))
    .min(2, t('profile.validation.firstNameMinLength'))
    .max(50, t('profile.validation.firstNameMaxLength')),
  lastName: yup.string()
    .required(t('profile.validation.lastNameRequired'))
    .min(2, t('profile.validation.lastNameMinLength'))
    .max(50, t('profile.validation.lastNameMaxLength')),
  title: yup.string()
    .max(100, t('profile.validation.titleMaxLength')),
  department: yup.string()
    .max(100, t('profile.validation.departmentMaxLength')),
  phoneNumber: yup.string()
    .max(20, t('profile.validation.phoneNumberMaxLength')),
}));

// Get current user
const currentUser = computed(() => {
  if (userStore.currentUser) {
    return userStore.currentUser;
  }
  
  const userInfo = authStore.userInfo;
  if (!userInfo) return null;
  
  return {
    id: userInfo.sub || userInfo.username || '',
    userId: userInfo.sub || userInfo.username,
    domainId: userInfo.domain_id || '',
    username: userInfo.username || userInfo.preferred_username || userInfo.sub || '',
    email: userInfo.email || '',
    firstName: userInfo.given_name || userInfo.name?.split(' ')[0] || '',
    lastName: userInfo.family_name || userInfo.name?.split(' ').slice(1).join(' ') || '',
    title: null,
    department: null,
    gender: 'NotSpecified' as const,
    phoneNumber: null,
    photoUrl: null,
    isActive: true,
    groups: authStore.userGroups || [],
    roles: [],
  };
});

// Load user data
// Note: We use authStore.userInfo as primary source since it's already available
// Backend fetch is optional and only for additional fields (title, department, etc.)
onMounted(async () => {
  // Populate form with current user data (from authStore or userStore)
  if (currentUser.value) {
    // Convert gender from integer to string if needed
    let genderValue: Gender | 'NotSpecified' | 'Male' | 'Female' = 'NotSpecified';
    if (currentUser.value.gender !== undefined && currentUser.value.gender !== null) {
      if (typeof currentUser.value.gender === 'number') {
        // Backend returns integer (0, 1, 2), convert to string
        switch (currentUser.value.gender) {
          case 1:
            genderValue = 'Male';
            break;
          case 2:
            genderValue = 'Female';
            break;
          case 0:
          default:
            genderValue = 'NotSpecified';
            break;
        }
      } else {
        // Already a string
        genderValue = currentUser.value.gender as Gender | 'NotSpecified' | 'Male' | 'Female';
      }
    }
    
    formData.value = {
      firstName: currentUser.value.firstName || '',
      lastName: currentUser.value.lastName || '',
      title: currentUser.value.title || '',
      department: currentUser.value.department || '',
      gender: genderValue,
      phoneNumber: currentUser.value.phoneNumber || '',
      photoUrl: currentUser.value.photoUrl || null,
    };
  }
  
  // Try to fetch additional user data from backend if username is available
  // This is optional - if it fails, we continue with authStore data
  if (authStore.userInfo?.username && !userStore.currentUser) {
    try {
      // Try to find user by username in the users list
      const users = await userStore.fetchUsers({ search: authStore.userInfo.username, pageSize: 1 });
      if (users && users.length > 0) {
        // Update form with backend data if available
        const backendUser = users[0];
        
        // Convert gender from integer to string if needed
        let genderValue: Gender | 'NotSpecified' | 'Male' | 'Female' = formData.value.gender;
        if (backendUser.gender !== undefined && backendUser.gender !== null) {
          if (typeof backendUser.gender === 'number') {
            // Backend returns integer (0, 1, 2), convert to string
            switch (backendUser.gender) {
              case 1:
                genderValue = 'Male';
                break;
              case 2:
                genderValue = 'Female';
                break;
              case 0:
              default:
                genderValue = 'NotSpecified';
                break;
            }
          } else {
            // Already a string
            genderValue = backendUser.gender as Gender | 'NotSpecified' | 'Male' | 'Female';
          }
        }
        
        formData.value = {
          firstName: backendUser.firstName || formData.value.firstName,
          lastName: backendUser.lastName || formData.value.lastName,
          title: backendUser.title || formData.value.title,
          department: backendUser.department || formData.value.department,
          gender: genderValue,
          phoneNumber: backendUser.phoneNumber || formData.value.phoneNumber,
          photoUrl: backendUser.photoUrl || formData.value.photoUrl,
        };
      }
    } catch (error) {
      // Silently fail - we'll use authStore data instead
      console.warn('Could not fetch additional user data from backend:', error);
    }
  }
});

// Watch for user changes
watch(() => currentUser.value, (user) => {
  if (user && !isEditing.value) {
    // Convert gender from integer to string if needed
    let genderValue: Gender | 'NotSpecified' | 'Male' | 'Female' = 'NotSpecified';
    if (user.gender !== undefined && user.gender !== null) {
      if (typeof user.gender === 'number') {
        // Backend returns integer (0, 1, 2), convert to string
        switch (user.gender) {
          case 1:
            genderValue = 'Male';
            break;
          case 2:
            genderValue = 'Female';
            break;
          case 0:
          default:
            genderValue = 'NotSpecified';
            break;
        }
      } else {
        // Already a string
        genderValue = user.gender as Gender | 'NotSpecified' | 'Male' | 'Female';
      }
    }
    
    formData.value = {
      firstName: user.firstName || '',
      lastName: user.lastName || '',
      title: user.title || '',
      department: user.department || '',
      gender: genderValue,
      phoneNumber: user.phoneNumber || '',
      photoUrl: user.photoUrl || null,
    };
  }
}, { deep: true });

// Save handler
const handleSave = async () => {
  if (!currentUser.value) {
    showSnackbar(t('profile.messages.userNotFound'), 'error');
    return;
  }
  
  try {
    // Validate form
    await schema.value.validate(formData.value, { abortEarly: false });
    
    isLoading.value = true;
    
    // Get user ID - MUST be the real backend userId, not Keycloak sub
    // If currentUser is from userStore (backend), use its userId/id
    // Otherwise, we need to fetch the user first to get the real ID
    let userId = currentUser.value.userId || currentUser.value.id;
    
    // If userId is still Keycloak sub (starts with UUID pattern), try to find real user
    if (!userId || userId === authStore.userInfo?.sub) {
      // Try to find user in backend by username/email
      const searchTerm = currentUser.value.username || currentUser.value.email || authStore.userInfo?.username || authStore.userInfo?.email || '';
      if (searchTerm) {
        try {
          await userStore.fetchUsers({ search: searchTerm, pageSize: 10 });
          const foundUser = userStore.users.find(u => 
            u.username === currentUser.value?.username || 
            u.email === currentUser.value?.email ||
            u.username === authStore.userInfo?.username ||
            u.email === authStore.userInfo?.email
          );
          
          if (foundUser) {
            userId = foundUser.userId || foundUser.id;
            // Update currentUser with the found user
            userStore.currentUser = foundUser;
          }
        } catch (error) {
          console.warn('Could not find user in backend:', error);
        }
      }
    }
    
    if (!userId || userId === authStore.userInfo?.sub) {
      throw new Error('Kullanıcı ID bulunamadı. Lütfen sayfayı yenileyin ve tekrar deneyin.');
    }
    
    // Update user - Backend requires username and email, so we include them
    // Don't include groups - backend will preserve existing groups if groupIds is not provided
    await userStore.updateUser(userId, {
      username: currentUser.value.username || authStore.userInfo?.username || authStore.userInfo?.preferred_username || '',
      email: currentUser.value.email || authStore.userInfo?.email || '', // Backend requires email
      firstName: formData.value.firstName,
      lastName: formData.value.lastName,
      title: formData.value.title || undefined,
      department: formData.value.department || undefined,
      gender: formData.value.gender,
      phoneNumber: formData.value.phoneNumber || undefined,
      photoUrl: formData.value.photoUrl || undefined,
      // groups is intentionally omitted - backend will preserve existing groups
    });
    
    // Update authStore.userInfo to reflect changes in header and side menu
    // This ensures the UI updates immediately without requiring a token refresh
    if (authStore.userInfo) {
      authStore.userInfo.given_name = formData.value.firstName;
      authStore.userInfo.family_name = formData.value.lastName;
      // Update full name if it exists
      if (authStore.userInfo.name) {
        authStore.userInfo.name = `${formData.value.firstName} ${formData.value.lastName}`;
      }
    }
    
    isEditing.value = false;
    showSnackbar(t('profile.messages.saveSuccess'), 'success');
  } catch (error: any) {
    console.error('Error saving profile:', error);
    if (error.errors) {
      showSnackbar(error.errors[0], 'error');
    } else {
      showSnackbar(error.message || t('profile.messages.saveError'), 'error');
    }
  } finally {
    isLoading.value = false;
  }
};

// Cancel handler
const handleCancel = () => {
  if (currentUser.value) {
    formData.value = {
      firstName: currentUser.value.firstName || '',
      lastName: currentUser.value.lastName || '',
      title: currentUser.value.title || '',
      department: currentUser.value.department || '',
      gender: currentUser.value.gender || 'NotSpecified',
      phoneNumber: currentUser.value.phoneNumber || '',
      photoUrl: currentUser.value.photoUrl || null,
    };
  }
  isEditing.value = false;
};

// Photo upload handler
const handlePhotoUploaded = async (photoUrl: string) => {
  formData.value.photoUrl = photoUrl;
  // Auto-save photo
  if (currentUser.value) {
    try {
      const userId = currentUser.value.userId || currentUser.value.id;
      
      // Update user with photoUrl (backend requires username and email)
      const response = await userStore.updateUser(userId, {
        username: currentUser.value.username || authStore.userInfo?.username || authStore.userInfo?.preferred_username || '',
        email: currentUser.value.email || authStore.userInfo?.email || '',
        photoUrl: photoUrl,
      });
      
      // Debug logs
      console.log('[ProfileGeneralTab] Photo uploaded successfully');
      console.log('[ProfileGeneralTab] photoUrl:', photoUrl);
      console.log('[ProfileGeneralTab] updateUser response:', response);
      console.log('[ProfileGeneralTab] userStore.currentUser after update:', userStore.currentUser);
      console.log('[ProfileGeneralTab] currentUser computed after update:', currentUser.value);
      
      // Force reactivity: explicitly update currentUser.photoUrl
      // Add cache-busting query parameter to force browser to reload the image
      const cacheBustUrl = `${photoUrl}?t=${Date.now()}`;
      if (userStore.currentUser) {
        userStore.currentUser.photoUrl = cacheBustUrl;
      }
      
      // Also update formData to ensure it's in sync
      formData.value.photoUrl = cacheBustUrl;
      
      // After image loads, update to clean URL (without cache bust parameter)
      // This ensures the URL is clean for future updates
      setTimeout(() => {
        if (userStore.currentUser) {
          userStore.currentUser.photoUrl = photoUrl;
        }
        formData.value.photoUrl = photoUrl;
      }, 500);
      
      showSnackbar(t('profile.messages.photoUploadSuccess'), 'success');
    } catch (error) {
      console.error('Error saving photo:', error);
      showSnackbar(t('profile.messages.photoUploadError'), 'error');
    }
  }
};

// Photo remove handler
const handlePhotoRemove = async () => {
  if (!currentUser.value) return;
  
  try {
    isLoading.value = true;
    formData.value.photoUrl = null;
    await userStore.updateUser(currentUser.value.id, {
      photoUrl: null,
    });
    showSnackbar(t('profile.messages.photoRemoveSuccess'), 'success');
  } catch (error: any) {
    console.error('Error removing photo:', error);
    showSnackbar(error.message || t('profile.messages.photoRemoveError'), 'error');
  } finally {
    isLoading.value = false;
  }
};

// Format date
const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-';
  try {
    return new Date(date).toLocaleDateString('tr-TR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    });
  } catch {
    return '-';
  }
};
</script>

<template>
  <v-row>
    <!-- Left Column: Photo and Personal Info -->
    <v-col cols="12" lg="4" md="12">
      <!-- Photo Card -->
      <v-card elevation="10" class="mb-4">
        <v-card-item>
          <h5 class="text-h5 mb-4">{{ t('profile.photo.title') }}</h5>
          <div class="d-flex flex-column align-center">
            <AvatarDisplay 
              :user="currentUser"
              :size="200"
            />
            <div class="mt-4 d-flex gap-2">
              <PhotoUpload 
                @uploaded="handlePhotoUploaded"
                :currentPhotoUrl="formData.photoUrl"
                :userId="currentUser?.userId || currentUser?.id || null"
              />
              <v-btn
                v-if="formData.photoUrl"
                variant="outlined"
                color="error"
                size="small"
                @click="handlePhotoRemove"
                :loading="isLoading"
              >
                {{ t('profile.photo.remove') }}
              </v-btn>
            </div>
            <p class="text-caption text-center mt-2 text-medium-emphasis">
              {{ t('profile.photo.maxSize') }}<br />
              {{ t('profile.photo.formats') }}
            </p>
          </div>
        </v-card-item>
      </v-card>
      
      <!-- Personal Info Card -->
      <v-card elevation="10">
        <v-card-item>
          <h5 class="text-h5 mb-4">{{ t('profile.personalInfo.title') }}</h5>
          <v-form ref="formRef">
            <v-text-field
              v-model="formData.firstName"
              :label="t('profile.personalInfo.firstName')"
              variant="outlined"
              density="comfortable"
              :disabled="!isEditing"
              required
            />
            <v-text-field
              v-model="formData.lastName"
              :label="t('profile.personalInfo.lastName')"
              variant="outlined"
              density="comfortable"
              class="mt-3"
              :disabled="!isEditing"
              required
            />
            <v-text-field
              v-model="formData.title"
              :label="t('profile.personalInfo.title')"
              variant="outlined"
              density="comfortable"
              class="mt-3"
              :disabled="!isEditing"
            />
            <v-text-field
              v-model="formData.department"
              :label="t('profile.personalInfo.department')"
              variant="outlined"
              density="comfortable"
              class="mt-3"
              :disabled="!isEditing"
            />
            <v-select
              v-model="formData.gender"
              :label="t('profile.personalInfo.gender')"
              :items="genderOptions"
              item-title="title"
              item-value="value"
              variant="outlined"
              density="comfortable"
              class="mt-3"
              :disabled="!isEditing"
            />
            <v-text-field
              v-model="formData.phoneNumber"
              :label="t('profile.personalInfo.phoneNumber')"
              variant="outlined"
              density="comfortable"
              class="mt-3"
              :disabled="!isEditing"
            />
            <div class="d-flex gap-2 mt-4">
              <v-btn
                v-if="!isEditing"
                color="primary"
                variant="flat"
                block
                @click="isEditing = true"
              >
                {{ t('profile.buttons.edit') }}
              </v-btn>
              <template v-else>
                <v-btn
                  color="primary"
                  variant="flat"
                  block
                  @click="handleSave"
                  :loading="isLoading"
                >
                  {{ t('profile.buttons.save') }}
                </v-btn>
                <v-btn
                  variant="outlined"
                  block
                  @click="handleCancel"
                  :disabled="isLoading"
                >
                  {{ t('profile.buttons.cancel') }}
                </v-btn>
              </template>
            </div>
          </v-form>
        </v-card-item>
      </v-card>
    </v-col>
    
    <!-- Right Column: Account Info and Groups -->
    <v-col cols="12" lg="8" md="12">
      <!-- Account Info Card -->
      <v-card elevation="10" class="mb-4">
        <v-card-item>
          <h5 class="text-h5 mb-4">{{ t('profile.accountInfo.title') }}</h5>
          <v-row>
            <v-col cols="12" md="6">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">{{ t('profile.accountInfo.username') }}</span>
                <p class="text-body-1 mt-1">{{ currentUser?.username || '-' }}</p>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">{{ t('profile.accountInfo.email') }}</span>
                <p class="text-body-1 mt-1">{{ currentUser?.email || '-' }}</p>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">{{ t('profile.accountInfo.domain') }}</span>
                <p class="text-body-1 mt-1">{{ authStore.domainName || '-' }}</p>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">{{ t('profile.accountInfo.status') }}</span>
                <p class="text-body-1 mt-1">
                  <v-chip 
                    :color="currentUser?.isActive ? 'success' : 'error'"
                    size="small"
                    variant="flat"
                  >
                    {{ currentUser?.isActive ? t('profile.status.active') : t('profile.status.inactive') }}
                  </v-chip>
                </p>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">{{ t('profile.accountInfo.createdAt') }}</span>
                <p class="text-body-1 mt-1">{{ formatDate(currentUser?.createdAt) }}</p>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">{{ t('profile.accountInfo.updatedAt') }}</span>
                <p class="text-body-1 mt-1">{{ formatDate(currentUser?.updatedAt) }}</p>
              </div>
            </v-col>
          </v-row>
        </v-card-item>
      </v-card>
      
      <!-- Groups Card -->
      <v-card elevation="10">
        <v-card-item>
          <div class="d-flex justify-space-between align-center mb-4">
            <h5 class="text-h5">{{ t('profile.groups.title') }}</h5>
            <v-btn
              variant="outlined"
              size="small"
              :to="'/apps/groups'"
            >
              {{ t('profile.groups.manage') }}
            </v-btn>
          </div>
          <div v-if="currentUser?.groups && currentUser.groups.length > 0">
            <v-chip
              v-for="group in currentUser.groups"
              :key="group"
              class="mr-2 mb-2"
              color="primary"
              variant="outlined"
            >
              {{ group }}
            </v-chip>
          </div>
          <p v-else class="text-body-2 text-medium-emphasis">
            {{ t('profile.groups.noGroups') }}
          </p>
        </v-card-item>
      </v-card>
    </v-col>
  </v-row>
</template>
