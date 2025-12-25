<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { Form, Field } from 'vee-validate';
import * as yup from 'yup';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useGroupStore } from '@/stores/apps/group';

const route = useRoute();
const router = useRouter();
const groupStore = useGroupStore();

const groupId = route.params.id as string;

const page = ref({ title: 'Grup Düzenle' });
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
    text: 'Grup Düzenle',
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

// Load group data
const loadGroup = async () => {
  loading.value = true;
  try {
    await groupStore.fetchGroupById(groupId);
    
    if (groupStore.currentGroup) {
      const group = groupStore.currentGroup;
      // Update formData - this will trigger reactivity
      formData.value = {
        name: group.name || '',
        description: group.description || '',
        isActive: group.isActive !== undefined ? group.isActive : true,
      };
    }
  } catch (error: any) {
    errorMessage.value = error.message || 'Grup yüklenirken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};

onMounted(async () => {
  await loadGroup();
});

watch(() => route.params.id, async (newId) => {
  if (newId) {
    await loadGroup();
  }
});

const onSubmit = async (values: any) => {
  loading.value = true;
  errorMessage.value = '';
  
  try {
    // FormData'dan değerleri al (v-model ile güncelleniyor)
    const groupData = {
      name: formData.value.name,
      description: formData.value.description || undefined,
      isActive: formData.value.isActive,
    };
    
    await groupStore.updateGroup(groupId, groupData);
    
    // Success - redirect to list with refresh parameter
    router.push({ path: '/apps/groups', query: { refresh: Date.now() } });
  } catch (error: any) {
    errorMessage.value = error.message || 'Grup güncellenirken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10" v-if="!loading || groupStore.currentGroup">
    <v-card-item>
      <h5 class="text-h5 mb-6 font-weight-semibold">Grup Düzenle</h5>
      
      <div v-if="loading && !groupStore.currentGroup" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" />
        <p class="text-subtitle-1 mt-4">Yükleniyor...</p>
      </div>

      <Form
        v-else-if="groupStore.currentGroup"
        v-slot="{ handleSubmit }"
        :validation-schema="schema"
        :initial-values="formData"
        :key="`form-${groupId}`"
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
            <!-- Group Name -->
            <v-col cols="12" md="8">
              <Field name="name" v-slot="{ field, errors }">
                <v-text-field
                  v-bind="field"
                  v-model="formData.name"
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
                  v-model="formData.description"
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
              Kaydet
            </v-btn>
          </div>
        </v-form>
      </Form>
    </v-card-item>
  </v-card>
</template>

