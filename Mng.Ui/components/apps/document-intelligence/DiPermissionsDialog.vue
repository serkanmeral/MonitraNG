<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { fetchFromMngKeeper } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diGetById,
  diGetPermissions,
  diSetPermissions,
  diBreakInheritance,
  diRestoreInheritance,
} from '@/services/documentIntelligenceService';
import {
  DI_PERMISSION_ACTIONS,
  type DiFolderPermissions,
  type DiGroupPermission,
} from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  folderId: string | null;
  folderName: string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  changed: [];
  notify: [text: string, color: 'success' | 'error' | 'info'];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const authStore = useAuthStore();

const EDITOR_PRESET = ['view', 'create', 'edit', 'upload', 'download'] as const;

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const actionMeta = [
  { key: 'view', color: 'info' },
  { key: 'create', color: 'success' },
  { key: 'edit', color: 'warning' },
  { key: 'delete', color: 'error' },
  { key: 'upload', color: 'primary' },
  { key: 'download', color: 'secondary' },
  { key: 'move', color: 'primary' },
  { key: 'share', color: 'info' },
] as const;

const perms = ref<DiFolderPermissions | null>(null);
const loading = ref(false);
const saving = ref(false);
const busy = ref(false);
const anchorFolderName = ref<string | null>(null);

const groups = ref<Array<{ id: string; name: string }>>([]);
const loadingGroups = ref(false);
const matrix = ref<Record<string, Record<string, boolean>>>({});

const inheritanceBroken = computed(() => perms.value?.inheritanceBroken === true);

const effectiveAnchorId = computed(() => perms.value?.effectiveAnchorId?.trim() || null);

/** Bu klasör miras alıyor ama üstte kırık anchor var → asıl kısıtlama oradan gelir. */
const restrictedByParentAnchor = computed(
  () => !inheritanceBroken.value && effectiveAnchorId.value != null
);

const effective = computed(() => perms.value?.effective ?? null);

const effectiveActionKeys = [
  { key: 'view', prop: 'canView' as const, color: 'info' },
  { key: 'create', prop: 'canCreate' as const, color: 'success' },
  { key: 'edit', prop: 'canEdit' as const, color: 'warning' },
  { key: 'delete', prop: 'canDelete' as const, color: 'error' },
  { key: 'upload', prop: 'canUpload' as const, color: 'primary' },
  { key: 'download', prop: 'canDownload' as const, color: 'secondary' },
  { key: 'move', prop: 'canMove' as const, color: 'primary' },
  { key: 'share', prop: 'canShare' as const, color: 'info' },
];

const filteredGroups = computed(() => {
  if (authStore.isAdmin) return groups.value;
  return groups.value.filter((g) => g.name.toLowerCase() !== 'admins');
});

const displayGroups = computed(() => {
  const map = new Map<string, { id: string; name: string; fromKeeper: boolean }>();
  for (const g of filteredGroups.value) {
    map.set(g.name, { id: g.id, name: g.name, fromKeeper: true });
  }
  for (const gp of perms.value?.groups ?? []) {
    if (!gp.groupName) continue;
    if (!map.has(gp.groupName)) {
      map.set(gp.groupName, { id: gp.groupId || '', name: gp.groupName, fromKeeper: false });
    }
  }
  return Array.from(map.values()).sort((a, b) => a.name.localeCompare(b.name));
});

function emptyActionRow(): Record<string, boolean> {
  const row: Record<string, boolean> = {};
  for (const a of DI_PERMISSION_ACTIONS) row[a] = false;
  return row;
}

function buildMatrix(source: DiGroupPermission[]) {
  const next: Record<string, Record<string, boolean>> = {};
  for (const g of displayGroups.value) next[g.name] = emptyActionRow();
  for (const gp of source) {
    if (!gp.groupName) continue;
    if (!next[gp.groupName]) next[gp.groupName] = emptyActionRow();
    for (const a of gp.permissions) {
      if (a in next[gp.groupName]) next[gp.groupName][a] = true;
    }
  }
  matrix.value = next;
}

async function loadAnchorName(anchorId: string | null) {
  if (!anchorId) {
    anchorFolderName.value = null;
    return;
  }
  try {
    const folder = await diGetById(anchorId);
    anchorFolderName.value = folder.name || anchorId;
  } catch {
    anchorFolderName.value = anchorId;
  }
}

