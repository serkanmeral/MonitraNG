<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { Form, Field } from 'vee-validate';
import * as yup from 'yup';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useUserStore } from '@/stores/apps/user';
import { fetchFromMngKeeper } from '@/services/apiService';

const router = useRouter();
const userStore = useUserStore();

const page = ref({ title: 'Yeni Kullanıcı Oluştur' });
const breadcrumbs = ref([
  {
    text: 'Dashboard',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Kullanıcı Yönetimi',
    disabled: false,
    href: '/apps/users',
  },
  {
    text: 'Yeni Kullanıcı',
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
  password: '',
  confirmPassword: '',
  firstName: '',
  lastName: '',
  selectedGroups: [] as string[],
});

// Validation schema
const schema = yup.object({
  username: yup.string().required('Kullanıcı adı gereklidir').min(3, 'Kullanıcı adı en az 3 karakter olmalıdır'),
  email: yup.string().email('Geçerli bir email adresi giriniz').required('Email gereklidir'),
  password: yup.string().required('Şifre gereklidir').min(6, 'Şifre en az 6 karakter olmalıdır'),
  confirmPassword: yup.string()
    .required('Şifre tekrarı gereklidir')
    .oneOf([yup.ref('password')], 'Şifreler eşleşmiyor'),
  firstName: yup.string().required('Ad gereklidir'),
  lastName: yup.string().required('Soyad gereklidir'),
});

// Load groups
onMounted(async () => {
  try {
    const response = await fetchFromMngKeeper('/group', 'GET');
    if (response.groups && Array.isArray(response.groups)) {
      groups.value = response.groups.map((g: any) => ({
        id: g.id || g.groupId || '',
        name: g.name || '',
      }));
    } else if (Array.isArray(response)) {
      groups.value = response.map((g: any) => ({
        id: g.id || g.groupId || '',
        name: g.name || '',
      }));
    }
  } catch (error) {
    console.error('Error loading groups:', error);
  }
});

const onSubmit = async (values: any) => {
  loading.value = true;
  errorMessage.value = '';
  
  try {
    const userData = {
      username: formData.value.username,
      email: formData.value.email,
      password: formData.value.password,
      firstName: formData.value.firstName,
      lastName: formData.value.lastName,
      groups: formData.value.selectedGroups,
    };
    
    await userStore.createUser(userData);
    
    // Success - redirect to list
    router.push('/apps/users');
  } catch (error: any) {
    errorMessage.value = error.message || 'Kullanıcı oluşturulurken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-item>
      <h5 class="text-h5 mb-6 font-weight-semibold">Yeni Kullanıcı Oluştur</h5>
      
      <Form
        v-slot="{ handleSubmit }"
        :validation-schema="schema"
        @submit="onSubmit"
      >
        <v-form @submit.prevent="handleSubmit">
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
                  label="Kullanıcı Adı *"
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
                  label="Email *"
                  type="email"
                  variant="outlined"
                  :error-messages="errors"
                  required
                />
              </Field>
            </v-col>

            <!-- Password -->
            <v-col cols="12" md="6">
              <Field name="password" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  v-model="formData.password"
                  label="Şifre *"
                  type="password"
                  variant="outlined"
                  :error-messages="errors"
                  required
                />
              </Field>
            </v-col>

            <!-- Confirm Password -->
            <v-col cols="12" md="6">
              <Field name="confirmPassword" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  v-model="formData.confirmPassword"
                  label="Şifre Tekrar *"
                  type="password"
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
                  label="Ad *"
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
                  label="Soyad *"
                  variant="outlined"
                  :error-messages="errors"
                  required
                />
              </Field>
            </v-col>

            <!-- Groups -->
            <v-col cols="12">
              <v-label class="mb-2">Gruplar</v-label>
              <v-select
                v-model="formData.selectedGroups"
                :items="groups"
                item-title="name"
                item-value="id"
                label="Gruplar Seçiniz"
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
                Kullanıcı otomatik olarak "users" grubuna atanacaktır
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

