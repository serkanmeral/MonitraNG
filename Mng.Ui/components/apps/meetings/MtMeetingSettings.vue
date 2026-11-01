<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  createOrgMeetingPerson,
  deleteOrgMeetingPerson,
  updateOrgMeetingPerson,
  type OrgMeetingPerson,
} from '@/services/meetingsService';
import MtMeetingNoticeSettings from '@/components/apps/meetings/MtMeetingNoticeSettings.vue';
import MtMeetingRooms from '@/components/apps/meetings/MtMeetingRooms.vue';
import MtMeetingTopics from '@/components/apps/meetings/MtMeetingTopics.vue';
import { TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  people: OrgMeetingPerson[];
}>();

const emit = defineEmits<{ changed: []; rooms: []; topics: [] }>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const tab = ref<'people' | 'topics' | 'rooms' | 'notices'>('people');
const search = ref('');
const organizationFilter = ref<string | null>(null);
const page = ref(1);
const dialog = ref(false);
const editingId = ref<string | null>(null);
const saving = ref(false);
const deleteTarget = ref<OrgMeetingPerson | null>(null);
const form = ref({ name: '', organization: '', email: '', note: '' });

function personOf(item: OrgMeetingPerson | { raw?: OrgMeetingPerson }): OrgMeetingPerson {
  if (item && typeof item === 'object' && 'raw' in item && item.raw) return item.raw;
  return item as OrgMeetingPerson;
}

function compareText(a?: string | null, b?: string | null) {
  return (a || '').localeCompare(b || '', 'tr', { sensitivity: 'base' });
}

const headers = computed(() => [
  { title: t('meetings.person.name'), key: 'name', sort: compareText },
  { title: t('meetings.person.organization'), key: 'organization', sort: compareText },
  { title: t('meetings.person.email'), key: 'email', sort: compareText },
  { title: t('meetings.person.note'), key: 'note', sort: compareText },
  { title: '', key: 'actions', sortable: false, align: 'end' as const, width: 96 },
]);

const organizationOptions = computed(() => {
  const names = new Set(props.people.map((person) => person.organization.trim()).filter((name) => name.length > 0));
  return [...names].sort((left, right) => left.localeCompare(right, 'tr', { sensitivity: 'base' }));
});

const filteredPeople = computed(() => {
  const query = search.value.trim().toLocaleLowerCase('tr');
  const organization = organizationFilter.value;
  return props.people.filter((person) => {
    if (organization && person.organization !== organization) return false;
    if (!query) return true;
    return [person.name, person.organization, person.email, person.note]
      .join(' ')
      .toLocaleLowerCase('tr')
      .includes(query);
  });
});

watch([search, organizationFilter], () => {
  page.value = 1;
});

function openCreate() {
  editingId.value = null;
  form.value = { name: '', organization: '', email: '', note: '' };
  dialog.value = true;
}

function openEdit(person: OrgMeetingPerson) {
  editingId.value = person.id;
  form.value = {
    name: person.name,
    organization: person.organization,
    email: person.email,
    note: person.note,
  };
  dialog.value = true;
}

async function save() {
  const name = form.value.name.trim();
  if (!name) {
    panelError(new Error('name'), 'meetings.validation.personName');
    return;
  }
  saving.value = true;
  try {
    const body = {
      name,
      organization: form.value.organization,
      email: form.value.email,
      note: form.value.note,
    };
    if (editingId.value) await updateOrgMeetingPerson(editingId.value, body);
    else await createOrgMeetingPerson(body);
    dialog.value = false;
    emit('changed');
  } catch (error) {
    panelError(error, 'meetings.personSaveFailed');
  } finally {
    saving.value = false;
  }
}

