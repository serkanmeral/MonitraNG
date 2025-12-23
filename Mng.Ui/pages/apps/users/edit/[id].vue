<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { Form, Field } from 'vee-validate';
import * as yup from 'yup';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useUserStore } from '@/stores/apps/user';
import { fetchFromMngKeeper } from '@/services/apiService';

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();

const userId = route.params.id as string;

const page = ref({ title: 'Kullanıcı Düzenle' });
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
    text: 'Kullanıcı Düzenle',
    disabled: true,
    href: '#',
  },
]);

const loading = ref(false);
const errorMessage = ref('');
const groups = ref<Array<{ id: string; name: string }>>([]);

// Form data
const formData = ref({
  email: '',
  firstName: '',
  lastName: '',
  isActive: true,
  selectedGroups: [] as string[],
});

// Validation schema
const schema = yup.object({
  email: yup.string().email('Geçerli bir email adresi giriniz').required('Email gereklidir'),
  firstName: yup.string().required('Ad gereklidir'),
  lastName: yup.string().required('Soyad gereklidir'),
});

// Load user data
const loadUser = async () => {
  loading.value = true;
  try {
    await userStore.fetchUserById(userId);
    
    if (userStore.currentUser) {
      const user = userStore.currentUser;
      formData.value = {
        email: user.email || '',
        firstName: user.firstName || '',
        lastName: user.lastName || '',
        isActive: user.isActive,
        selectedGroups: user.groups || [],
      };
    }
  } catch (error: any) {
    errorMessage.value = error.message || 'Kullanıcı yüklenirken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};

// Load groups
const loadGroups = async () => {
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
    const userData = {
      email: formData.value.email,
      firstName: formData.value.firstName,
      lastName: formData.value.lastName,
      isActive: formData.value.isActive,
      groups: formData.value.selectedGroups,
    };
    
    await userStore.updateUser(userId, userData);
    
    // Success - redirect to list
    router.push('/apps/users');
  } catch (error: any) {
    errorMessage.value = error.message || 'Kullanıcı güncellenirken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10" v-if="!loading || userStore.currentUser">
    <v-card-item>
      <h5 class="text-h5 mb-6 font-weight-semibold">Kullanıcı Düzenle</h5>
      
      <div v-if="loading && !userStore.currentUser" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" />
        <p class="text-subtitle-1 mt-4">Yükleniyor...</p>
      </div>

      <Form
        v-else
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
            <!-- Username (Read-only) -->
            <v-col cols="12" md="6">
              <v-text-field
                :model-value="userStore.currentUser?.username || ''"
                label="Kullanıcı Adı"
                variant="outlined"
                disabled
              />
              <div class="text-caption text-medium-emphasis mt-1">
                Kullanıcı adı değiştirilemez
              </div>
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

            <!-- Is Active -->
            <v-col cols="12" md="6">
              <v-switch
                v-model="formData.isActive"
                label="Aktif"
                color="success"
                hide-details
              />
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
              Kaydet
            </v-btn>
          </div>
        </v-form>
      </Form>
    </v-card-item>
  </v-card>
</template>

