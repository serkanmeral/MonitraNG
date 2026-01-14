<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRouter } from 'vue-router';
import { Form, Field } from 'vee-validate';
import * as yup from 'yup';
import { useLocaleStore } from '@/stores/locale';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useGroupStore } from '@/stores/apps/group';

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params);
  }
  // Fallback: try global.t if available
  if (i18n?.global?.t) {
    return i18n.global.t(key, params);
  }
  return key;
};
const localeStore = useLocaleStore();
const router = useRouter();
const groupStore = useGroupStore();

const page = computed(() => ({ title: t('groups.create.title') }));
const breadcrumbs = computed(() => [
  {
    text: t('groups.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('groups.breadcrumbs.groups'),
    disabled: false,
    href: '/apps/groups',
  },
  {
    text: t('groups.breadcrumbs.create'),
    disabled: true,
    href: '#',
  },
]);

const loading = ref(false);
const errorMessage = ref('');

// Form data
const formData = ref({
  name: '',
  description: '',
  isActive: true,
});

// Validation schema
const schema = computed(() => yup.object({
  name: yup.string().required(t('groups.validation.nameRequired')).min(2, t('groups.validation.nameMinLength')),
  description: yup.string().max(500, t('groups.validation.descriptionMaxLength')),
}));

const onSubmit = async (values: any) => {
  loading.value = true;
  errorMessage.value = '';
  
  try {
    const groupData = {
      name: values.name,
      description: values.description || undefined,
      isActive: formData.value.isActive, // Switch value comes from formData
    };
    
    await groupStore.createGroup(groupData);
    
    // Success - redirect to list with refresh parameter
    router.push({ path: '/apps/groups', query: { refresh: Date.now() } });
  } catch (error: any) {
    console.error('Error in onSubmit:', error);
    errorMessage.value = error.message || t('groups.errors.create');
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-item>
      <h5 class="text-h5 mb-6 font-weight-semibold">{{ t('groups.create.title') }}</h5>
      
      <Form
        v-slot="{ handleSubmit, errors: formErrors }"
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

          <!-- Validation Errors Debug -->
          <v-alert
            v-if="Object.keys(formErrors).length > 0"
            type="warning"
            variant="tonal"
            density="compact"
            class="mb-4"
          >
            Validation hataları: {{ JSON.stringify(formErrors) }}
          </v-alert>

          <v-row>
            <!-- Group Name -->
            <v-col cols="12" md="8">
              <Field name="name" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  :label="t('groups.create.name')"
                  variant="outlined"
                  :error-messages="errors"
                  required
                />
              </Field>
            </v-col>

            <!-- Is Active -->
            <v-col cols="12" md="4">
              <v-switch
                v-model="formData.isActive"
                :label="t('groups.create.isActive')"
                color="success"
                hide-details
                class="mt-4"
              />
            </v-col>

            <!-- Description -->
            <v-col cols="12">
              <Field name="description" v-slot="{ field, errors }">
                <v-textarea
                  v-bind="field"
                  :label="t('groups.create.description')"
                  variant="outlined"
                  :error-messages="errors"
                  rows="3"
                  counter="500"
                />
              </Field>
            </v-col>
          </v-row>

          <!-- Actions -->
          <div class="d-flex justify-end ga-3 mt-6">
            <v-btn
              color="error"
              variant="flat"
              @click="router.push('/apps/groups')"
              :disabled="loading"
            >
              {{ t('groups.create.cancel') }}
            </v-btn>
            <v-btn
              color="primary"
              variant="flat"
              type="submit"
              :loading="loading"
            >
              {{ t('groups.create.create') }}
            </v-btn>
          </div>
        </v-form>
      </Form>
    </v-card-item>
  </v-card>
</template>