async function loadGroups() {
  loadingGroups.value = true;
  try {
    const response: any = await fetchFromMngKeeper('/group?page=1&pageSize=1000', 'GET');
    let loaded: any[] = [];
    if (Array.isArray(response)) loaded = response;
    else if (response?.groups && Array.isArray(response.groups)) loaded = response.groups;
    else if (response?.data && Array.isArray(response.data)) loaded = response.data;
    else if (response?.data?.groups && Array.isArray(response.data.groups)) loaded = response.data.groups;
    else if (response?.items && Array.isArray(response.items)) loaded = response.items;

    groups.value = loaded
      .filter((g: any) => {
        const active = g.isActive !== undefined ? g.isActive : (g.IsActive !== undefined ? g.IsActive : true);
        return active !== false;
      })
      .map((g: any) => ({
        id: String(g.groupId ?? g.id ?? g.Id ?? g.dataId ?? ''),
        name: String(g.name ?? g.Name ?? ''),
      }))
      .filter((g) => g.name.length > 0);
  } catch {
    groups.value = [];
  } finally {
    loadingGroups.value = false;
  }
}

async function loadPermissions() {
  if (!props.folderId) return;
  loading.value = true;
  try {
    perms.value = await diGetPermissions(props.folderId);
    buildMatrix(perms.value.groups);
    await loadAnchorName(perms.value.effectiveAnchorId);
  } catch (e) {
    emit('notify', panelError(e, 'documentIntelligence.permissions.errors.load'), 'error');
    perms.value = null;
    anchorFolderName.value = null;
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.modelValue, props.folderId] as const,
  async ([isOpen]) => {
    if (isOpen && props.folderId) {
      if (!groups.value.length) await loadGroups();
      await loadPermissions();
    }
  },
  { immediate: true },
);

watch(displayGroups, () => {
  if (perms.value) buildMatrix(perms.value.groups);
});

function toggleAction(groupName: string, action: string, value: boolean) {
  const row = { ...(matrix.value[groupName] ?? emptyActionRow()) };
  row[action] = value;
  if (value && action !== 'view') row.view = true;
  matrix.value = { ...matrix.value, [groupName]: row };
}

function setAllForGroup(groupName: string, value: boolean) {
  const row = emptyActionRow();
  if (value) {
    for (const a of DI_PERMISSION_ACTIONS) row[a] = true;
  }
  matrix.value = { ...matrix.value, [groupName]: row };
}

function applyPresetToAll(actions: readonly string[]) {
  const next = { ...matrix.value };
  for (const g of displayGroups.value) {
    const row = emptyActionRow();
    for (const a of actions) row[a] = true;
    next[g.name] = row;
  }
  matrix.value = next;
}

function groupHasAny(groupName: string): boolean {
  const row = matrix.value[groupName];
  return row ? DI_PERMISSION_ACTIONS.some((a) => row[a]) : false;
}

function clearAllGroups() {
  const next = { ...matrix.value };
  for (const g of displayGroups.value) next[g.name] = emptyActionRow();
  matrix.value = next;
}

async function save() {
  if (!props.folderId) return;
  saving.value = true;
  try {
    const payloadGroups: DiGroupPermission[] = [];
    for (const g of displayGroups.value) {
      const row = matrix.value[g.name];
      if (!row) continue;
      const actions = DI_PERMISSION_ACTIONS.filter((a) => row[a]);
      if (actions.length === 0) continue;
      payloadGroups.push({ groupId: g.id || null, groupName: g.name, permissions: actions });
    }
    perms.value = await diSetPermissions(props.folderId, { groups: payloadGroups });
    buildMatrix(perms.value.groups);
    await loadAnchorName(perms.value.effectiveAnchorId);
    emit('notify', t('documentIntelligence.permissions.saved'), 'success');
    emit('changed');
  } catch (e) {
    emit('notify', panelError(e, 'documentIntelligence.permissions.errors.save'), 'error');
  } finally {
    saving.value = false;
  }
}

async function breakInheritance() {
  if (!props.folderId) return;
  busy.value = true;
  try {
    perms.value = await diBreakInheritance(props.folderId);
    buildMatrix(perms.value.groups);
    await loadAnchorName(perms.value.effectiveAnchorId);
    emit('notify', t('documentIntelligence.permissions.inheritanceBrokenMsg'), 'success');
    emit('changed');
  } catch (e) {
    emit('notify', panelError(e, 'documentIntelligence.permissions.errors.break'), 'error');
  } finally {
    busy.value = false;
  }
}

