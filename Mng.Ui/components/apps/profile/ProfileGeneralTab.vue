<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import type { User, Gender } from '@/stores/apps/user';
import { useUserFieldPolicies } from '@/composables/useUserFieldPolicies';
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
  // Geçici olarak alert kullanıyoruz
  if (type === 'error') {
    alert(message);
  }
};

// Form data
const formData = ref({
  firstName: '',
  lastName: '',
  email: '',
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

// Email regex pattern
const emailRegex = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

// Validation schema (directory: yalnızca düzenlenebilir alanlar zorunlu)
const schema = computed(() => yup.object({
  firstName: fieldEditable('firstName')
    ? yup.string()
        .required(t('profile.validation.firstNameRequired'))
        .min(2, t('profile.validation.firstNameMinLength'))
        .max(50, t('profile.validation.firstNameMaxLength'))
    : yup.string().nullable(),
  lastName: fieldEditable('lastName')
    ? yup.string()
        .required(t('profile.validation.lastNameRequired'))
        .min(2, t('profile.validation.lastNameMinLength'))
        .max(50, t('profile.validation.lastNameMaxLength'))
    : yup.string().nullable(),
  email: fieldEditable('email')
    ? yup.string()
        .required(t('profile.validation.emailRequired'))
        .matches(emailRegex, t('profile.validation.emailInvalid'))
    : yup.string().nullable(),
  title: yup.string()
    .max(100, t('profile.validation.titleMaxLength')),
  department: yup.string()
    .max(100, t('profile.validation.departmentMaxLength')),
  phoneNumber: yup.string()
    .max(20, t('profile.validation.phoneNumberMaxLength')),
}));

const profileUser = computed(() => userStore.currentUser);

const {
  isDirectory,
  fieldEditable,
  sourceLabelKey,
  sourceChipColor,
} = useUserFieldPolicies(profileUser);

// Get current user
const currentUser = computed(() => {
  const userInfo = authStore.userInfo;
  if (!userInfo) return null;
  
  const currentKeycloakUserId = userInfo.sub || userInfo.username;
  
  // Only use userStore.currentUser if it matches the current authenticated user
  // Compare using keycloakUserId (if available) or username/email
  if (userStore.currentUser) {
    const storedKeycloakUserId = userStore.currentUser.keycloakUserId;
    const storedUsername = userStore.currentUser.username;
    const storedEmail = userStore.currentUser.email;
    const authUsername = userInfo.username || userInfo.preferred_username;
    const authEmail = userInfo.email;
    
    // Match if keycloakUserId matches, or username/email matches
    const isMatch = 
      (storedKeycloakUserId && storedKeycloakUserId === currentKeycloakUserId) ||
      (storedUsername && storedUsername === authUsername) ||
      (storedEmail && storedEmail === authEmail);
    
    if (isMatch) {
      return userStore.currentUser;
    } else {
      // Mismatch - clear the stored user (it's from a different user)
      userStore.currentUser = null;
    }
  }
  
  // Fallback to authStore.userInfo
  // Note: This fallback user doesn't have a backend MongoDB id, so photo upload won't work
  // until userStore.currentUser is populated from backend
  return {
    id: currentKeycloakUserId || '',
    userId: currentKeycloakUserId,
    keycloakUserId: currentKeycloakUserId,
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
    }
  }
});

// Helper function to update formData from user
const updateFormDataFromUser = (user: any) => {
  if (!user) return;
  
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
    email: user.email || '',
    title: user.title || '',
    department: user.department || '',
    gender: genderValue,
    phoneNumber: user.phoneNumber || '',
    photoUrl: user.photoUrl || null,
  };
};

// Watch for user changes - only update formData when not editing
watch(() => currentUser.value, (user) => {
  if (user && !isEditing.value) {
    updateFormDataFromUser(user);
  }
}, { deep: true });

