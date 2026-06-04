<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AcWorkflowEditor from '@/components/apps/automation-center/workflows/AcWorkflowEditor.vue';
import { useAutomationCenterBreadcrumbs } from '@/composables/useAutomationCenterBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';
import { workflowDefinitionGet } from '@/services/workflowService';

definePageMeta({ layout: 'default' });

const route = useRoute();
const { t } = useAppI18n();
const auth = useAuthStore();

const workflowId = computed(() => String(route.params.workflowId ?? ''));
const workflowName = ref<string | null>(null);

const { breadcrumbs } = useAutomationCenterBreadcrumbs({
  tail: computed(() => ({
    text: workflowName.value ?? t('automationCenter.workflows.editorTitle'),
    disabled: true,
  })),
});

onMounted(async () => {
  if (!auth.isManager) {
    void navigateTo('/unauthorized');
    return;
  }
  try {
    const doc = await workflowDefinitionGet(workflowId.value);
    workflowName.value = doc.name;
  } catch {
    workflowName.value = null;
  }
});
</script>

<template>
  <div class="ac-flow ac-workflow-editor-page">
    <BaseBreadcrumb
      :title="workflowName ?? t('automationCenter.workflows.editorTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="d-flex flex-wrap align-center ga-2 mb-4">
      <v-btn
        variant="text"
        prepend-icon="mdi-arrow-left"
        to="/apps/automation-center/workflows"
      >
        {{ t('automationCenter.workflows.backToList') }}
      </v-btn>
    </div>

    <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
      <AcWorkflowEditor :workflow-id="workflowId" />
    </v-card>
  </div>
</template>