async function restoreInheritance() {
  if (!props.folderId) return;
  busy.value = true;
  try {
    perms.value = await diRestoreInheritance(props.folderId);
    buildMatrix(perms.value.groups);
    await loadAnchorName(perms.value.effectiveAnchorId);
    emit('notify', t('documentIntelligence.permissions.inheritanceRestoredMsg'), 'success');
    emit('changed');
  } catch (e) {
    emit('notify', panelError(e, 'documentIntelligence.permissions.errors.restore'), 'error');
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="920" scrollable>
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center text-subtitle-1 font-weight-bold">
        <v-icon size="20" class="mr-2">mdi-shield-account-outline</v-icon>
        {{ t('documentIntelligence.permissions.title') }}
        <span class="text-medium-emphasis font-weight-regular ml-2 text-truncate">— {{ folderName }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="open = false" />
      </v-card-title>
      <v-divider />

      <v-card-text>
        <div v-if="loading" class="d-flex justify-center pa-8">
          <v-progress-circular indeterminate color="primary" size="32" />
        </div>

        <template v-else>
          <!-- Geçerli kullanıcının etkin yetkisi -->
          <v-sheet v-if="effective" variant="outlined" rounded="lg" class="pa-3 mb-4">
            <div class="text-caption text-medium-emphasis mb-2">
              {{ t('documentIntelligence.permissions.yourEffective') }}
            </div>
            <div class="d-flex flex-wrap ga-2">
              <v-chip
                v-for="a in effectiveActionKeys"
                :key="a.key"
                size="small"
                :color="effective[a.prop] ? a.color : undefined"
                :variant="effective[a.prop] ? 'flat' : 'outlined'"
              >
                {{ t('documentIntelligence.permissions.actions.' + a.key) }}
              </v-chip>
            </div>
            <p v-if="!effective.canEdit" class="text-caption text-warning mt-2 mb-0">
              {{ t('documentIntelligence.permissions.noEditHint') }}
            </p>
            <p v-if="!effective.canEdit && authStore.userGroups.length" class="text-caption text-medium-emphasis mb-0">
              {{ t('documentIntelligence.permissions.yourJwtGroups', { groups: authStore.userGroups.join(', ') }) }}
            </p>
          </v-sheet>

          <v-alert
            v-if="restrictedByParentAnchor"
            type="warning"
            variant="tonal"
            density="comfortable"
            class="mb-4"
          >
            {{
              t('documentIntelligence.permissions.restrictedByParent', {
                folder: anchorFolderName || effectiveAnchorId,
              })
            }}
          </v-alert>

          <v-alert
            :type="inheritanceBroken ? 'warning' : 'info'"
            variant="tonal"
            density="comfortable"
            class="mb-4"
          >
            <div class="d-flex align-center flex-wrap ga-2">
              <span class="text-body-2">
                {{
                  inheritanceBroken
                    ? t('documentIntelligence.permissions.brokenInfo')
                    : restrictedByParentAnchor
                      ? t('documentIntelligence.permissions.inheritedFromAnchor')
                      : t('documentIntelligence.permissions.inheritedInfo')
                }}
              </span>
              <v-spacer />
              <v-btn
                v-if="!inheritanceBroken"
                size="small"
                color="warning"
                variant="flat"
                class="text-none"
                prepend-icon="mdi-link-variant-off"
                :loading="busy"
                @click="breakInheritance"
              >
                {{ t('documentIntelligence.permissions.breakInheritance') }}
              </v-btn>
              <v-btn
                v-else
                size="small"
                color="primary"
                variant="tonal"
                class="text-none"
                prepend-icon="mdi-link-variant"
                :loading="busy"
                @click="restoreInheritance"
              >
                {{ t('documentIntelligence.permissions.restoreInheritance') }}
              </v-btn>
            </div>
          </v-alert>

          <div v-if="loadingGroups" class="d-flex justify-center pa-4">
            <v-progress-circular indeterminate color="primary" size="28" />
          </div>

          <div v-else-if="!displayGroups.length" class="text-medium-emphasis text-body-2 pa-4 text-center">
            {{ t('documentIntelligence.permissions.noGroups') }}
          </div>

          <template v-else>
            <div v-if="inheritanceBroken" class="d-flex flex-wrap align-center ga-2 mb-3">
              <span class="text-caption text-medium-emphasis mr-1">
                {{ t('documentIntelligence.permissions.bulkActions') }}
              </span>
              <v-btn
                size="small"
                variant="tonal"
                color="primary"
                class="text-none"
                prepend-icon="mdi-pencil-outline"
                @click="applyPresetToAll(EDITOR_PRESET)"
              >
                {{ t('documentIntelligence.permissions.grantEditorAll') }}
              </v-btn>
              <v-btn
                size="small"
                variant="tonal"
                class="text-none"
                prepend-icon="mdi-check-all"
                @click="applyPresetToAll(DI_PERMISSION_ACTIONS)"
              >
                {{ t('documentIntelligence.permissions.grantFullAll') }}
              </v-btn>
              <v-btn
                size="small"
                variant="text"
                class="text-none"
                prepend-icon="mdi-close-circle-outline"
                @click="clearAllGroups"
              >
                {{ t('documentIntelligence.permissions.clearAll') }}
              </v-btn>
            </div>

            <div class="di-perm-table-wrap">
              <v-table density="compact" class="di-perm-table">
                <thead>
                  <tr>
                    <th class="di-perm-sticky-left text-left" style="min-width: 220px">
                      {{ t('documentIntelligence.permissions.group') }}
                    </th>
                    <th
                      v-for="a in actionMeta"
                      :key="a.key"
                      class="text-center di-perm-action-col"
                    >
                      {{ t('documentIntelligence.permissions.actions.' + a.key) }}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="g in displayGroups" :key="g.name">
                    <td class="di-perm-sticky-left">
                      <div class="d-flex align-center flex-wrap ga-1 py-1">
                        <v-chip size="small" variant="flat" color="primary" class="flex-shrink-0">
                          {{ g.name }}
                        </v-chip>
                        <v-btn
                          size="x-small"
                          variant="tonal"
                          color="primary"
                          class="text-none px-2"
                          :disabled="!inheritanceBroken"
                          :title="t('documentIntelligence.permissions.grantAll')"
                          @click="setAllForGroup(g.name, true)"
                        >
                          <v-icon size="14" start>mdi-check-all</v-icon>
                          {{ t('documentIntelligence.permissions.grantAll') }}
                        </v-btn>
                        <v-btn
                          size="x-small"
                          variant="text"
                          class="text-none px-1"
                          :disabled="!inheritanceBroken || !groupHasAny(g.name)"
                          :title="t('documentIntelligence.permissions.clear')"
                          @click="setAllForGroup(g.name, false)"
                        >
                          {{ t('documentIntelligence.permissions.clear') }}
                        </v-btn>
                      </div>
                    </td>
                    <td v-for="a in actionMeta" :key="a.key" class="text-center di-perm-action-col">
                      <v-checkbox
                        :model-value="matrix[g.name]?.[a.key] || false"
                        :disabled="!inheritanceBroken"
                        :color="a.color"
                        density="compact"
                        hide-details
                        @update:model-value="(val) => toggleAction(g.name, a.key, val === true)"
                      />
                    </td>
                  </tr>
                </tbody>
              </v-table>
            </div>
          </template>

          <p v-if="!inheritanceBroken" class="text-caption text-medium-emphasis mt-3 mb-0">
            {{ t('documentIntelligence.permissions.editHint') }}
          </p>
        </template>
      </v-card-text>

      <v-divider />
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="open = false">{{ t('documentIntelligence.cancel') }}</v-btn>
        <v-btn
          v-if="inheritanceBroken"
          color="primary"
          variant="flat"
          class="text-none"
          prepend-icon="mdi-content-save"
          :loading="saving"
          @click="save"
        >
          {{ t('documentIntelligence.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.di-perm-table-wrap {
  max-height: 52vh;
  overflow: auto;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  border-radius: 8px;
}

.di-perm-table {
  min-width: 720px;
}

.di-perm-table thead th {
  position: sticky;
  top: 0;
  background-color: rgb(var(--v-theme-surface));
  z-index: 2;
  font-weight: 600;
  white-space: nowrap;
}

.di-perm-sticky-left {
  position: sticky;
  left: 0;
  z-index: 1;
  background-color: rgb(var(--v-theme-surface));
  box-shadow: 2px 0 6px rgba(0, 0, 0, 0.06);
}

.di-perm-table thead th.di-perm-sticky-left {
  z-index: 3;
}

.di-perm-action-col {
  min-width: 72px;
  white-space: nowrap;
}
</style>
