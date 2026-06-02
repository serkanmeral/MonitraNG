<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOcWorkspaceCatalogInject } from '@/composables/useOcWorkspaceCatalog';
import { ocExtractDgErrorMessage, ocGetWorkspace, ocUpdateWorkspace } from '@/services/operationCoreService';
import { useGroupStore } from '@/stores/apps/group';
import type { OpWorkspaceDetail } from '@/types/apps/operationCore';
import { OC_WORKSPACE_TYPE_VALUES } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceId: string;
}>();

const { t, locale } = useAppI18n();
const groupStore = useGroupStore();
const catalog = useOcWorkspaceCatalogInject();

/** i18n message compiler `{seq:D4}` gibi süslü parantezleri parse edemez; hint doğrudan metin. */
const keyFormatHintText = computed(() =>
  locale() === 'en'
    ? 'E.g. HD-0001 — prefix plus 4-digit sequence number'
    : 'Örn. HD-0001 — prefix artı 4 haneli sıra numarası'
);

const loading = ref(true);
const saving = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);
const workspace = ref<OpWorkspaceDetail | null>(null);

const form = ref({
  name: '',
  description: '',
  workspaceType: 'team' as string,
  workItemKeyPrefix: '',
  workItemKeyFormat: '{prefix}-{seq:D4}',
  workItemSequenceStart: '' as string,
  viewGroups: [] as string[],
  editGroups: [] as string[],
  adminGroups: [] as string[],
});

const workspaceTypeItems = computed(() =>
  OC_WORKSPACE_TYPE_VALUES.map((value) => ({
    value,
    title: t(`operationCore.workspaceDefinitions.general.workspaceType.${value}`),
  }))
);

const groupItems = computed(() =>
  (groupStore.groups || []).map((g) => ({
    title: g.name,
    value: g.id || g.groupId,
  }))
);

onMounted(() => {
  if (!groupStore.groups?.length) {
    void groupStore.fetchGroups();
  }
});

async function loadWorkspace() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    const ws = await ocGetWorkspace(props.workspaceId);
    workspace.value = ws;
    if (ws) {
      form.value = {
        name: ws.name,
        description: ws.description ?? '',
        workspaceType: ws.workspaceType ?? 'team',
        workItemKeyPrefix: ws.workItemKeyPrefix ?? '',
        workItemKeyFormat: ws.workItemKeyFormat ?? '{prefix}-{seq:D4}',
        workItemSequenceStart:
          ws.workItemSequenceStart != null ? String(ws.workItemSequenceStart) : '',
        viewGroups: [...(ws.viewGroups ?? [])],
        editGroups: [...(ws.editGroups ?? [])],
        adminGroups: [...(ws.adminGroups ?? [])],
      };
    }
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.general.loadError')
    );
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.workspaceId,
  () => {
    void loadWorkspace();
  },
  { immediate: true }
);

