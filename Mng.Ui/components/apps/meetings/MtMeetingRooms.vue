<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  createOrgMeetingRoom,
  deleteOrgMeetingRoom,
  listOrgMeetingRooms,
  updateOrgMeetingRoom,
  type OrgMeetingRoom,
} from '@/services/meetingsService';
import { TrashIcon } from 'vue-tabler-icons';

const emit = defineEmits<{ changed: [] }>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const rooms = ref<OrgMeetingRoom[]>([]);
const loading = ref(false);
const saving = ref(false);
const dialog = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<OrgMeetingRoom | null>(null);
const form = ref({ name: '', capacity: '', archived: false });

onMounted(() => {
  void load();
});

async function load() {
  loading.value = true;
  try {
    rooms.value = await listOrgMeetingRooms();
  } catch (error) {
    panelError(error, 'meetings.room.loadFailed');
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  form.value = { name: '', capacity: '', archived: false };
  dialog.value = true;
}

function openEdit(room: OrgMeetingRoom) {
  editingId.value = room.id;
  form.value = { name: room.name, capacity: room.capacity ? String(room.capacity) : '', archived: room.archived };
  dialog.value = true;
}

async function save() {
  const name = form.value.name.trim();
  if (!name) {
    panelError(new Error('name'), 'meetings.room.nameRequired');
    return;
  }
  const capacity = Number(form.value.capacity);
  saving.value = true;
  try {
    const input = {
      name,
      capacity: Number.isFinite(capacity) && capacity > 0 ? Math.round(capacity) : null,
      archived: form.value.archived,
    };
    if (editingId.value) await updateOrgMeetingRoom(editingId.value, input);
    else await createOrgMeetingRoom(input);
    dialog.value = false;
    await load();
    emit('changed');
  } catch (error) {
    panelError(error, 'meetings.room.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function remove() {
  if (!deleteTarget.value) return;
  saving.value = true;
  try {
    await deleteOrgMeetingRoom(deleteTarget.value.id);
    deleteTarget.value = null;
    await load();
    emit('changed');
  } catch (error) {
    const message = error instanceof Error ? error.message : '';
    panelError(error, message.includes('ROOM_IN_USE') ? 'meetings.room.inUse' : 'meetings.room.saveFailed');
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between flex-wrap ga-3 mb-4">
      <p class="text-body-2 text-medium-emphasis mb-0">{{ t('meetings.room.hint') }}</p>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">{{ t('meetings.room.add') }}</v-btn>
    </div>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />
    <v-list v-if="rooms.length" class="pa-0">
      <v-list-item v-for="room in rooms" :key="room.id" class="px-0" @click="openEdit(room)">
        <v-list-item-title>
          {{ room.name }}
          <v-chip v-if="room.archived" size="x-small" class="ms-2" variant="tonal">{{ t('meetings.room.archived') }}</v-chip>
        </v-list-item-title>
        <v-list-item-subtitle v-if="room.capacity">{{ t('meetings.room.capacityValue', { n: room.capacity }) }}</v-list-item-subtitle>
        <template #append>
          <v-btn icon variant="text" :aria-label="t('meetings.delete')" @click.stop="deleteTarget = room">
            <TrashIcon size="18" />
          </v-btn>
        </template>
      </v-list-item>
    </v-list>
    <p v-else-if="!loading" class="text-medium-emphasis">{{ t('meetings.room.empty') }}</p>

    <v-dialog v-model="dialog" max-width="480">
      <v-card>
        <v-card-title>{{ editingId ? t('meetings.room.edit') : t('meetings.room.add') }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.name" :label="t('meetings.room.name')" variant="outlined" />
          <v-text-field v-model="form.capacity" :label="t('meetings.room.capacity')" type="number" min="1" variant="outlined" />
          <v-switch v-model="form.archived" :label="t('meetings.room.archived')" color="primary" hide-details />
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
        <v-card-title>{{ t('meetings.room.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('meetings.room.deleteBody', { name: deleteTarget.name }) }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">{{ t('meetings.cancel') }}</v-btn>
          <v-btn color="error" :loading="saving" @click="remove">{{ t('meetings.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
