<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { ocListWorkspaces } from '@/services/operationCoreService';
import {
  pmCreateMeetingPerson,
  pmDeleteMeetingPerson,
  pmEnsureMeetingWorkspace,
  pmGetMeetingWorkspace,
  pmListProjectMeetingPeople,
  pmSetMeetingWorkspace,
  pmUpdateMeetingPerson,
} from '@/services/projectManagementService';
import type { PmMeetingPerson, PmMeetingWorkspace } from '@/types/apps/projectManagement';
import type { OpWorkspace } from '@/types/apps/operationCore';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify();

const settingsTab = ref('external');
const people = ref<PmMeetingPerson[]>([]);
const loading = ref(false);
const saving = ref(false);
const meeting = ref<PmMeetingWorkspace | null>(null);
const workspaces = ref<OpWorkspace[]>([]);
const selectedWorkspaceId = ref<string | null>(null);
const meetingLoading = ref(false);
const meetingSaving = ref(false);
const dialog = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmMeetingPerson | null>(null);
const form = ref({ name: '', organization: '', email: '', note: '' });
const search = ref<string | null>('');
const organizationFilter = ref<string | null>(null);
const page = ref(1);

function compareText(a?: string | null, b?: string | null) {
  return (a || '').localeCompare(b || '', 'tr', { sensitivity: 'base' });
}

const headers = computed(() => [
  { title: t('projectManagement.settings.colName'), key: 'name', sort: compareText },
  { title: t('projectManagement.settings.colOrganization'), key: 'organization', sort: compareText },
  { title: t('projectManagement.settings.colEmail'), key: 'email', sort: compareText },
  { title: t('projectManagement.settings.note'), key: 'note', sort: compareText },
  { title: '', key: 'actions', sortable: false, align: 'end' as const, width: 96 },
]);

const organizationOptions = computed(() => {
  const names = new Set(
    people.value.map((person) => (person.organization || '').trim()).filter((name) => name.length > 0),
  );
  return [...names].sort((a, b) => a.localeCompare(b, 'tr', { sensitivity: 'base' }));
});

const filteredPeople = computed(() => {
  const query = (search.value || '').trim().toLocaleLowerCase('tr');
  const organization = organizationFilter.value;
  return people.value.filter((person) => {
    if (organization && (person.organization || '') !== organization) return false;
    if (!query) return true;
    const haystack = [person.name, person.organization, person.email, person.note]
      .filter((part) => part)
      .join(' ')
      .toLocaleLowerCase('tr');
    return haystack.includes(query);
  });
});

watch([search, organizationFilter], () => {
  page.value = 1;
});

async function loadPeople() {
  if (!props.projectId) return;
  loading.value = true;
  try {
    people.value = await pmListProjectMeetingPeople(props.projectId);
  } catch (error) {
    people.value = [];
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    loading.value = false;
  }
}

async function loadMeeting() {
  if (!props.projectId) return;
  meetingLoading.value = true;
  try {
    const [current, listed] = await Promise.all([
      pmGetMeetingWorkspace(props.projectId),
      ocListWorkspaces(),
    ]);
    meeting.value = current;
    workspaces.value = listed;
    selectedWorkspaceId.value = current.workspaceId || null;
  } catch (error) {
    meeting.value = null;
    workspaces.value = [];
    selectedWorkspaceId.value = null;
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    meetingLoading.value = false;
  }
}

async function load() {
  await Promise.all([loadPeople(), loadMeeting()]);
}

async function saveMeetingSelection() {
  if (!props.projectId) return;
  meetingSaving.value = true;
  try {
    meeting.value = await pmSetMeetingWorkspace(props.projectId, selectedWorkspaceId.value);
    selectedWorkspaceId.value = meeting.value.workspaceId || null;
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    meetingSaving.value = false;
  }
}

