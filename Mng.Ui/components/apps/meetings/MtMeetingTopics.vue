<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  MEETING_TOPIC_COLORS,
  createOrgMeetingTopic,
  listOrgMeetingTopics,
  meetingTopicColorHex,
  updateOrgMeetingTopic,
  type MeetingTopicColor,
  type OrgMeetingTopic,
} from '@/services/meetingsService';

const emit = defineEmits<{ changed: [] }>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const topics = ref<OrgMeetingTopic[]>([]);
const loading = ref(false);
const saving = ref(false);
const dialog = ref(false);
const editingId = ref<string | null>(null);
const form = ref({ name: '', code: '', color: 'blue' as MeetingTopicColor, archived: false });

onMounted(() => {
  void load();
});

async function load() {
  loading.value = true;
  try {
    topics.value = await listOrgMeetingTopics();
  } catch (error) {
    panelError(error, 'meetings.topic.saveFailed');
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingId.value = null;
  form.value = { name: '', code: '', color: 'blue', archived: false };
  dialog.value = true;
}

function openEdit(topic: OrgMeetingTopic) {
  editingId.value = topic.id;
  form.value = { name: topic.name, code: topic.code, color: topic.color, archived: topic.archived };
  dialog.value = true;
}

async function save() {
  const name = form.value.name.trim();
  if (!name) {
    panelError(new Error('name'), 'meetings.topic.nameRequired');
    return;
  }
  saving.value = true;
  try {
    const input = { name, code: form.value.code, color: form.value.color, archived: form.value.archived };
    if (editingId.value) await updateOrgMeetingTopic(editingId.value, input);
    else await createOrgMeetingTopic(input);
    dialog.value = false;
    await load();
    emit('changed');
  } catch (error) {
    panelError(error, 'meetings.topic.saveFailed');
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between flex-wrap ga-3 mb-4">
      <p class="text-body-2 text-medium-emphasis mb-0">{{ t('meetings.topic.hint') }}</p>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">{{ t('meetings.topic.new') }}</v-btn>
    </div>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />
    <v-list v-if="topics.length" class="pa-0">
      <v-list-item v-for="topic in topics" :key="topic.id" class="px-0" @click="openEdit(topic)">
        <template #prepend>
          <span class="mt-topic-dot" :style="{ background: meetingTopicColorHex(topic.color) }" />
        </template>
        <v-list-item-title>
          {{ topic.name }}
          <span v-if="topic.code" class="text-medium-emphasis"> · {{ topic.code }}</span>
          <v-chip v-if="topic.archived" size="x-small" class="ms-2" variant="tonal">{{ t('meetings.topic.archive') }}</v-chip>
        </v-list-item-title>
      </v-list-item>
    </v-list>
    <p v-else-if="!loading" class="text-medium-emphasis">{{ t('meetings.topic.empty') }}</p>

    <v-dialog v-model="dialog" max-width="480">
      <v-card>
        <v-card-title>{{ editingId ? t('meetings.topic.edit') : t('meetings.topic.new') }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" :label="t('meetings.topic.name')" variant="outlined" />
          <v-text-field v-model="form.code" :label="t('meetings.topic.code')" variant="outlined" />
          <div class="text-subtitle-2 mb-2">{{ t('meetings.topic.color') }}</div>
          <div class="d-flex flex-wrap ga-2 mb-4">
            <button
              v-for="color in MEETING_TOPIC_COLORS"
              :key="color"
              type="button"
              class="mt-topic-swatch"
              :class="{ 'mt-topic-swatch--on': form.color === color }"
              :style="{ background: meetingTopicColorHex(color) }"
              :aria-label="t(`meetings.topic.colors.${color}`)"
              @click="form.color = color"
            />
          </div>
          <v-switch v-model="form.archived" :label="t('meetings.topic.archive')" color="primary" hide-details />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">{{ t('meetings.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" @click="save">{{ t('meetings.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.mt-topic-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  display: inline-block;
}
.mt-topic-swatch {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  border: 2px solid transparent;
  cursor: pointer;
}
.mt-topic-swatch--on {
  border-color: rgb(var(--v-theme-on-surface));
}
</style>
