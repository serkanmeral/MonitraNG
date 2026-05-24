<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { Form, Field } from 'vee-validate';
import * as yup from 'yup';
import { useLocaleStore } from '@/stores/locale';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useGroupStore } from '@/stores/apps/group';
import { useGroupFieldPolicies } from '@/composables/useGroupFieldPolicies';

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
const route = useRoute();
const router = useRouter();
const groupStore = useGroupStore();

const groupId = route.params.id as string;

const currentGroupRef = computed(() => groupStore.currentGroup);
const { isDirectory, canEdit, sourceLabelKey, sourceChipColor } =
  useGroupFieldPolicies(currentGroupRef);

const page = computed(() => ({ title: t('groups.edit.title') }));
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
    text: t('groups.breadcrumbs.edit'),
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
    errorMessage.value = error.message || t('groups.errors.load');
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
    errorMessage.value = error.message || t('groups.errors.update');
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10" v-if="!loading || groupStore.currentGroup">
    <v-card-item>
      <div class="d-flex align-center flex-wrap ga-3 mb-4">
        <h5 class="text-h5 font-weight-semibold mb-0">{{ t('groups.edit.title') }}</h5>
        <v-chip v-if="groupStore.currentGroup" size="small" variant="tonal" :color="sourceChipColor">
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
        {{ t('groups.directory.editHint') }}
      </v-alert>
      
      <div v-if="loading && !groupStore.currentGroup" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" />
        <p class="text-subtitle-1 mt-4">{{ t('groups.edit.loading') }}</p>
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
                  :label="t('groups.edit.name')"
                  variant="outlined"
                  :error-messages="errors"
                  :disabled="!canEdit"
                  required
                />
              </Field>
            </v-col>

            <!-- Is Active -->
            <v-col cols="12" md="4">
              <v-switch
                v-model="formData.isActive"
                :label="t('groups.edit.isActive')"
                color="success"
                hide-details
                class="mt-4"
                :disabled="!canEdit"
              />
            </v-col>

            <!-- Description -->
            <v-col cols="12">
              <Field name="description" v-slot="{ field, errors }">
                <v-textarea
                  v-bind="field"
                  v-model="formData.description"
                  :label="t('groups.edit.description')"
                  variant="outlined"
                  :error-messages="errors"
                  rows="3"
                  counter="500"
                  :disabled="!canEdit"
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
              {{ t('groups.edit.cancel') }}
            </v-btn>
            <v-btn
              v-if="canEdit"
              color="primary"
              variant="flat"
              type="submit"
              :loading="loading"
            >
              {{ t('groups.edit.save') }}
            </v-btn>
          </div>
        </v-form>
      </Form>
    </v-card-item>
  </v-card>
</template>

