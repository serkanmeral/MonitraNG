<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { ocCreateWorkspace } from '@/services/operationCoreService';
import { ocListWorkspaceFolders } from '@/services/operationCore/workspaceFolders';
import { ocInstallWorkspacePack, ocListWorkspacePacks, type OcWorkspacePackSummary } from '@/services/operationCore/workspacePacks';
import { OC_WORKSPACE_TYPE_VALUES, type OpWorkspaceFolder } from '@/types/apps/operationCore';
import { resolveOcWorkspaceLibrary } from '@/utils/ocWorkspaceLibrary';

const props = defineProps<{
  modelValue: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  created: [string];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const step = ref(0);
const saving = ref(false);
const loadingOptions = ref(false);
const errorLocal = ref<string | null>(null);
const createdId = ref<string | null>(null);
const finishedId = ref<string | null>(null);
const folders = ref<OpWorkspaceFolder[]>([]);
const packs = ref<OcWorkspacePackSummary[]>([]);

const form = ref({
  name: '',
  workspaceType: 'team',
  workItemKeyPrefix: '',
  folderId: '' as string,
  viewGroups: [] as string[],
  editGroups: [] as string[],
  packCode: '',
  boardNames: {} as Record<string, string>,
});

const stepLabels = computed(() => [
  t('operationCore.workspaceDefinitions.create.stepIdentity'),
  t('operationCore.workspaceDefinitions.create.stepPlace'),
  t('operationCore.workspaceDefinitions.create.stepPack'),
]);

const workspaceTypeItems = computed(() =>
  OC_WORKSPACE_TYPE_VALUES.map((value) => ({
    value,
    title: t(`operationCore.workspaceDefinitions.general.workspaceType.${value}`),
  }))
);

const folderItems = computed(() => {
  const byId = new Map(folders.value.map((folder) => [folder.__dataId, folder]));
  const titled = folders.value.map((folder) => ({
    value: folder.__dataId,
    title: folderPath(folder, byId),
  }));
  titled.sort((a, b) => a.title.localeCompare(b.title, 'tr'));
  return [
    { value: '', title: t('operationCore.workspaceDefinitions.create.folderRoot') },
    ...titled,
  ];
});

const selectedPack = computed(() => packs.value.find((pack) => pack.code === form.value.packCode) ?? null);

function boardNameLabel(name: string): string {
  const boards = selectedPack.value?.boards ?? [];
  if (boards.length < 2) return t('operationCore.workspaceDefinitions.create.boardName');
  return `${t('operationCore.workspaceDefinitions.create.boardName')} · ${name}`;
}

const suggestionText = computed(() => {
  const pack = selectedPack.value;
  if (!pack?.suggestedWorkspaceType && !pack?.suggestedPrefix) return '';
  const typeTitle = pack.suggestedWorkspaceType
    ? t(`operationCore.workspaceDefinitions.general.workspaceType.${pack.suggestedWorkspaceType}`)
    : '';
  return t('operationCore.workspaceDefinitions.create.packSuggestion', {
    type: typeTitle || '—',
    prefix: pack.suggestedPrefix || '—',
  });
});

const canNext = computed(() => {
  if (saving.value || createdId.value || finishedId.value) return false;
  if (step.value === 0) return form.value.name.trim().length > 0 && form.value.workItemKeyPrefix.trim().length > 0;
  return true;
});

const savedId = computed(() => createdId.value || finishedId.value);
const canFinish = computed(() => canNext.value && !loadingOptions.value);

function folderPath(folder: OpWorkspaceFolder, byId: Map<string, OpWorkspaceFolder>): string {
  const names: string[] = [];
  const seen = new Set<string>();
  let current: OpWorkspaceFolder | undefined = folder;
  while (current && !seen.has(current.__dataId)) {
    seen.add(current.__dataId);
    names.unshift(current.name);
    const parentId: string | null = current.parentFolderId;
    current = parentId ? byId.get(parentId) : undefined;
  }
  return names.join(' / ');
}

function resetForm() {
  step.value = 0;
  saving.value = false;
  errorLocal.value = null;
  createdId.value = null;
  finishedId.value = null;
  form.value = {
    name: '',
    workspaceType: 'team',
    workItemKeyPrefix: '',
    folderId: '',
    viewGroups: [],
    editGroups: [],
    packCode: '',
    boardNames: {},
  };
}

function applyPackBoardNames(code: string, overwrite: boolean) {
  if (!overwrite && Object.keys(form.value.boardNames).length > 0) return;
  const pack = packs.value.find((item) => item.code === code);
  const next: Record<string, string> = {};
  for (const board of pack?.boards ?? []) next[board.key] = board.name;
  form.value.boardNames = next;
}

watch(
  () => form.value.packCode,
  (code) => applyPackBoardNames(code, true)
);

watch(packs, () => applyPackBoardNames(form.value.packCode, false));

async function loadOptions() {
  loadingOptions.value = true;
  try {
    const [folderRows, packRows] = await Promise.all([
      ocListWorkspaceFolders().catch(() => [] as OpWorkspaceFolder[]),
      ocListWorkspacePacks().catch(() => [] as OcWorkspacePackSummary[]),
    ]);
    folders.value = folderRows;
    packs.value = packRows;
  } finally {
    loadingOptions.value = false;
  }
}

watch(
  () => props.modelValue,
  (open) => {
    if (!open) return;
    resetForm();
    void loadOptions();
  }
);

function close() {
  const id = createdId.value;
  if (id && finishedId.value !== id) {
    finishedId.value = id;
    emit('created', id);
  }
  createdId.value = null;
  if (props.modelValue) emit('update:modelValue', false);
}

function next() {
  if (!canNext.value || step.value >= 2) return;
  errorLocal.value = null;
  step.value += 1;
}

function back() {
  if (saving.value || createdId.value || step.value === 0) return;
  errorLocal.value = null;
  step.value -= 1;
}

async function finish() {
  if (!canFinish.value || createdId.value) return;
  const name = form.value.name.trim();
  const prefix = form.value.workItemKeyPrefix.trim();
  if (!name || !prefix) return;

  saving.value = true;
  errorLocal.value = null;
  try {
    const payload: Record<string, unknown> = {
      name,
      workspaceType: form.value.workspaceType || null,
      workItemKeyPrefix: prefix,
      workItemKeyFormat: '{prefix}-{seq:D4}',
      workItemSequenceStart: 1,
      viewGroups: [...form.value.viewGroups],
      editGroups: [...form.value.editGroups],
    };
    if (form.value.folderId) payload.folderId = form.value.folderId;

    const id = await ocCreateWorkspace(payload);
    if (!id) {
      errorLocal.value = t('operationCore.workspaceDefinitions.create.saveError');
      return;
    }
    createdId.value = id;

    try {
      await resolveOcWorkspaceLibrary(id);
    } catch {
      // Missing library root does not undo the workspace.
    }

    if (form.value.packCode) {
      try {
        await ocInstallWorkspacePack(id, form.value.packCode, { ...form.value.boardNames });
      } catch (e: unknown) {
        errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.create.packError');
        return;
      }
    }

    finishedId.value = id;
    emit('created', id);
    createdId.value = null;
    emit('update:modelValue', false);
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.create.saveError');
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="720"
    :persistent="saving || !!savedId"
    @update:model-value="(open) => { if (!open) close(); }"
  >
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center ga-2">
        <v-icon icon="mdi-folder-plus-outline" />
        {{ t('operationCore.workspaceDefinitions.create.title') }}
      </v-card-title>
      <v-divider />
      <v-card-text>
        <div class="d-flex flex-wrap ga-2 mb-4">
          <v-chip
            v-for="(label, index) in stepLabels"
            :key="label"
            :color="step === index ? 'primary' : undefined"
            :variant="step === index ? 'flat' : 'tonal'"
            size="small"
          >
            {{ index + 1 }}. {{ label }}
          </v-chip>
        </div>

        <v-alert
          v-if="errorLocal"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="errorLocal = null"
        >
          {{ errorLocal }}
        </v-alert>

        <div v-if="step === 0">
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('operationCore.workspaceDefinitions.create.identityHint') }}
          </p>
          <v-text-field
            v-model="form.name"
            :label="t('operationCore.workspaceDefinitions.general.fieldName')"
            density="comfortable"
            autofocus
            :disabled="!!savedId"
          />
          <v-select
            v-model="form.workspaceType"
            class="mt-3"
            :items="workspaceTypeItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.general.fieldWorkspaceType')"
            density="comfortable"
            :disabled="!!savedId"
          />
          <v-text-field
            v-model="form.workItemKeyPrefix"
            class="mt-3"
            :label="t('operationCore.workspaceDefinitions.general.fieldKeyPrefix')"
            :hint="t('operationCore.workspaceDefinitions.create.prefixHint')"
            persistent-hint
            density="comfortable"
            :disabled="!!savedId"
          />
        </div>

        <div v-else-if="step === 1">
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('operationCore.workspaceDefinitions.create.placeHint') }}
          </p>
          <v-select
            v-model="form.folderId"
            :items="folderItems"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.create.fieldFolder')"
            :hint="t('operationCore.workspaceDefinitions.create.folderHint')"
            persistent-hint
            density="comfortable"
            :loading="loadingOptions"
            :disabled="!!savedId"
          />
          <MngDirectoryPickerField
            v-model="form.viewGroups"
            class="mt-3"
            entity="group"
            multiple
            :label="t('operationCore.workspaceDefinitions.general.fieldViewGroups')"
            density="comfortable"
            variant="outlined"
            :disabled="!!savedId"
          />
          <p class="text-caption text-medium-emphasis mt-1">
            {{ t('operationCore.workspaceDefinitions.create.viewGroupsHint') }}
          </p>
          <MngDirectoryPickerField
            v-model="form.editGroups"
            class="mt-3"
            entity="group"
            multiple
            :label="t('operationCore.workspaceDefinitions.general.fieldEditGroups')"
            density="comfortable"
            variant="outlined"
            :disabled="!!savedId"
          />
          <p class="text-caption text-medium-emphasis mt-1">
            {{ t('operationCore.workspaceDefinitions.create.editGroupsHint') }}
          </p>
        </div>

        <div v-else>
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('operationCore.workspaceDefinitions.create.packHint') }}
          </p>
          <v-radio-group v-model="form.packCode" :disabled="!!savedId || saving" hide-details>
            <v-radio :label="t('operationCore.workspaceDefinitions.create.packNone')" value="" />
            <v-radio
              v-for="pack in packs"
              :key="pack.code"
              :value="pack.code"
            >
              <template #label>
                <span>
                  <span class="font-weight-medium">{{ pack.name }}</span>
                  <span class="text-medium-emphasis"> · {{ pack.version }}</span>
                  <span v-if="pack.description" class="d-block text-caption text-medium-emphasis">{{ pack.description }}</span>
                </span>
              </template>
            </v-radio>
          </v-radio-group>
          <p v-if="!loadingOptions && packs.length === 0" class="text-caption text-medium-emphasis mt-2">
            {{ t('operationCore.workspaceDefinitions.create.packEmpty') }}
          </p>
          <v-alert v-if="suggestionText" type="info" variant="tonal" density="compact" class="mt-4">
            {{ suggestionText }}
          </v-alert>
          <div v-if="selectedPack && selectedPack.boards.length" class="mt-4">
            <v-text-field
              v-for="board in selectedPack.boards"
              :key="board.key"
              v-model="form.boardNames[board.key]"
              :label="boardNameLabel(board.name)"
              :hint="t('operationCore.workspaceDefinitions.create.boardNameHint')"
              persistent-hint
              maxlength="80"
              counter="80"
              density="comfortable"
              variant="outlined"
              class="mb-2"
              :disabled="!!savedId || saving"
            />
          </div>
        </div>
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-4 py-3">
        <v-btn variant="text" class="text-none" :disabled="saving" @click="close">
          {{ savedId ? t('operationCore.workspaceDefinitions.create.closeSaved') : t('operationCore.definitions.cancel') }}
        </v-btn>
        <v-spacer />
        <v-btn
          v-if="step > 0 && !savedId"
          variant="text"
          class="text-none"
          :disabled="saving"
          @click="back"
        >
          {{ t('operationCore.workspaceDefinitions.create.back') }}
        </v-btn>
        <v-btn
          v-if="step < 2"
          color="primary"
          rounded="lg"
          class="text-none"
          :disabled="!canNext"
          @click="next"
        >
          {{ t('operationCore.workspaceDefinitions.create.next') }}
        </v-btn>
        <v-btn
          v-else-if="!savedId"
          color="primary"
          rounded="lg"
          class="text-none"
          :loading="saving"
          :disabled="!canFinish"
          @click="finish"
        >
          {{ t('operationCore.workspaceDefinitions.create.submit') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
