<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { Form, Field } from 'vee-validate';
import * as yup from 'yup';
import { useLocaleStore } from '@/stores/locale';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useUserStore, Gender } from '@/stores/apps/user';
import { fetchFromMngKeeper } from '@/services/apiService';
import { useUserFieldPolicies } from '@/composables/useUserFieldPolicies';

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

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const localeStore = useLocaleStore();

const userId = route.params.id as string;

const page = computed(() => ({ 
  title: t('users.edit.title') 
}));
const breadcrumbs = computed(() => [
  {
    text: t('users.edit.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('users.edit.breadcrumbs.users'),
    disabled: false,
    href: '/apps/users',
  },
  {
    text: t('users.edit.breadcrumbs.edit'),
    disabled: true,
    href: '#',
  },
]);

const loading = ref(false);
const errorMessage = ref('');
const groups = ref<Array<{ id: string; name: string }>>([]);
const formKey = ref(0); // Key for form re-rendering

// Form data
const formData = ref({
  email: '',
  firstName: '',
  lastName: '',
  isActive: true,
  includeInApplication: true,
  selectedGroups: [] as string[],
  title: null as string | null,
  department: null as string | null,
  phoneNumber: null as string | null,
  photoUrl: null as string | null,
  gender: 'NotSpecified' as Gender | 'NotSpecified' | 'Male' | 'Female',
});

const viewingUserRef = computed(() => userStore.viewingUser);
const {
  isDirectory,
  canManageGroups,
  canDeactivate,
  fieldEditable,
  sourceLabelKey,
  sourceChipColor,
} = useUserFieldPolicies(viewingUserRef);

const genderOptions = computed(() => [
  { value: 'NotSpecified', title: t('users.details.gender.notSpecified') },
  { value: 'Male', title: t('users.details.gender.male') },
  { value: 'Female', title: t('users.details.gender.female') },
]);

// Validation — yalnızca düzenlenebilir alanlar için zorunluluk
const schema = computed(() =>
  yup.object({
    email: fieldEditable('email')
      ? yup.string().email(t('users.edit.validation.emailInvalid')).required(t('users.edit.validation.emailRequired'))
      : yup.string().nullable(),
    firstName: fieldEditable('firstName')
      ? yup.string().required(t('users.edit.validation.firstNameRequired'))
      : yup.string().nullable(),
    lastName: fieldEditable('lastName')
      ? yup.string().required(t('users.edit.validation.lastNameRequired'))
      : yup.string().nullable(),
  })
);

// Load user data
const loadUser = async () => {
  loading.value = true;
  try {
    await userStore.fetchUserById(userId);
    
    if (userStore.viewingUser) {
      const user = userStore.viewingUser;
      let genderValue: Gender | 'NotSpecified' | 'Male' | 'Female' = 'NotSpecified';
      if (user.gender != null) {
        if (typeof user.gender === 'number') {
          genderValue = user.gender === 1 ? 'Male' : user.gender === 2 ? 'Female' : 'NotSpecified';
        } else {
          genderValue = user.gender as Gender;
        }
      }
      formData.value = {
        email: user.email || '',
        firstName: user.firstName || '',
        lastName: user.lastName || '',
        isActive: user.isActive,
        includeInApplication: user.includeInApplication !== false,
        selectedGroups: user.groups || [],
        title: user.title || null,
        department: user.department || null,
        phoneNumber: user.phoneNumber || null,
        photoUrl: user.photoUrl || null,
        gender: genderValue,
      };
      // Force form re-render to pick up initial values
      formKey.value++;
    }
  } catch (error: any) {
    errorMessage.value = error.message || t('users.edit.errors.loadFailed');
  } finally {
    loading.value = false;
  }
};

// Load groups
const loadGroups = async () => {
  try {
    const response = await fetchFromMngKeeper('/group', 'GET');
    let allGroups: any[] = [];
    
    if (response.groups && Array.isArray(response.groups)) {
      allGroups = response.groups;
    } else if (Array.isArray(response)) {
      allGroups = response;
    }
    
    // Filter out 'admins' group (case-insensitive)
    groups.value = allGroups
      .filter((g: any) => {
        const groupName = (g.name || '').toLowerCase();
        return groupName !== 'admins';
      })
      .map((g: any) => ({
        id: g.id || g.groupId || '',
        name: g.name || '',
      }));
  } catch (error) {
    console.error('Error loading groups:', error);
  }
};

onMounted(async () => {
  await Promise.all([loadUser(), loadGroups()]);
});

watch(() => route.params.id, async (newId) => {
  if (newId) {
    await loadUser();
  }
});

const onSubmit = async (values: any) => {
  loading.value = true;
  errorMessage.value = '';
  
  try {
    // Get username from viewingUser (required by backend)
    const username = userStore.viewingUser?.username || '';
    if (!username) {
      throw new Error(t('users.edit.errors.usernameNotFound'));
    }
    
    // Get current user data to preserve fields not in the form
    const currentUser = userStore.viewingUser;
    if (!currentUser) {
      throw new Error(t('users.edit.errors.userInfoNotFound'));
    }
    
    // Check if groups were changed by comparing with original user groups
    const originalGroups = currentUser.groups || [];
    const selectedGroups = formData.value.selectedGroups || [];
    
    // Compare arrays (order doesn't matter, just membership)
    const groupsChanged = 
      originalGroups.length !== selectedGroups.length ||
      !originalGroups.every((g: string) => selectedGroups.includes(g)) ||
      !selectedGroups.every((g: string) => originalGroups.includes(g));
    
    const userData: any = {
      username,
    };

    if (fieldEditable('email')) userData.email = formData.value.email;
    else userData.email = currentUser.email || '';

    if (fieldEditable('firstName')) userData.firstName = formData.value.firstName;
    else userData.firstName = currentUser.firstName || '';

    if (fieldEditable('lastName')) userData.lastName = formData.value.lastName;
    else userData.lastName = currentUser.lastName || '';

    if (fieldEditable('isActive')) userData.isActive = formData.value.isActive;
    else userData.isActive = currentUser.isActive;

    if (fieldEditable('includeInApplication')) {
      userData.includeInApplication = formData.value.includeInApplication;
    } else {
      userData.includeInApplication = currentUser.includeInApplication !== false;
    }

    if (canManageGroups.value && groupsChanged) {
      userData.groups = selectedGroups;
    }

    if (fieldEditable('title')) userData.title = formData.value.title || null;
    if (fieldEditable('department')) userData.department = formData.value.department || null;
    if (fieldEditable('phoneNumber')) userData.phoneNumber = formData.value.phoneNumber || null;
    if (fieldEditable('photoUrl')) userData.photoUrl = formData.value.photoUrl || null;
    if (fieldEditable('gender')) userData.gender = formData.value.gender;
    
    await userStore.updateUser(userId, userData);
    
    // Success - redirect to list with refresh parameter
    router.push({ path: '/apps/users', query: { refresh: Date.now() } });
  } catch (error: any) {
    console.error('[UserEdit] Error updating user:', error);
    errorMessage.value = error.message || t('users.edit.errors.updateFailed');
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10" v-if="!loading || userStore.viewingUser">
    <v-card-item>
      <div class="d-flex align-center flex-wrap ga-3 mb-4">
        <h5 class="text-h5 font-weight-semibold mb-0">{{ t('users.edit.title') }}</h5>
        <v-chip
          v-if="userStore.viewingUser"
          size="small"
          variant="tonal"
          :color="sourceChipColor"
        >
          {{ t(sourceLabelKey) }}
        </v-chip>
      </div>

      <v-alert
        v-if="isDirectory"
        type="info"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        {{ t('users.directory.editHint') }}
      </v-alert>
      
      <div v-if="loading && !userStore.viewingUser" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" />
        <p class="text-subtitle-1 mt-4">{{ t('users.edit.loading') }}</p>
      </div>

      <Form
        v-else-if="userStore.viewingUser"
        v-slot="{ handleSubmit }"
        :validation-schema="schema"
        :initial-values="formData"
        :key="`form-${userId}-${formKey}`"
      >
        <v-form @submit.prevent="handleSubmit(onSubmit)">
          <!-- Error Message -->
          <v-alert
            v-if="errorMessage"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-4"
          >
            {{ errorMessage }}
          </v-alert>

          <v-row>
            <v-col v-if="isDirectory" cols="12">
              <p class="text-subtitle-2 text-medium-emphasis mb-2">
                {{ t('users.edit.sections.directoryIdentity') }}
              </p>
            </v-col>

            <!-- Username (Read-only) -->
            <v-col cols="12" md="6">
              <v-text-field
                :model-value="userStore.viewingUser?.username || ''"
                :label="t('users.edit.fields.username')"
                variant="outlined"
                disabled
              />
              <div class="text-caption text-medium-emphasis mt-1">
                {{ t('users.edit.fields.usernameNote') }}
              </div>
            </v-col>

            <!-- Email -->
            <v-col cols="12" md="6">
              <Field name="email" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  v-model="formData.email"
                  :label="t('users.edit.fields.email') + (fieldEditable('email') ? ' *' : '')"
                  type="email"
                  variant="outlined"
                  :error-messages="errors"
                  :disabled="!fieldEditable('email')"
                  :hint="!fieldEditable('email') ? t('users.directory.fieldReadOnly') : undefined"
                  :required="fieldEditable('email')"
                />
              </Field>
            </v-col>

            <!-- First Name -->
            <v-col cols="12" md="6">
              <Field name="firstName" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  v-model="formData.firstName"
                  :label="t('users.edit.fields.firstName') + (fieldEditable('firstName') ? ' *' : '')"
                  variant="outlined"
                  :error-messages="errors"
                  :disabled="!fieldEditable('firstName')"
                  :hint="!fieldEditable('firstName') ? t('users.directory.fieldReadOnly') : undefined"
                  :required="fieldEditable('firstName')"
                />
              </Field>
            </v-col>

            <!-- Last Name -->
            <v-col cols="12" md="6">
              <Field name="lastName" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  v-model="formData.lastName"
                  :label="t('users.edit.fields.lastName') + (fieldEditable('lastName') ? ' *' : '')"
                  variant="outlined"
                  :error-messages="errors"
                  :disabled="!fieldEditable('lastName')"
                  :hint="!fieldEditable('lastName') ? t('users.directory.fieldReadOnly') : undefined"
                  :required="fieldEditable('lastName')"
                />
              </Field>
            </v-col>

            <v-col v-if="isDirectory" cols="12">
              <p class="text-subtitle-2 text-medium-emphasis mb-2 mt-2">
                {{ t('users.edit.sections.appProfile') }}
              </p>
            </v-col>

            <!-- Title -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.title"
                :label="t('users.edit.fields.title')"
                variant="outlined"
                :placeholder="t('users.edit.fields.titlePlaceholder')"
                :disabled="!fieldEditable('title')"
              />
            </v-col>

            <!-- Department -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.department"
                :label="t('users.edit.fields.department')"
                variant="outlined"
                :placeholder="t('users.edit.fields.departmentPlaceholder')"
                :disabled="!fieldEditable('department')"
              />
            </v-col>

            <!-- Phone Number -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.phoneNumber"
                :label="t('users.edit.fields.phoneNumber')"
                variant="outlined"
                :placeholder="t('users.edit.fields.phonePlaceholder')"
                :disabled="!fieldEditable('phoneNumber')"
              />
            </v-col>

            <!-- Gender -->
            <v-col v-if="fieldEditable('gender')" cols="12" md="6">
              <v-select
                v-model="formData.gender"
                :label="t('users.edit.fields.gender')"
                :items="genderOptions"
                item-title="title"
                item-value="value"
                variant="outlined"
              />
            </v-col>

            <!-- Photo URL -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.photoUrl"
                :label="t('users.edit.fields.photoUrl')"
                variant="outlined"
                :placeholder="t('users.edit.fields.photoUrlPlaceholder')"
                :disabled="!fieldEditable('photoUrl')"
              />
              <div v-if="!fieldEditable('photoUrl')" class="text-caption text-medium-emphasis mt-1">
                {{ t('users.edit.fields.photoUrlNote') }}
              </div>
            </v-col>

            <!-- Is Active -->
            <v-col v-if="canDeactivate" cols="12" md="6">
              <v-switch
                v-model="formData.isActive"
                :label="t('users.edit.fields.isActive')"
                color="success"
                hide-details
              />
            </v-col>

            <!-- Application scope -->
            <v-col v-if="fieldEditable('includeInApplication')" cols="12" md="6">
              <v-switch
                v-model="formData.includeInApplication"
                :label="t('users.applicationScope.includeInApplication')"
                :hint="t('users.applicationScope.includeInApplicationHint')"
                persistent-hint
                color="primary"
              />
            </v-col>

            <!-- Groups -->
            <v-col cols="12">
              <v-label class="mb-2">{{ t('users.edit.fields.groups') }}</v-label>
              <template v-if="canManageGroups">
                <v-select
                  v-model="formData.selectedGroups"
                  :items="groups"
                  item-title="name"
                  item-value="id"
                  :label="t('users.edit.fields.groupsSelect')"
                  variant="outlined"
                  multiple
                  chips
                  closable-chips
                >
                  <template v-slot:item="{ props, item }">
                    <v-list-item v-bind="props" :title="item.raw.name" />
                  </template>
                </v-select>
              </template>
              <template v-else>
                <p class="text-caption text-medium-emphasis mb-2">
                  {{ t('users.directory.groupsManagedExternally') }}
                </p>
                <div class="d-flex ga-1 flex-wrap">
                  <v-chip
                    v-for="groupName in formData.selectedGroups"
                    :key="groupName"
                    size="small"
                    color="primary"
                    variant="outlined"
                  >
                    {{ groupName }}
                  </v-chip>
                  <span
                    v-if="!formData.selectedGroups?.length"
                    class="text-caption text-medium-emphasis"
                  >
                    {{ t('users.groups.none') }}
                  </span>
                </div>
              </template>
            </v-col>
          </v-row>

          <!-- Actions -->
          <div class="d-flex justify-end ga-3 mt-6">
            <v-btn
              color="error"
              variant="flat"
              @click="router.push('/apps/users')"
              :disabled="loading"
            >
              {{ t('users.edit.buttons.cancel') }}
            </v-btn>
            <v-btn
              color="primary"
              variant="flat"
              type="submit"
              :loading="loading"
            >
              {{ t('users.edit.buttons.save') }}
            </v-btn>
          </div>
        </v-form>
      </Form>
    </v-card-item>
  </v-card>
</template>

