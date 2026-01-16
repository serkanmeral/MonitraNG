<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useDatasetCategoryStore } from '@/stores/apps/datasetCategory';
import { useAuthStore } from '@/stores/auth';
import { TagIcon, CheckIcon, XIcon, ArrowLeftIcon } from 'vue-tabler-icons';

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
  title: isEditMode.value ? t('datasetCategories.form.edit.title') : t('datasetCategories.form.create.title'),
}));

const breadcrumbs = computed(() => [
  {
    text: t('datasetCategories.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('datasetCategories.breadcrumbs.categories'),
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
const categoryNameRules = computed(() => [
  (v: string) => !!v || t('datasetCategories.form.validation.categoryNameRequired'),
  (v: string) => (v && v.length >= 2) || t('datasetCategories.form.validation.categoryNameMinLength'),
  (v: string) => (v && v.length <= 100) || t('datasetCategories.form.validation.categoryNameMaxLength'),
]);

const categoryDescriptionRules = computed(() => [
  (v: string) => !v || v.length <= 500 || t('datasetCategories.form.validation.descriptionMaxLength'),
]);

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
        errorMessage.value = t('datasetCategories.form.messages.notFound');
        setTimeout(() => {
          router.push('/apps/dataset-categories');
        }, 2000);
      }
    } catch (error: any) {
      errorMessage.value = error.message || t('datasetCategories.form.messages.loadError');
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
      
      successMessage.value = t('datasetCategories.form.messages.updateSuccess');
    } else {
      // Create new category
      await categoryStore.createCategory({
        categoryName: formData.value.categoryName.trim(),
        categoryDescription: formData.value.categoryDescription.trim() || undefined,
      });
      
      successMessage.value = t('datasetCategories.form.messages.createSuccess');
    }
    
    // Redirect to list after success
    setTimeout(() => {
      router.push('/apps/dataset-categories');
    }, 1500);
  } catch (error: any) {
    errorMessage.value = error.message || t('datasetCategories.form.messages.saveError');
    
    // Handle duplicate category name error
    if (error.message && (error.message.includes('zaten mevcut') || error.message.includes('already exists'))) {
      errorMessage.value = t('datasetCategories.form.validation.duplicateName');
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
        <strong>{{ t('datasetCategories.warning.title') }}</strong> {{ t('datasetCategories.warning.message') }}
      </v-alert>
      
      <!-- Loading State (only for edit mode when fetching data) -->
      <div v-if="loading && isEditMode" class="text-center py-12">
        <v-progress-circular
          indeterminate
          color="primary"
          size="64"
        ></v-progress-circular>
        <p class="text-subtitle-1 mt-4 text-medium-emphasis">{{ t('datasetCategories.form.messages.loading') }}</p>
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
              :label="t('datasetCategories.form.fields.categoryName') + ' *'"
              :placeholder="t('datasetCategories.form.fields.categoryNamePlaceholder')"
              variant="outlined"
              required
              :disabled="loading"
              :hint="t('datasetCategories.form.fields.categoryNameHint')"
              persistent-hint
              prepend-inner-icon="mdi-tag"
            ></v-text-field>
          </v-col>
          
          <!-- Category Description -->
          <v-col cols="12">
            <v-textarea
              v-model="formData.categoryDescription"
              :rules="categoryDescriptionRules"
              :label="t('datasetCategories.form.fields.description')"
              :placeholder="t('datasetCategories.form.fields.descriptionPlaceholder')"
              variant="outlined"
              rows="4"
              :disabled="loading"
              :hint="t('datasetCategories.form.fields.descriptionHint')"
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
              {{ t('datasetCategories.buttons.cancel') }}
            </v-btn>
            
            <v-btn
              color="primary"
              variant="flat"
              type="submit"
              :loading="loading"
              :disabled="!formData.categoryName.trim()"
            >
              <CheckIcon class="mr-2" size="18" />
              {{ isEditMode ? t('datasetCategories.form.edit.submit') : t('datasetCategories.form.create.submit') }}
            </v-btn>
          </v-col>
        </v-row>
      </v-form>
    </v-card-text>
  </v-card>
</template>