async function saveGeneral() {
  if (!props.workspaceId || !form.value.name.trim()) return;
  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  const seqRaw = form.value.workItemSequenceStart.trim();
  const seqNum = seqRaw === '' ? null : Number(seqRaw);
  try {
    await ocUpdateWorkspace(props.workspaceId, {
      name: form.value.name.trim(),
      description: form.value.description.trim() || null,
      workspaceType: form.value.workspaceType || null,
      workItemKeyPrefix: form.value.workItemKeyPrefix.trim() || null,
      workItemKeyFormat: form.value.workItemKeyFormat.trim() || null,
      workItemSequenceStart: Number.isFinite(seqNum) ? seqNum : null,
      viewGroups: form.value.viewGroups,
      editGroups: form.value.editGroups,
      adminGroups: form.value.adminGroups,
    });
    await catalog.refresh();
    await loadWorkspace();
    successLocal.value = t('operationCore.workspaceDefinitions.saveSuccess');
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.general.saveError')
    );
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-general-tab pa-4 pa-md-6">
    <v-alert
      v-if="errorLocal"
      type="error"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="errorLocal = null"
    >
      {{ errorLocal }}
    </v-alert>

    <v-alert
      v-if="successLocal"
      type="success"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="successLocal = null"
    >
      {{ successLocal }}
    </v-alert>

    <p class="text-body-2 text-medium-emphasis mb-4">
      {{ t('operationCore.workspaceDefinitions.general.subtitle') }}
    </p>

    <div v-if="loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <template v-else-if="workspace">
      <v-alert v-if="workspace.key" type="info" variant="tonal" density="compact" class="mb-4">
        {{ t('operationCore.workspaceDefinitions.general.keyReadonly') }}
        <code class="ml-1">{{ workspace.key }}</code>
      </v-alert>

      <v-text-field
        v-model="form.name"
        :label="t('operationCore.workspaceDefinitions.general.fieldName')"
        density="comfortable"
        required
      />
      <v-textarea
        v-model="form.description"
        class="mt-3"
        :label="t('operationCore.workspaceDefinitions.general.fieldDescription')"
        rows="2"
        auto-grow
        density="comfortable"
        variant="outlined"
      />
      <v-select
        v-model="form.workspaceType"
        class="mt-3"
        :items="workspaceTypeItems"
        item-title="title"
        item-value="value"
        :label="t('operationCore.workspaceDefinitions.general.fieldWorkspaceType')"
        density="comfortable"
      />
      <v-text-field
        v-model="form.workItemKeyPrefix"
        class="mt-3"
        :label="t('operationCore.workspaceDefinitions.general.fieldKeyPrefix')"
        :hint="t('operationCore.workspaceDefinitions.general.keyPrefixHint')"
        persistent-hint
        density="comfortable"
      />
      <v-text-field
        v-model="form.workItemKeyFormat"
        class="mt-3"
        :label="t('operationCore.workspaceDefinitions.general.fieldKeyFormat')"
        :hint="keyFormatHintText"
        persistent-hint
        density="comfortable"
      />
      <v-text-field
        v-model="form.workItemSequenceStart"
        class="mt-3"
        type="number"
        :label="t('operationCore.workspaceDefinitions.general.fieldSequenceStart')"
        density="comfortable"
      />

      <v-divider class="my-5" />

      <div class="text-subtitle-2 font-weight-medium mb-1">
        {{ t('operationCore.workspaceDefinitions.general.accessSection') }}
      </div>
      <p class="text-caption text-medium-emphasis mb-3">
        {{ t('operationCore.workspaceDefinitions.general.accessHint') }}
      </p>
      <v-row dense>
        <v-col cols="12" md="4">
          <v-select
            v-model="form.viewGroups"
            :items="groupItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.general.fieldViewGroups')"
            variant="outlined"
            density="compact"
            multiple
            chips
            closable-chips
            clearable
          />
        </v-col>
        <v-col cols="12" md="4">
          <v-select
            v-model="form.editGroups"
            :items="groupItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.general.fieldEditGroups')"
            variant="outlined"
            density="compact"
            multiple
            chips
            closable-chips
            clearable
          />
        </v-col>
        <v-col cols="12" md="4">
          <v-select
            v-model="form.adminGroups"
            :items="groupItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.general.fieldAdminGroups')"
            variant="outlined"
            density="compact"
            multiple
            chips
            closable-chips
            clearable
          />
        </v-col>
      </v-row>

      <div class="d-flex justify-end mt-6">
        <v-btn
          color="primary"
          rounded="lg"
          class="text-none"
          :loading="saving"
          :disabled="!form.name.trim()"
          @click="saveGeneral"
        >
          {{ t('operationCore.definitions.save') }}
        </v-btn>
      </div>
    </template>

    <v-alert v-else type="warning" variant="tonal">
      {{ t('operationCore.workspaceDefinitions.general.notFound') }}
    </v-alert>
  </div>
</template>
