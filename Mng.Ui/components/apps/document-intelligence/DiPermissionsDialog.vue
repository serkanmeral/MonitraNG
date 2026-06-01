<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { fetchFromMngKeeper } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  diGetPermissions,
  diSetPermissions,
  diBreakInheritance,
  diRestoreInheritance,
  diExtractMessage,
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
  /** Yetki durumu değiştiğinde (kaydet/kır/geri yükle) parent ağacı/içeriği tazelesin. */
  changed: [];
  notify: [text: string, color: 'success' | 'error' | 'info'];
}>();

const { t } = useAppI18n();
const authStore = useAuthStore();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

// Aksiyon meta (etiketler i18n ile template'te çözülür).
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

const groups = ref<Array<{ id: string; name: string }>>([]);
const loadingGroups = ref(false);

// groupName -> { action -> boolean } (yalnızca miras kırıkken düzenlenebilir)
const matrix = ref<Record<string, Record<string, boolean>>>({});

const inheritanceBroken = computed(() => perms.value?.inheritanceBroken === true);

// Manager kullanıcılar "admins" grubunu göremez (mevcut PermissionEditor deseni).
const filteredGroups = computed(() => {
  if (authStore.isAdmin) return groups.value;
  return groups.value.filter((g) => g.name.toLowerCase() !== 'admins');
});

// Matriste gösterilecek satırlar: MngKeeper grupları ∪ mevcut izin kayıtlarındaki gruplar.
// Böylece keeper'da olmayan/silinen ya da filtrelenen (ör. admin olmayanlar için "admins")
// ama kaydı bulunan gruplar da görünür — "tanımladığım izinleri göremiyorum" sorununu önler.
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
  // Tüm görünür gruplar için boş satır (keeper ∪ kayıtlı gruplar)
  for (const g of displayGroups.value) next[g.name] = emptyActionRow();
  // Kayıtlı grupları işaretle
  for (const gp of source) {
    if (!gp.groupName) continue;
    if (!next[gp.groupName]) next[gp.groupName] = emptyActionRow();
    for (const a of gp.permissions) {
      if (a in next[gp.groupName]) next[gp.groupName][a] = true;
    }
  }
  matrix.value = next;
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
  } catch (e) {
    emit('notify', diExtractMessage(e, t('documentIntelligence.permissions.errors.load')), 'error');
    perms.value = null;
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

// Gruplar/izinler sonradan yüklenirse matris satırlarını yeniden kur.
watch(displayGroups, () => {
  if (perms.value) buildMatrix(perms.value.groups);
});

function toggleAction(groupName: string, action: string, value: boolean) {
  const row = { ...(matrix.value[groupName] ?? emptyActionRow()) };
  row[action] = value;
  // "view" baz erişim: başka aksiyon işaretlendiğinde view de açılsın.
  if (value && action !== 'view') row.view = true;
  // Yeni nesne atayarak reaktiviteyi garanti et.
  matrix.value = { ...matrix.value, [groupName]: row };
}

function setAllForGroup(groupName: string, value: boolean) {
  const row = emptyActionRow();
  if (value) {
    for (const a of DI_PERMISSION_ACTIONS) row[a] = true;
  }
  matrix.value = { ...matrix.value, [groupName]: row };
}

function groupHasAny(groupName: string): boolean {
  const row = matrix.value[groupName];
  return row ? DI_PERMISSION_ACTIONS.some((a) => row[a]) : false;
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
    emit('notify', t('documentIntelligence.permissions.saved'), 'success');
    emit('changed');
  } catch (e) {
    emit('notify', diExtractMessage(e, t('documentIntelligence.permissions.errors.save')), 'error');
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
    emit('notify', t('documentIntelligence.permissions.inheritanceBrokenMsg'), 'success');
    emit('changed');
  } catch (e) {
    emit('notify', diExtractMessage(e, t('documentIntelligence.permissions.errors.break')), 'error');
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
    emit('notify', t('documentIntelligence.permissions.inheritanceRestoredMsg'), 'success');
    emit('changed');
  } catch (e) {
    emit('notify', diExtractMessage(e, t('documentIntelligence.permissions.errors.restore')), 'error');
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="860" scrollable>
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
          <!-- Miras durumu -->
          <v-alert
            :type="inheritanceBroken ? 'warning' : 'info'"
            variant="tonal"
            density="comfortable"
            class="mb-4"
          >
            <div class="d-flex align-center flex-wrap ga-2">
              <span class="text-body-2">
                {{ inheritanceBroken
                  ? t('documentIntelligence.permissions.brokenInfo')
                  : t('documentIntelligence.permissions.inheritedInfo') }}
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

          <div v-else class="di-perm-table-wrap">
            <v-table density="compact" class="di-perm-table">
              <thead>
                <tr>
                  <th class="text-left" style="min-width: 160px">{{ t('documentIntelligence.permissions.group') }}</th>
                  <th v-for="a in actionMeta" :key="a.key" class="text-center" style="min-width: 78px">
                    {{ t('documentIntelligence.permissions.actions.' + a.key) }}
                  </th>
                  <th class="text-center" style="min-width: 96px">{{ t('documentIntelligence.permissions.all') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="g in displayGroups" :key="g.name">
                  <td>
                    <v-chip size="small" variant="flat" color="primary">{{ g.name }}</v-chip>
                  </td>
                  <td v-for="a in actionMeta" :key="a.key" class="text-center">
                    <v-checkbox
                      :model-value="matrix[g.name]?.[a.key] || false"
                      :disabled="!inheritanceBroken"
                      :color="a.color"
                      density="compact"
                      hide-details
                      @update:model-value="(val) => toggleAction(g.name, a.key, val === true)"
                    />
                  </td>
                  <td class="text-center">
                    <v-btn-toggle density="compact" variant="text" divided>
                      <v-btn size="x-small" :disabled="!inheritanceBroken" @click="setAllForGroup(g.name, true)">
                        {{ t('documentIntelligence.permissions.grantAll') }}
                      </v-btn>
                      <v-btn size="x-small" :disabled="!inheritanceBroken || !groupHasAny(g.name)" @click="setAllForGroup(g.name, false)">
                        {{ t('documentIntelligence.permissions.clear') }}
                      </v-btn>
                    </v-btn-toggle>
                  </td>
                </tr>
              </tbody>
            </v-table>
          </div>

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
  max-height: 50vh;
  overflow: auto;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  border-radius: 8px;
}
.di-perm-table thead th {
  position: sticky;
  top: 0;
  background-color: rgb(var(--v-theme-surface));
  z-index: 1;
  font-weight: 600;
  white-space: nowrap;
}
</style>
