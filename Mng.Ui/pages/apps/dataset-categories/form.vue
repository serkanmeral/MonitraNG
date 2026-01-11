<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useDatasetCategoryStore } from '@/stores/apps/datasetCategory';
import { useAuthStore } from '@/stores/auth';
import { TagIcon, CheckIcon, XIcon, ArrowLeftIcon } from 'vue-tabler-icons';

const route = useRoute();
const router = useRouter();
const categoryStore = useDatasetCategoryStore();
const authStore = useAuthStore();

// Props to determine mode (can be used as component or page)
const props = defineProps<{
  editDataId?: string;
}>();

// Check if edit mode
const isEditMode = computed(() => {
  // If used as component with prop (from edit/[dataId].vue)
  if (props.editDataId) {
    return true;
  }
  // Check if route path is /create (not edit mode)
  if (route.path.includes('/create')) {
    return false;
  }
  // Check route params - if dataId exists and is not 'create', it's edit mode
  return !!route.params.dataId && route.params.dataId !== 'create';
});

const dataId = computed(() => {
  // If used as component with prop (from edit/[dataId].vue)
  if (props.editDataId) {
    return props.editDataId;
  }
  // Get from route params (should be undefined for create route)
  return route.params.dataId as string | undefined;
});

const page = computed(() => ({
  title: isEditMode.value ? 'Kategori Düzenle' : 'Yeni Kategori',
}));

const breadcrumbs = computed(() => [
  {
    text: 'Dashboard',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Dataset Kategorileri',
    disabled: false,
    href: '/apps/dataset-categories',
  },
  {
    text: page.value.title,
    disabled: true,
    href: '#',
  },
]);

// Form data
const formData = ref({
  categoryName: '',
  categoryDescription: '',
});

// Validation rules
const categoryNameRules = [
  (v: string) => !!v || 'Kategori adı gereklidir',
  (v: string) => (v && v.length >= 2) || 'Kategori adı en az 2 karakter olmalıdır',
  (v: string) => (v && v.length <= 100) || 'Kategori adı en fazla 100 karakter olabilir',
];

const categoryDescriptionRules = [
  (v: string) => !v || v.length <= 500 || 'Açıklama en fazla 500 karakter olabilir',
];

// Form state
const formRef = ref();
const loading = ref(false);
const errorMessage = ref('');
const successMessage = ref('');

// Load category if edit mode
onMounted(async () => {
  // Only load category if in edit mode and dataId exists
  if (isEditMode.value && dataId.value && dataId.value.trim()) {
    loading.value = true;
    try {
      const category = await categoryStore.fetchCategoryById(dataId.value);
      if (category) {
        formData.value = {
          categoryName: category.categoryName,
          categoryDescription: category.categoryDescription || '',
        };
      } else {
        errorMessage.value = 'Kategori bulunamadı';
        setTimeout(() => {
          router.push('/apps/dataset-categories');
        }, 2000);
      }
    } catch (error: any) {
      errorMessage.value = error.message || 'Kategori yüklenirken bir hata oluştu';
      setTimeout(() => {
        router.push('/apps/dataset-categories');
      }, 2000);
    } finally {
      loading.value = false;
    }
  }
  // If create mode, form will be empty (default state)
});

// Handle form submit
const handleSubmit = async () => {
  errorMessage.value = '';
  successMessage.value = '';
  
  // Validate form
  const { valid } = await formRef.value.validate();
  if (!valid) {
    return;
  }
  
  loading.value = true;
  
  try {
    if (isEditMode.value && dataId.value) {
      // Update existing category
      await categoryStore.updateCategory(dataId.value, {
        categoryName: formData.value.categoryName.trim(),
        categoryDescription: formData.value.categoryDescription.trim() || undefined,
      });
      
      successMessage.value = 'Kategori başarıyla güncellendi!';
    } else {
      // Create new category
      await categoryStore.createCategory({
        categoryName: formData.value.categoryName.trim(),
        categoryDescription: formData.value.categoryDescription.trim() || undefined,
      });
      
      successMessage.value = 'Kategori başarıyla oluşturuldu!';
    }
    
    // Redirect to list after success
    setTimeout(() => {
      router.push('/apps/dataset-categories');
    }, 1500);
  } catch (error: any) {
    errorMessage.value = error.message || 'Kategori kaydedilirken bir hata oluştu';
    
    // Handle duplicate category name error
    if (error.message && error.message.includes('zaten mevcut')) {
      errorMessage.value = 'Bu kategori adı zaten kullanılıyor. Lütfen farklı bir ad seçin.';
    }
  } finally {
    loading.value = false;
  }
};

