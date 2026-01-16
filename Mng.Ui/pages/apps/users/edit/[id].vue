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
const formKey = ref(0); // Key for form re-rendering

// Form data
const formData = ref({
  email: '',
  firstName: '',
  lastName: '',
  isActive: true,
  selectedGroups: [] as string[],
  title: null as string | null,
  department: null as string | null,
  phoneNumber: null as string | null,
  photoUrl: null as string | null,
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
    
    if (userStore.viewingUser) {
      const user = userStore.viewingUser;
      formData.value = {
        email: user.email || '',
        firstName: user.firstName || '',
        lastName: user.lastName || '',
        isActive: user.isActive,
        selectedGroups: user.groups || [],
        title: user.title || null,
        department: user.department || null,
        phoneNumber: user.phoneNumber || null,
        photoUrl: user.photoUrl || null,
      };
      // Force form re-render to pick up initial values
      formKey.value++;
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
      throw new Error('Kullanıcı adı bulunamadı');
    }
    
    // Get current user data to preserve fields not in the form
    const currentUser = userStore.viewingUser;
    if (!currentUser) {
      throw new Error('Kullanıcı bilgileri bulunamadı');
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
      username: username, // Required by backend
      email: formData.value.email,
      firstName: formData.value.firstName,
      lastName: formData.value.lastName,
      isActive: formData.value.isActive,
    };
    
    // Only include groups if they were actually changed
    // If groups weren't changed, don't send them - backend will preserve existing groups
    if (groupsChanged) {
      userData.groups = selectedGroups;
    }
    
    // Include nullable fields from form data
    // These fields are now in the form, so we always send them (even if null)
    if (formData.value.title !== undefined) {
      userData.title = formData.value.title || null;
    }
    if (formData.value.department !== undefined) {
      userData.department = formData.value.department || null;
    }
    if (formData.value.phoneNumber !== undefined) {
      userData.phoneNumber = formData.value.phoneNumber || null;
    }
    if (formData.value.photoUrl !== undefined) {
      userData.photoUrl = formData.value.photoUrl || null;
    }
    
    await userStore.updateUser(userId, userData);
    
    // Success - redirect to list with refresh parameter
    router.push({ path: '/apps/users', query: { refresh: Date.now() } });
  } catch (error: any) {
    console.error('[UserEdit] Error updating user:', error);
    errorMessage.value = error.message || 'Kullanıcı güncellenirken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10" v-if="!loading || userStore.viewingUser">
    <v-card-item>
      <h5 class="text-h5 mb-6 font-weight-semibold">Kullanıcı Düzenle</h5>
      
      <div v-if="loading && !userStore.viewingUser" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" />
        <p class="text-subtitle-1 mt-4">Yükleniyor...</p>
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
            <!-- Username (Read-only) -->
            <v-col cols="12" md="6">
              <v-text-field
                :model-value="userStore.viewingUser?.username || ''"
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

            <!-- Title -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.title"
                label="Ünvan"
                variant="outlined"
                placeholder="Örn: Manager, Developer, QA Engineer"
              />
            </v-col>

            <!-- Department -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.department"
                label="Departman"
                variant="outlined"
                placeholder="Örn: IT, Development, QA"
              />
            </v-col>

            <!-- Phone Number -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.phoneNumber"
                label="Telefon Numarası"
                variant="outlined"
                placeholder="+90XXXXXXXXXX"
              />
            </v-col>

            <!-- Photo URL (Read-only) -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.photoUrl"
                label="Fotoğraf URL"
                variant="outlined"
                placeholder="https://..."
                disabled
              />
              <div class="text-caption text-medium-emphasis mt-1">
                Fotoğraf URL değiştirilemez
              </div>
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

