<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAppToast } from '@/composables/useAppToast';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diCreateFileResource, diListTemplates } from '@/services/documentIntelligenceService';
import {
  createOrgMeeting,
  createOrgMeetingAction,
  createOrgMeetingAttendance,
  createOrgMeetingPerson,
  listOrgMeetingPeople,
} from '@/services/meetingsService';
import type { DiTemplateSummary } from '@/types/apps/documentIntelligence';
import { fileTitle, minutesReaderFor, parseMinutesDocx, type ParsedMinutes } from '@/utils/pmMinutesDocx';

const open = defineModel<boolean>({ required: true });
const emit = defineEmits<{ saved: [] }>();

const { t } = useAppI18n();
const toast = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');

const templates = ref<DiTemplateSummary[]>([]);
const templateId = ref<string | null>(null);
const loadingTemplates = ref(false);
const file = ref<File | null>(null);
const picked = ref<File | File[] | null>(null);
const preview = ref<ParsedMinutes | null>(null);
const busy = ref(false);

const bvTemplates = computed(() => templates.value.filter((row) => minutesReaderFor(row.code)));
const selected = computed(() => bvTemplates.value.find((row) => row.id === templateId.value) || null);
const canSave = computed(() => Boolean(file.value && preview.value?.readable && selected.value && !busy.value));

watch(open, (value) => {
  if (!value) return;
  file.value = null;
  picked.value = null;
  preview.value = null;
  void loadTemplates();
});

watch(picked, (value) => {
  const next = Array.isArray(value) ? value[0] || null : value;
  void readFile(next);
});

async function loadTemplates() {
  if (templates.value.length) {
    prefer();
    return;
  }
  loadingTemplates.value = true;
  try {
    const listed = await diListTemplates();
    templates.value = listed.items.filter((row) => (row.status || '').toLowerCase() === 'published');
    prefer();
  } catch (error) {
    panelError(error, 'meetings.import.loadFailed');
  } finally {
    loadingTemplates.value = false;
  }
}

function prefer() {
  const match = bvTemplates.value[0];
  if (match) templateId.value = match.id;
}

async function readFile(next: File | null) {
  file.value = next;
  preview.value = null;
  if (!next || !selected.value) return;
  if (next.size > 20 * 1024 * 1024) {
    panelError(new Error('size'), 'meetings.import.tooLarge');
    file.value = null;
    return;
  }
  busy.value = true;
  try {
    preview.value = await parseMinutesDocx(next, selected.value.code);
  } catch (error) {
    panelError(error, 'meetings.import.readFailed');
  } finally {
    busy.value = false;
  }
}

async function fileToBase64(source: File): Promise<string> {
  const dataUrl = await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result || ''));
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(source);
  });
  return dataUrl.includes(',') ? dataUrl.split(',')[1] : dataUrl;
}

async function save() {
  const current = file.value;
  const parsed = preview.value;
  if (!current || !parsed?.readable || !selected.value) return;
  busy.value = true;
  try {
    const stored = await diCreateFileResource({
      parentId: null,
      name: current.name,
      originalFileName: current.name,
      content: await fileToBase64(current),
      mimeType: current.type || 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      extension: 'docx',
      size: current.size,
    });
    const meetingId = await createOrgMeeting({
      title: parsed.name || fileTitle(current.name),
      startAt: parsed.startAt || new Date().toISOString(),
      endAt: parsed.endAt,
      status: 'held',
      location: parsed.location,
      agenda: parsed.topics ? parsed.topics.slice(0, 4000) : null,
      minutesResourceId: stored.id,
    });
    const people = await listOrgMeetingPeople();
    const byName = new Map(people.map((row) => [row.name.toLocaleLowerCase('tr'), row]));
    for (const row of parsed.participants) {
      const key = row.name.toLocaleLowerCase('tr');
      let person = byName.get(key);
      if (!person) {
        person = await createOrgMeetingPerson({ name: row.name, organization: row.organization || null });
        byName.set(key, person);
      }
      await createOrgMeetingAttendance({
        meetingId,
        kind: 'external',
        personId: person.id,
        displayName: person.name,
        organization: person.organization || row.organization || null,
        presence: 'attended',
      });
    }
    for (const row of parsed.actions) {
      const note = [row.code, ...row.firms].filter(Boolean).join(' · ');
      await createOrgMeetingAction({
        meetingId,
        title: row.title,
        dueAt: row.dueDate,
        status: 'open',
        note: note || null,
      });
    }
    toast.push({
      title: t('meetings.import.savedTitle'),
      message: t('meetings.import.saved'),
      severity: 'success',
    });
    open.value = false;
    emit('saved');
  } catch (error) {
    panelError(error, 'meetings.import.saveFailed');
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="640" scrollable>
    <v-card>
      <v-card-title>{{ t('meetings.import.title') }}</v-card-title>
      <v-card-text>
        <p class="text-medium-emphasis mb-3">{{ t('meetings.import.hint') }}</p>
        <v-alert v-if="!loadingTemplates && !bvTemplates.length" type="warning" variant="tonal" class="mb-3">
          {{ t('meetings.import.noTemplate') }}
        </v-alert>
        <v-select
          v-model="templateId"
          :items="bvTemplates"
          item-title="name"
          item-value="id"
          :label="t('meetings.import.template')"
          :loading="loadingTemplates"
          variant="outlined"
        />
        <v-file-input
          v-model="picked"
          :label="t('meetings.import.file')"
          accept=".docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
          variant="outlined"
          :disabled="!selected"
        />
        <div v-if="preview">
          <v-alert v-if="!preview.readable" type="warning" variant="tonal">
            {{ t('meetings.import.unreadable') }}
          </v-alert>
          <template v-else>
            <div class="text-subtitle-2">{{ preview.name }}</div>
            <div class="text-medium-emphasis">
              {{ t('meetings.import.summary', { people: preview.participants.length, actions: preview.actions.length }) }}
            </div>
          </template>
        </div>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="open = false">{{ t('meetings.cancel') }}</v-btn>
        <v-btn color="primary" :loading="busy" :disabled="!canSave" @click="save">{{ t('meetings.import.save') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