// Handle cancel
const handleCancel = () => {
  router.push('/apps/dataset-categories');
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-title class="pa-4 bg-primary text-white d-flex align-center">
      <TagIcon class="mr-2" size="24" />
      <span>{{ page.title }}</span>
    </v-card-title>
    
    <v-divider></v-divider>
    
    <v-card-text class="pa-6">
      <!-- Error Message -->
      <v-alert
        v-if="errorMessage"
        type="error"
        variant="tonal"
        density="compact"
        class="mb-4"
        closable
        @click:close="errorMessage = ''"
      >
        {{ errorMessage }}
      </v-alert>
      
      <!-- Success Message -->
      <v-alert
        v-if="successMessage"
        type="success"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        {{ successMessage }}
      </v-alert>
      
      <!-- Admin/Manager Warning -->
      <v-alert
        v-if="!authStore.isManager"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        <strong>Uyarı:</strong> Bu sayfaya erişim için manager veya admin yetkisi gereklidir.
      </v-alert>
      
      <!-- Loading State (only for edit mode when fetching data) -->
      <div v-if="loading && isEditMode" class="text-center py-12">
        <v-progress-circular
          indeterminate
          color="primary"
          size="64"
        ></v-progress-circular>
        <p class="text-subtitle-1 mt-4 text-medium-emphasis">Kategori yükleniyor...</p>
      </div>
      
      <!-- Form (always visible in create mode, visible in edit mode after data is loaded) -->
      <v-form
        v-else
        ref="formRef"
        @submit.prevent="handleSubmit"
      >
        <v-row>
          <!-- Category Name -->
          <v-col cols="12">
            <v-text-field
              v-model="formData.categoryName"
              :rules="categoryNameRules"
              label="Kategori Adı *"
              placeholder="Örn: System Datasets, Book Datasets"
              variant="outlined"
              required
              :disabled="loading"
              hint="2-100 karakter arasında, benzersiz olmalıdır"
              persistent-hint
              prepend-inner-icon="mdi-tag"
            ></v-text-field>
          </v-col>
          
          <!-- Category Description -->
          <v-col cols="12">
            <v-textarea
              v-model="formData.categoryDescription"
              :rules="categoryDescriptionRules"
              label="Açıklama"
              placeholder="Kategori açıklaması (opsiyonel)"
              variant="outlined"
              rows="4"
              :disabled="loading"
              hint="En fazla 500 karakter"
              persistent-hint
              prepend-inner-icon="mdi-text"
              auto-grow
            ></v-textarea>
          </v-col>
        </v-row>
        
        <!-- Form Actions -->
        <v-row class="mt-4">
          <v-col cols="12" class="d-flex justify-end ga-3">
            <v-btn
              color="default"
              variant="outlined"
              @click="handleCancel"
              :disabled="loading"
            >
              <XIcon class="mr-2" size="18" />
              İptal
            </v-btn>
            
            <v-btn
              color="primary"
              variant="flat"
              type="submit"
              :loading="loading"
              :disabled="!formData.categoryName.trim()"
            >
              <CheckIcon class="mr-2" size="18" />
              {{ isEditMode ? 'Güncelle' : 'Oluştur' }}
            </v-btn>
          </v-col>
        </v-row>
      </v-form>
    </v-card-text>
  </v-card>
</template>
