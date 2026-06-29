<script setup lang="ts">
import { ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  ocCreateTag,
  ocDeleteTag,

  ocListTagsForWorkspace,
  ocUpdateTag,
} from '@/services/operationCoreService';
import type { OpTag } from '@/types/apps/operationCore';
import { TM_STATUS_THEME_COLORS, isTmStatusThemeColor } from '@/utils/taskManagerStatusColor';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);

const tags = ref<OpTag[]>([]);

const dialogOpen = ref(false);
const editingId = ref<string | null>(null);
const form = ref<{ name: string; color: string | null; description: string }>({
  name: '',
  color: null,
  description: '',
});

const deleteDialogOpen = ref(false);
const deleteTarget = ref<OpTag | null>(null);

const colorItems = [
  { title: t('operationCore.tags.noColor'), value: null as string | null },
  ...TM_STATUS_THEME_COLORS.map((c) => ({ title: c, value: c as string | null })),
];

async function loadAll() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    tags.value = await ocListTagsForWorkspace(props.workspaceId);
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.tags.loadError');
  } finally {
    loading.value = false;
  }
}

watch(() => props.workspaceId, () => void loadAll(), { immediate: true });

function openCreate() {
  editingId.value = null;
  form.value = { name: '', color: null, description: '' };
  dialogOpen.value = true;
}

function openEdit(tag: OpTag) {
  editingId.value = tag.__dataId;
  form.value = { name: tag.name, color: tag.color ?? null, description: tag.description ?? '' };
  dialogOpen.value = true;
}

async function save() {
  const name = form.value.name.trim();
  if (!name || !props.workspaceId) return;
  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    const payload: Record<string, unknown> = {
      name,
      workspaceId: props.workspaceId,
      color: form.value.color || null,
      description: form.value.description.trim() || null,
    };
    if (editingId.value) {
      await ocUpdateTag(editingId.value, payload);
    } else {
      await ocCreateTag(payload);
    }
    dialogOpen.value = false;
    await loadAll();
    successLocal.value = t('operationCore.workspaceDefinitions.saveSuccess');
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.tags.saveError');
  } finally {
    saving.value = false;
  }
}

function confirmDelete(tag: OpTag) {
  deleteTarget.value = tag;
  deleteDialogOpen.value = true;
}

async function doDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteTag(deleteTarget.value.__dataId);
    deleteDialogOpen.value = false;
    deleteTarget.value = null;
    await loadAll();
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.tags.deleteError');
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-tags-tab pa-4 pa-md-6">
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

    <div class="d-flex align-start justify-space-between ga-3 flex-wrap mb-4">
      <div>
        <h3 class="text-subtitle-1 font-weight-medium mb-1">
          {{ t('operationCore.tags.catalogTitle') }}
        </h3>
        <p class="text-body-2 text-medium-emphasis mb-0">
          {{ t('operationCore.tags.catalogSubtitle') }}
        </p>
      </div>
      <v-btn
        color="primary"
        rounded="lg"
        class="text-none"
        prepend-icon="mdi-plus"
        @click="openCreate"
      >
        {{ t('operationCore.tags.add') }}
      </v-btn>
    </div>

    <div v-if="loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <template v-else>
      <v-alert v-if="!tags.length" type="info" variant="tonal" density="compact">
        {{ t('operationCore.tags.empty') }}
      </v-alert>

      <v-card v-else variant="outlined" rounded="lg">
        <v-list lines="two" density="comfortable">
          <v-list-item
            v-for="(tag, idx) in tags"
            :key="tag.__dataId"
            :border="idx > 0 ? 'top' : undefined"
          >
            <template #prepend>
              <v-chip
                size="small"
                variant="tonal"
                :color="tag.color && isTmStatusThemeColor(tag.color) ? tag.color : undefined"
                class="text-none"
              >
                {{ tag.name }}
              </v-chip>
            </template>
            <v-list-item-subtitle v-if="tag.description">
              {{ tag.description }}
            </v-list-item-subtitle>
            <template #append>
              <v-btn
                icon="mdi-pencil-outline"
                variant="text"
                size="small"
                :aria-label="t('operationCore.tags.edit')"
                @click="openEdit(tag)"
              />
              <v-btn
                icon="mdi-delete-outline"
                variant="text"
                size="small"
                color="error"
                :aria-label="t('operationCore.tags.delete')"
                @click="confirmDelete(tag)"
              />
            </template>
          </v-list-item>
        </v-list>
      </v-card>
    </template>

    <v-dialog v-model="dialogOpen" max-width="480" persistent>
      <v-card rounded="lg">
        <v-card-title class="text-h6">
          {{ editingId ? t('operationCore.tags.editTitle') : t('operationCore.tags.addTitle') }}
        </v-card-title>
        <v-card-text>
          <v-text-field
            v-model="form.name"
            :label="t('operationCore.tags.name')"
            variant="outlined"
            density="comfortable"
            autofocus
            class="mb-3"
            hide-details="auto"
          />
          <v-select
            v-model="form.color"
            :items="colorItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.tags.color')"
            variant="outlined"
            density="comfortable"
            class="mb-3"
            hide-details="auto"
          >
            <template #selection="{ item }">
              <v-chip
                v-if="item.value"
                size="small"
                variant="tonal"
                :color="String(item.value)"
                class="text-none"
              >
                {{ item.title }}
              </v-chip>
              <span v-else class="text-medium-emphasis">{{ item.title }}</span>
            </template>
          </v-select>
          <v-textarea
            v-model="form.description"
            :label="t('operationCore.tags.description')"
            variant="outlined"
            density="comfortable"
            rows="2"
            auto-grow
            hide-details="auto"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="dialogOpen = false">
            {{ t('operationCore.tags.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="saving"
            :disabled="!form.name.trim()"
            @click="save"
          >
            {{ t('operationCore.tags.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialogOpen" max-width="420">
      <v-card rounded="lg">
        <v-card-title class="text-h6">{{ t('operationCore.tags.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('operationCore.tags.deleteConfirm', { name: deleteTarget?.name ?? '' }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialogOpen = false">
            {{ t('operationCore.tags.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" class="text-none" :loading="deleting" @click="doDelete">
            {{ t('operationCore.tags.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