async function remove() {
  if (!deleteTarget.value) return;
  saving.value = true;
  try {
    await deleteOrgMeetingPerson(deleteTarget.value.id);
    deleteTarget.value = null;
    emit('changed');
  } catch (error) {
    const message = error instanceof Error ? error.message : '';
    panelError(error, message === 'PERSON_IN_USE' ? 'meetings.personInUse' : 'meetings.personSaveFailed');
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <div class="pa-4">
    <v-tabs v-model="tab" color="primary" class="mb-4">
      <v-tab value="people">{{ t('meetings.settings.peopleTab') }}</v-tab>
      <v-tab value="topics">{{ t('meetings.settings.topicsTab') }}</v-tab>
      <v-tab value="rooms">{{ t('meetings.settings.roomsTab') }}</v-tab>
      <v-tab value="notices">{{ t('meetings.settings.noticesTab') }}</v-tab>
    </v-tabs>
    <MtMeetingTopics v-if="tab === 'topics'" @changed="emit('topics')" />
    <MtMeetingRooms v-else-if="tab === 'rooms'" @changed="emit('rooms')" />
    <MtMeetingNoticeSettings v-else-if="tab === 'notices'" />
    <div v-else>
    <div class="d-flex align-center justify-space-between flex-wrap ga-3 mb-4">
      <p class="text-body-2 text-medium-emphasis mb-0">{{ t('meetings.settings.hint') }}</p>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">{{ t('meetings.settings.add') }}</v-btn>
    </div>

    <div class="d-flex flex-wrap ga-3 mb-3 align-center">
      <v-text-field
        v-model="search"
        :label="t('meetings.settings.search')"
        density="compact"
        variant="outlined"
        hide-details
        clearable
        prepend-inner-icon="mdi-magnify"
        style="max-width: 320px; min-width: 220px;"
      />
      <v-select
        v-model="organizationFilter"
        :items="organizationOptions"
        :label="t('meetings.settings.organization')"
        :placeholder="t('meetings.settings.allOrganizations')"
        density="compact"
        variant="outlined"
        hide-details
        clearable
        style="max-width: 280px; min-width: 200px;"
      />
      <div class="text-body-2 text-medium-emphasis">{{ t('meetings.settings.count', { n: filteredPeople.length }) }}</div>
    </div>

    <v-data-table
      v-model:page="page"
      :headers="headers"
      :items="filteredPeople"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      :items-per-page="25"
      :items-per-page-options="[10, 25, 50]"
    >
      <template #item.organization="{ item }">{{ personOf(item).organization || '—' }}</template>
      <template #item.email="{ item }">{{ personOf(item).email || '—' }}</template>
      <template #item.note="{ item }">{{ personOf(item).note || '—' }}</template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end">
          <v-btn icon variant="text" size="small" :aria-label="t('meetings.settings.edit')" @click="openEdit(personOf(item))">
            <v-icon>mdi-pencil-outline</v-icon>
          </v-btn>
          <v-btn icon variant="text" size="small" color="error" :aria-label="t('meetings.delete')" @click="deleteTarget = personOf(item)">
            <TrashIcon size="18" />
          </v-btn>
        </div>
      </template>
      <template #no-data>
        <div class="text-body-2 text-medium-emphasis py-6">
          {{ people.length ? t('meetings.settings.noMatch') : t('meetings.settings.empty') }}
        </div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="560">
      <v-card>
        <v-card-title>{{ editingId ? t('meetings.settings.edit') : t('meetings.settings.add') }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.name" :label="t('meetings.person.name')" variant="outlined" autofocus />
          <v-text-field v-model="form.organization" :label="t('meetings.person.organization')" variant="outlined" />
          <v-text-field v-model="form.email" :label="t('meetings.person.email')" variant="outlined" />
          <v-textarea v-model="form.note" :label="t('meetings.person.note')" variant="outlined" rows="2" auto-grow />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">{{ t('meetings.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" @click="save">{{ t('meetings.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="deleteTarget !== null" max-width="480" @update:model-value="(open) => { if (!open) deleteTarget = null; }">
      <v-card v-if="deleteTarget">
        <v-card-title>{{ t('meetings.settings.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('meetings.settings.deleteBody', { name: deleteTarget.name }) }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">{{ t('meetings.cancel') }}</v-btn>
          <v-btn color="error" :loading="saving" @click="remove">{{ t('meetings.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
    </div>
  </div>
</template>
