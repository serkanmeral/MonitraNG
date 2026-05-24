<script setup lang="ts">
import { ref, computed } from 'vue';
import { LockIcon, EyeIcon, EyeOffIcon } from 'vue-tabler-icons';
import { fetchFromMngKeeper } from '@/services/apiService';
import { useUserStore } from '@/stores/apps/user';
import { useUserFieldPolicies } from '@/composables/useUserFieldPolicies';
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

const userStore = useUserStore();
const profileUser = computed(() => userStore.currentUser);
const { canChangePassword } = useUserFieldPolicies(profileUser);

// Form data
const formData = ref({
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
});

// UI state
const showCurrentPassword = ref(false);
const showNewPassword = ref(false);
const showConfirmPassword = ref(false);
const isLoading = ref(false);
const saveSuccess = ref(false);
const error = ref<string | null>(null);

// Password strength indicator
const passwordStrength = computed(() => {
  const password = formData.value.newPassword;
  if (!password) return { strength: 0, label: '', color: '' };
  
  let strength = 0;
  
  // Length check
  if (password.length >= 8) strength++;
  if (password.length >= 12) strength++;
  
  // Character variety
  if (/[a-z]/.test(password)) strength++;
  if (/[A-Z]/.test(password)) strength++;
  if (/[0-9]/.test(password)) strength++;
  if (/[^a-zA-Z0-9]/.test(password)) strength++;
  
  if (strength <= 2) {
    return { strength, label: t('profile.security.passwordStrength.weak'), color: 'error' };
  } else if (strength <= 4) {
    return { strength, label: t('profile.security.passwordStrength.fair'), color: 'warning' };
  } else if (strength <= 5) {
    return { strength, label: t('profile.security.passwordStrength.good'), color: 'info' };
  } else {
    return { strength, label: t('profile.security.passwordStrength.strong'), color: 'success' };
  }
});

// Validation schema
const schema = computed(() => yup.object({
  currentPassword: yup.string()
    .required(t('profile.security.validation.currentPasswordRequired')),
  newPassword: yup.string()
    .required(t('profile.security.validation.newPasswordRequired'))
    .min(8, t('profile.security.validation.passwordMinLength'))
    .matches(/[a-z]/, t('profile.security.validation.passwordLowercase'))
    .matches(/[A-Z]/, t('profile.security.validation.passwordUppercase'))
    .matches(/[0-9]/, t('profile.security.validation.passwordNumber'))
    .matches(/[^a-zA-Z0-9]/, t('profile.security.validation.passwordSpecial')),
  confirmPassword: yup.string()
    .required(t('profile.security.validation.confirmPasswordRequired'))
    .oneOf([yup.ref('newPassword')], t('profile.security.validation.passwordsDoNotMatch')),
}));

// Snackbar helper
const showSnackbar = (message: string, type: 'success' | 'error' = 'success') => {
  if (type === 'error') {
    alert(message);
  } else {
    // TODO: Use Vuetify snackbar
    alert(message);
  }
};

// Save handler
const handleSave = async () => {
  error.value = null;
  saveSuccess.value = false;
  
  try {
    // Validate form
    await schema.value.validate(formData.value, { abortEarly: false });
    
    isLoading.value = true;
    
    // Call API
    // Backend expects CurrentPassword and NewPassword (capitalized)
    const response = await fetchFromMngKeeper('/auth/change-password', 'POST', {
      CurrentPassword: formData.value.currentPassword,
      NewPassword: formData.value.newPassword,
    });
    
    // Check response - backend returns 200 OK on success, or ErrorResponse on error
    if (response && (response as any).error) {
      throw new Error((response as any).errorDescription || (response as any).error || t('profile.security.messages.changePasswordError'));
    }
    
    // Success - backend returns 200 OK with no body or success message
    saveSuccess.value = true;
    showSnackbar(t('profile.security.messages.changePasswordSuccess'), 'success');
    
    // Reset form
    formData.value = {
      currentPassword: '',
      newPassword: '',
      confirmPassword: '',
    };
    
    // Hide success message after 3 seconds
    setTimeout(() => {
      saveSuccess.value = false;
    }, 3000);
    
  } catch (err: any) {
    console.error('Error changing password:', err);
    
    // Extract error message
    let errorMessage = t('profile.security.messages.changePasswordError');
    if (err.message) {
      errorMessage = err.message;
    } else if (err.data?.errorDescription) {
      errorMessage = err.data.errorDescription;
    } else if (err.data?.error) {
      errorMessage = err.data.error;
    } else if (err.errors) {
      // Validation errors
      const firstError = Object.values(err.errors)[0];
      errorMessage = Array.isArray(firstError) ? firstError[0] : firstError;
    }
    
    error.value = errorMessage;
    showSnackbar(errorMessage, 'error');
  } finally {
    isLoading.value = false;
  }
};

// Cancel handler
const handleCancel = () => {
  formData.value = {
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  };
  error.value = null;
  saveSuccess.value = false;
};
</script>

