<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { Form, Field } from 'vee-validate';
import * as yup from 'yup';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useGroupStore } from '@/stores/apps/group';

const router = useRouter();
const groupStore = useGroupStore();

const page = ref({ title: 'Yeni Grup Oluştur' });
const breadcrumbs = ref([
  {
    text: 'Dashboard',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Grup Yönetimi',
    disabled: false,
    href: '/apps/groups',
  },
  {
    text: 'Yeni Grup',
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
const schema = yup.object({
  name: yup.string().required('Grup adı gereklidir').min(2, 'Grup adı en az 2 karakter olmalıdır'),
  description: yup.string().max(500, 'Açıklama en fazla 500 karakter olabilir'),
});

const onSubmit = async (values: any) => {
  console.log('onSubmit called with values:', values);
  
  loading.value = true;
  errorMessage.value = '';
  
  try {
    const groupData = {
      name: values.name,
      description: values.description || undefined,
      isActive: formData.value.isActive, // Switch value comes from formData
    };
    
    console.log('Calling groupStore.createGroup with:', groupData);
    await groupStore.createGroup(groupData);
    
    console.log('Group created successfully, redirecting...');
    // Success - redirect to list with refresh parameter
    router.push({ path: '/apps/groups', query: { refresh: Date.now() } });
  } catch (error: any) {
    console.error('Error in onSubmit:', error);
    errorMessage.value = error.message || 'Grup oluşturulurken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-item>
      <h5 class="text-h5 mb-6 font-weight-semibold">Yeni Grup Oluştur</h5>
      
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
                  label="Grup Adı *"
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
                label="Aktif"
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
                  label="Açıklama"
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
              İptal
            </v-btn>
            <v-btn
              color="primary"
              variant="flat"
              type="submit"
              :loading="loading"
            >
              Oluştur
            </v-btn>
          </div>
        </v-form>
      </Form>
    </v-card-item>
  </v-card>
</template>