async function generateMeetingWorkspace() {
  if (!props.projectId) return;
  meetingSaving.value = true;
  try {
    meeting.value = await pmEnsureMeetingWorkspace(props.projectId);
    selectedWorkspaceId.value = meeting.value.workspaceId || null;
    workspaces.value = await ocListWorkspaces();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    meetingSaving.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  form.value = { name: '', organization: '', email: '', note: '' };
  dialog.value = true;
}

function openEdit(person: PmMeetingPerson) {
  editingId.value = person.id;
  form.value = {
    name: person.name,
    organization: person.organization || '',
    email: person.email || '',
    note: person.note || '',
  };
  dialog.value = true;
}

async function save() {
  const name = form.value.name.trim();
  if (name.length < 2) return;
  saving.value = true;
  try {
    const body = {
      name,
      organization: form.value.organization.trim() || null,
      email: form.value.email.trim() || null,
      note: form.value.note.trim() || null,
    };
    if (editingId.value) await pmUpdateMeetingPerson(editingId.value, body);
    else await pmCreateMeetingPerson(props.projectId, body);
    dialog.value = false;
    await loadPeople();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function remove() {
  if (!deleteTarget.value) return;
  saving.value = true;
  try {
    await pmDeleteMeetingPerson(deleteTarget.value.id);
    deleteTarget.value = null;
    await loadPeople();
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

watch(() => props.projectId, () => {
  void load();
});

onMounted(() => {
  void load();
});
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between flex-wrap ga-3 mb-4">
      <div>
        <div class="text-h6">{{ t('projectManagement.settings.title') }}</div>
        <div class="text-body-2 text-medium-emphasis mt-1">{{ t('projectManagement.settings.hint') }}</div>
      </div>
    </div>

    <v-tabs v-model="settingsTab" color="primary" class="mb-4">
      <v-tab value="external">{{ t('projectManagement.settings.externalPeople') }}</v-tab>
      <v-tab value="meetings">{{ t('projectManagement.settings.meetings') }}</v-tab>
    </v-tabs>

    <v-window v-model="settingsTab" class="pm-settings-window">
      <v-window-item value="external">
        <div class="d-flex align-center justify-space-between flex-wrap ga-3 mb-3">
          <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.settings.externalHint') }}</div>
          <v-btn color="primary" @click="openCreate">
            <PlusIcon size="18" class="mr-1" />
            {{ t('projectManagement.settings.add') }}
          </v-btn>
        </div>

        <div class="d-flex flex-wrap ga-3 mb-3 align-center">
          <v-text-field
            v-model="search"
            :label="t('projectManagement.settings.search')"
            density="compact"
            hide-details
            clearable
            prepend-inner-icon="mdi-magnify"
            style="max-width: 320px; min-width: 220px;"
          />
          <v-select
            v-model="organizationFilter"
            :items="organizationOptions"
            :label="t('projectManagement.settings.filterOrganization')"
            :placeholder="t('projectManagement.settings.filterAll')"
            density="compact"
            hide-details
            clearable
            style="max-width: 280px; min-width: 200px;"
          />
          <div class="text-body-2 text-medium-emphasis">
            {{ t('projectManagement.settings.count', { n: filteredPeople.length }) }}
          </div>
        </div>

        <v-data-table
          v-model:page="page"
          :headers="headers"
          :items="filteredPeople"
          :loading="loading"
          item-value="id"
          density="comfortable"
          class="rounded-lg border"
          :items-per-page="25"
          :items-per-page-options="[10, 25, 50, 100]"
        >
          <template #item.organization="{ item }">
            {{ item.organization || '—' }}
          </template>
          <template #item.email="{ item }">
            {{ item.email || '—' }}
          </template>
          <template #item.note="{ item }">
            {{ item.note || '—' }}
          </template>
          <template #item.actions="{ item }">
            <div class="d-flex justify-end">
              <v-btn icon variant="text" size="small" @click="openEdit(item)">
                <v-icon>mdi-pencil-outline</v-icon>
              </v-btn>
              <v-btn icon variant="text" size="small" color="error" @click="deleteTarget = item">
                <component :is="TrashIcon" size="18" />
              </v-btn>
            </div>
          </template>
          <template #no-data>
            <div class="text-body-2 text-medium-emphasis py-6">
              {{ people.length ? t('projectManagement.settings.noMatch') : t('projectManagement.settings.empty') }}
            </div>
          </template>
        </v-data-table>
      </v-window-item>

      <v-window-item value="meetings">
        <div class="text-body-2 text-medium-emphasis mb-4">{{ t('projectManagement.settings.meetingHint') }}</div>
        <v-progress-linear v-if="meetingLoading" indeterminate color="primary" class="mb-3" />
        <div v-else class="d-flex flex-column ga-3" style="max-width: 640px;">
          <div class="text-body-2">
            {{ t('projectManagement.settings.meetingTemplate') }}: {{ meeting?.templateName || 'Toplantı tutanağı' }}
          </div>
          <div class="text-body-2">
            {{ t('projectManagement.settings.meetingSuggested') }}: {{ meeting?.suggestedName || '—' }}
          </div>
          <v-autocomplete
            v-model="selectedWorkspaceId"
            :items="workspaces"
            item-title="name"
            item-value="__dataId"
            :label="t('projectManagement.settings.meetingWorkspace')"
            :no-data-text="t('projectManagement.settings.meetingEmpty')"
            clearable
            density="comfortable"
          />
          <div v-if="meeting?.workspaceId && !meeting.workspaceName" class="text-caption text-medium-emphasis">
            {{ t('projectManagement.settings.meetingMissing') }}
          </div>
          <div class="d-flex flex-wrap ga-2">
            <v-btn color="primary" :loading="meetingSaving" @click="saveMeetingSelection">
              {{ t('projectManagement.settings.meetingSave') }}
            </v-btn>
            <v-btn variant="tonal" :loading="meetingSaving" @click="generateMeetingWorkspace">
              {{ t('projectManagement.settings.meetingGenerate') }}
            </v-btn>
          </div>
        </div>
      </v-window-item>
    </v-window>

    <v-dialog v-model="dialog" max-width="560">
      <v-card>
        <v-card-title>
          {{ editingId ? t('projectManagement.settings.edit') : t('projectManagement.settings.add') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field
            v-model="form.name"
            :label="t('projectManagement.settings.colName')"
            density="comfortable"
            autofocus
          />
          <v-text-field
            v-model="form.organization"
            :label="t('projectManagement.settings.colOrganization')"
            density="comfortable"
          />
          <v-text-field
            v-model="form.email"
            :label="t('projectManagement.settings.colEmail')"
            density="comfortable"
          />
          <v-textarea
            v-model="form.note"
            :label="t('projectManagement.settings.note')"
            density="comfortable"
            rows="2"
            auto-grow
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="form.name.trim().length < 2" @click="save">
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="deleteTarget !== null" max-width="480" @update:model-value="(open) => { if (!open) deleteTarget = null; }">
      <v-card v-if="deleteTarget">
        <v-card-title>{{ t('projectManagement.settings.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.settings.deleteBody', { name: deleteTarget.name }) }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="saving" @click="remove">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.pm-settings-window,
.pm-settings-window :deep(.v-window__container),
.pm-settings-window :deep(.v-window-item) {
  overflow: visible;
  height: auto;
}
</style>