<template>
  <v-row>
    <!-- Change Password Card -->
    <v-col cols="12" lg="8" md="12">
      <v-card v-if="!canChangePassword" elevation="10" class="mb-4">
        <v-card-item>
          <h5 class="text-h5 mb-4">
            <LockIcon size="24" class="mr-2" />
            {{ t('profile.security.changePassword.title') }}
          </h5>
          <v-alert type="info" variant="tonal" density="comfortable">
            {{ t('profile.security.directoryPasswordManaged') }}
          </v-alert>
        </v-card-item>
      </v-card>

      <v-card v-else elevation="10" class="mb-4">
        <v-card-item>
          <h5 class="text-h5 mb-4">
            <LockIcon size="24" class="mr-2" />
            {{ t('profile.security.changePassword.title') }}
          </h5>
          <p class="text-body-2 text-medium-emphasis mb-6">
            {{ t('profile.security.changePassword.description') }}
          </p>
          
          <v-form @submit.prevent="handleSave">
            <!-- Current Password -->
            <v-text-field
              v-model="formData.currentPassword"
              :type="showCurrentPassword ? 'text' : 'password'"
              :label="t('profile.security.changePassword.currentPassword')"
              :append-inner-icon="showCurrentPassword ? EyeOffIcon : EyeIcon"
              @click:append-inner="showCurrentPassword = !showCurrentPassword"
              variant="outlined"
              density="comfortable"
              class="mb-4"
              :error-messages="error && error.includes('current') ? error : ''"
              autocomplete="current-password"
            />
            
            <!-- New Password -->
            <v-text-field
              v-model="formData.newPassword"
              :type="showNewPassword ? 'text' : 'password'"
              :label="t('profile.security.changePassword.newPassword')"
              :append-inner-icon="showNewPassword ? EyeOffIcon : EyeIcon"
              @click:append-inner="showNewPassword = !showNewPassword"
              variant="outlined"
              density="comfortable"
              class="mb-2"
              autocomplete="new-password"
            />
            
            <!-- Password Strength Indicator -->
            <div v-if="formData.newPassword" class="mb-4">
              <div class="d-flex align-center mb-1">
                <span class="text-caption text-medium-emphasis mr-2">
                  {{ t('profile.security.changePassword.passwordStrength') }}:
                </span>
                <v-chip
                  :color="passwordStrength.color"
                  size="small"
                  variant="flat"
                >
                  {{ passwordStrength.label }}
                </v-chip>
              </div>
              <v-progress-linear
                :model-value="(passwordStrength.strength / 6) * 100"
                :color="passwordStrength.color"
                height="4"
                rounded
              />
            </div>
            
            <!-- Password Requirements -->
            <v-alert
              type="info"
              variant="tonal"
              density="compact"
              class="mb-4"
            >
              <template v-slot:title>
                {{ t('profile.security.changePassword.requirements.title') }}
              </template>
              <ul class="text-caption mt-2 mb-0 pl-4">
                <li>{{ t('profile.security.changePassword.requirements.minLength') }}</li>
                <li>{{ t('profile.security.changePassword.requirements.uppercase') }}</li>
                <li>{{ t('profile.security.changePassword.requirements.lowercase') }}</li>
                <li>{{ t('profile.security.changePassword.requirements.number') }}</li>
                <li>{{ t('profile.security.changePassword.requirements.special') }}</li>
              </ul>
            </v-alert>
            
            <!-- Confirm Password -->
            <v-text-field
              v-model="formData.confirmPassword"
              :type="showConfirmPassword ? 'text' : 'password'"
              :label="t('profile.security.changePassword.confirmPassword')"
              :append-inner-icon="showConfirmPassword ? EyeOffIcon : EyeIcon"
              @click:append-inner="showConfirmPassword = !showConfirmPassword"
              variant="outlined"
              density="comfortable"
              class="mb-4"
              autocomplete="new-password"
            />
            
            <!-- Error Message -->
            <v-alert
              v-if="error"
              type="error"
              variant="tonal"
              density="compact"
              class="mb-4"
              closable
              @click:close="error = null"
            >
              {{ error }}
            </v-alert>
            
            <!-- Success Message -->
            <v-alert
              v-if="saveSuccess"
              type="success"
              variant="tonal"
              density="compact"
              class="mb-4"
            >
              {{ t('profile.security.messages.changePasswordSuccess') }}
            </v-alert>
            
            <!-- Action Buttons -->
            <div class="d-flex gap-2">
              <v-btn
                color="primary"
                variant="flat"
                :loading="isLoading"
                @click="handleSave"
              >
                {{ t('profile.security.changePassword.save') }}
              </v-btn>
              <v-btn
                color="secondary"
                variant="outlined"
                :disabled="isLoading"
                @click="handleCancel"
              >
                {{ t('profile.security.changePassword.cancel') }}
              </v-btn>
            </div>
          </v-form>
        </v-card-item>
      </v-card>
    </v-col>
    
    <!-- Security Tips Card -->
    <v-col cols="12" lg="4" md="12">
      <v-card elevation="10">
        <v-card-item>
          <h5 class="text-h5 mb-4">{{ t('profile.security.tips.title') }}</h5>
          <v-list class="py-0">
            <v-list-item
              v-for="(tip, index) in [
                t('profile.security.tips.tip1'),
                t('profile.security.tips.tip2'),
                t('profile.security.tips.tip3'),
                t('profile.security.tips.tip4'),
              ]"
              :key="index"
              class="px-0"
            >
              <template v-slot:prepend>
                <v-icon color="primary" size="small">mdi-check-circle</v-icon>
              </template>
              <v-list-item-title class="text-body-2">
                {{ tip }}
              </v-list-item-title>
            </v-list-item>
          </v-list>
        </v-card-item>
      </v-card>
    </v-col>
  </v-row>
</template>
