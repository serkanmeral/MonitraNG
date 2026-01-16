<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRouter } from 'vue-router';
import { Form, Field } from 'vee-validate';
import * as yup from 'yup';
import { useLocaleStore } from '@/stores/locale';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useUserStore } from '@/stores/apps/user';
import { fetchFromMngKeeper } from '@/services/apiService';

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

const router = useRouter();
const userStore = useUserStore();
const localeStore = useLocaleStore();

const page = computed(() => ({ 
  title: t('users.create.title') 
}));
const breadcrumbs = computed(() => [
  {
    text: t('users.create.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('users.create.breadcrumbs.users'),
    disabled: false,
    href: '/apps/users',
  },
  {
    text: t('users.create.breadcrumbs.create'),
    disabled: true,
    href: '#',
  },
]);

const loading = ref(false);
const errorMessage = ref('');
const groups = ref<Array<{ id: string; name: string }>>([]);

// Form data
const formData = ref({
  username: '',
  email: '',
  firstName: '',
  lastName: '',
  selectedGroups: [] as string[],
  title: null as string | null,
  department: null as string | null,
  phoneNumber: null as string | null,
  isActive: true,
});

// Validation schema (computed to react to locale changes)
const schema = computed(() => yup.object({
  username: yup.string().required(t('users.create.validation.usernameRequired')).min(3, t('users.create.validation.usernameMinLength')),
  email: yup.string().email(t('users.create.validation.emailInvalid')).required(t('users.create.validation.emailRequired')),
  firstName: yup.string().required(t('users.create.validation.firstNameRequired')),
  lastName: yup.string().required(t('users.create.validation.lastNameRequired')),
}));

// Load groups
onMounted(async () => {
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
});

const onSubmit = async (values: any) => {
  loading.value = true;
  errorMessage.value = '';
  
  try {
    const userData: any = {
      username: formData.value.username,
      email: formData.value.email,
      firstName: formData.value.firstName,
      lastName: formData.value.lastName,
      groups: formData.value.selectedGroups,
      isActive: formData.value.isActive,
    };
    
    // Only include nullable fields if they have values
    if (formData.value.title) {
      userData.title = formData.value.title;
    }
    if (formData.value.department) {
      userData.department = formData.value.department;
    }
    if (formData.value.phoneNumber) {
      userData.phoneNumber = formData.value.phoneNumber;
    }
    
    await userStore.createUser(userData);
    
    // Success - redirect to list
    router.push('/apps/users');
  } catch (error: any) {
    errorMessage.value = error.message || t('users.create.errors.createFailed');
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-item>
      <h5 class="text-h5 mb-6 font-weight-semibold">{{ t('users.create.title') }}</h5>
      
      <Form
        v-slot="{ handleSubmit }"
        :validation-schema="schema"
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
            <!-- Username -->
            <v-col cols="12" md="6">
              <Field name="username" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  v-model="formData.username"
                  :label="t('users.create.fields.username') + ' *'"
                  variant="outlined"
                  :error-messages="errors"
                  required
                />
              </Field>
            </v-col>

            <!-- Email -->
            <v-col cols="12" md="6">
              <Field name="email" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  v-model="formData.email"
                  :label="t('users.create.fields.email') + ' *'"
                  type="email"
                  variant="outlined"
                  :error-messages="errors"
                  required
                />
              </Field>
            </v-col>

            <!-- First Name -->
            <v-col cols="12" md="6">
              <Field name="firstName" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  v-model="formData.firstName"
                  :label="t('users.create.fields.firstName') + ' *'"
                  variant="outlined"
                  :error-messages="errors"
                  required
                />
              </Field>
            </v-col>

            <!-- Last Name -->
            <v-col cols="12" md="6">
              <Field name="lastName" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  v-model="formData.lastName"
                  :label="t('users.create.fields.lastName') + ' *'"
                  variant="outlined"
                  :error-messages="errors"
                  required
                />
              </Field>
            </v-col>

            <!-- Title -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.title"
                :label="t('users.create.fields.title')"
                variant="outlined"
                :placeholder="t('users.create.fields.titlePlaceholder')"
              />
            </v-col>

            <!-- Department -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.department"
                :label="t('users.create.fields.department')"
                variant="outlined"
                :placeholder="t('users.create.fields.departmentPlaceholder')"
              />
            </v-col>

            <!-- Phone Number -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.phoneNumber"
                :label="t('users.create.fields.phoneNumber')"
                variant="outlined"
                :placeholder="t('users.create.fields.phonePlaceholder')"
              />
            </v-col>

            <!-- Is Active -->
            <v-col cols="12" md="6">
              <v-switch
                v-model="formData.isActive"
                :label="t('users.create.fields.isActive')"
                color="success"
                hide-details
              />
            </v-col>

            <!-- Groups -->
            <v-col cols="12">
              <v-label class="mb-2">{{ t('users.create.fields.groups') }}</v-label>
              <v-select
                v-model="formData.selectedGroups"
                :items="groups"
                item-title="name"
                item-value="id"
                :label="t('users.create.fields.groupsSelect')"
                variant="outlined"
                multiple
                chips
                closable-chips
              >
                <template v-slot:item="{ props, item }">
                  <v-list-item v-bind="props" :title="item.raw.name" />
                </template>
              </v-select>
              <div class="text-caption text-medium-emphasis mt-1">
                {{ t('users.create.fields.groupsNote') }}
              </div>
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
              {{ t('users.create.buttons.cancel') }}
            </v-btn>
            <v-btn
              color="primary"
              variant="flat"
              type="submit"
              :loading="loading"
            >
              {{ t('users.create.buttons.create') }}
            </v-btn>
          </div>
        </v-form>
      </Form>
    </v-card-item>
  </v-card>
</template>