// Watch for isEditing changes - when exiting edit mode, refresh formData from currentUser
watch(() => isEditing.value, (newValue, oldValue) => {
  if (oldValue === true && newValue === false && currentUser.value) {
    // Just exited edit mode, refresh formData from currentUser
    // Use nextTick to ensure userStore.currentUser is updated
    nextTick(() => {
      if (userStore.currentUser) {
        updateFormDataFromUser(userStore.currentUser);
      } else if (currentUser.value) {
        updateFormDataFromUser(currentUser.value);
      }
    });
  }
});

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
    
    // Get user ID - MUST be the real backend userId (MongoDB ObjectId), not Keycloak sub
    // Use userStore.currentUser.id if available
    let userId = userStore.currentUser?.id;
    
    // If userStore.currentUser doesn't exist, try to find user in backend
    if (!userId) {
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
            userId = foundUser.id;
            // Update currentUser with the found user
            userStore.currentUser = foundUser;
          }
        } catch (error) {
          // Silently handle error - will throw error below
        }
      }
    }
    
    if (!userId) {
      throw new Error('User ID not found. Please refresh the page.');
    }
    
    
    // Update user - Backend requires username and email, so we include them
    // Don't include groups - backend will preserve existing groups if groupIds is not provided
    const payload: Parameters<typeof userStore.updateUser>[1] = {
      username:
        currentUser.value.username ||
        authStore.userInfo?.username ||
        authStore.userInfo?.preferred_username ||
        '',
    };
    if (fieldEditable('email')) {
      payload.email = formData.value.email || currentUser.value.email || '';
    } else {
      payload.email = currentUser.value.email || authStore.userInfo?.email || '';
    }
    if (fieldEditable('firstName')) payload.firstName = formData.value.firstName;
    else payload.firstName = currentUser.value.firstName || '';
    if (fieldEditable('lastName')) payload.lastName = formData.value.lastName;
    else payload.lastName = currentUser.value.lastName || '';
    if (fieldEditable('title')) payload.title = formData.value.title || undefined;
    if (fieldEditable('department')) payload.department = formData.value.department || undefined;
    if (fieldEditable('gender')) payload.gender = formData.value.gender;
    if (fieldEditable('phoneNumber')) payload.phoneNumber = formData.value.phoneNumber || undefined;
    if (fieldEditable('photoUrl')) payload.photoUrl = formData.value.photoUrl || undefined;

    await userStore.updateUser(userId, payload);
    
    // Wait for Vue reactivity to update userStore.currentUser
    await nextTick();
    
    // Update formData with the latest userStore.currentUser data to ensure reactivity
    if (userStore.currentUser) {
      updateFormDataFromUser(userStore.currentUser);
    }
    
    // Update authStore.userInfo to reflect changes in header and side menu
    // This ensures the UI updates immediately without requiring a token refresh
    if (authStore.userInfo) {
      authStore.userInfo.given_name = formData.value.firstName;
      authStore.userInfo.family_name = formData.value.lastName;
      authStore.userInfo.email = formData.value.email;
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
      // MUST use userStore.currentUser.id (MongoDB ObjectId), not Keycloak sub
      // If userStore.currentUser doesn't exist, fetch it first
      let userId = userStore.currentUser?.id;
      
      if (!userId) {
        // Try to find user in backend by username/email
        const searchTerm = currentUser.value.username || currentUser.value.email || authStore.userInfo?.username || authStore.userInfo?.email || '';
        if (searchTerm) {
          await userStore.fetchUsers({ search: searchTerm, pageSize: 10 });
          const foundUser = userStore.users.find(u => 
            u.username === currentUser.value?.username || 
            u.email === currentUser.value?.email ||
            u.username === authStore.userInfo?.username ||
            u.email === authStore.userInfo?.email
          );
          
          if (foundUser) {
            userId = foundUser.id;
            userStore.currentUser = foundUser;
          }
        }
      }
      
      if (!userId) {
        throw new Error('User ID not found. Please refresh the page.');
      }
      
      // Update user with photoUrl - include all current user data to prevent data loss
      // Backend requires username and email, and we need to preserve other fields
      // Use currentUser.value as source of truth, fallback to formData if needed
      const response = await userStore.updateUser(userId, {
        username: currentUser.value.username || authStore.userInfo?.username || authStore.userInfo?.preferred_username || '',
        email: formData.value.email || currentUser.value.email || authStore.userInfo?.email || '',
        firstName: currentUser.value.firstName || formData.value.firstName || '',
        lastName: currentUser.value.lastName || formData.value.lastName || '',
        title: currentUser.value.title ?? formData.value.title ?? null,
        department: currentUser.value.department ?? formData.value.department ?? null,
        gender: currentUser.value.gender || formData.value.gender || 'NotSpecified',
        phoneNumber: currentUser.value.phoneNumber ?? formData.value.phoneNumber ?? null,
        photoUrl: photoUrl,
      });
      
      
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
  <v-alert
    v-if="isDirectory"
    type="info"
    variant="tonal"
    density="compact"
    class="mb-4"
  >
    <div class="d-flex align-center flex-wrap ga-2">
      <v-chip size="small" variant="tonal" :color="sourceChipColor">
        {{ t(sourceLabelKey) }}
      </v-chip>
      <span>{{ t('profile.directory.profileHint') }}</span>
    </div>
  </v-alert>

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
                :userId="userStore.currentUser?.id || null"
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
              :disabled="!isEditing || !fieldEditable('firstName')"
              :hint="!fieldEditable('firstName') ? t('profile.directory.fieldReadOnly') : undefined"
              required
            />
            <v-text-field
              v-model="formData.lastName"
              :label="t('profile.personalInfo.lastName')"
              variant="outlined"
              density="comfortable"
              class="mt-3"
              :disabled="!isEditing || !fieldEditable('lastName')"
              :hint="!fieldEditable('lastName') ? t('profile.directory.fieldReadOnly') : undefined"
              required
            />
            <v-text-field
              v-model="formData.email"
              :label="t('profile.personalInfo.email')"
              variant="outlined"
              density="comfortable"
              class="mt-3"
              :disabled="!isEditing || !fieldEditable('email')"
              :hint="!fieldEditable('email') ? t('profile.directory.fieldReadOnly') : undefined"
              type="email"
              required
            />
            <v-text-field
              v-model="formData.title"
              :label="t('profile.personalInfo.jobTitle')"
              variant="outlined"
              density="comfortable"
              class="mt-3"
              :disabled="!isEditing || !fieldEditable('title')"
            />
            <v-text-field
              v-model="formData.department"
              :label="t('profile.personalInfo.department')"
              variant="outlined"
              density="comfortable"
              class="mt-3"
              :disabled="!isEditing || !fieldEditable('department')"
            />
            <v-select
              v-if="fieldEditable('gender')"
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
              :disabled="!isEditing || !fieldEditable('phoneNumber')"
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
