<script setup lang="ts">
import { ref, watch } from 'vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import DiMarkdownPreviewDialog from '@/components/apps/document-intelligence/DiMarkdownPreviewDialog.vue';
import DiPickResourceDialog from '@/components/apps/document-intelligence/DiPickResourceDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  createOrgMeetingAttachment,
  deleteOrgMeetingAttachment,
  listOrgMeetingAttachments,
  type OrgMeetingAttachment,
} from '@/services/meetingsService';
import {
  diAwaitScan,
  diCreateFileResource,
  diCreateMarkdown,
  diGetById,
} from '@/services/documentIntelligenceService';
import { DI_RESOURCE_TYPE, type DiResource } from '@/types/apps/documentIntelligence';
import { diScanBlocksContent } from '@/utils/diFilePreview';
import { diPageResourceLabel } from '@/utils/diPageResource';
import { useDomainFileLimit } from '@/utils/domainFileLimit';
import { TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{ meetingId: string | null }>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const { maxBytes, maxMb } = useDomainFileLimit();

interface AttachmentRow {
  link: OrgMeetingAttachment;
  resource: DiResource | null;
  name: string;
}

const rows = ref<AttachmentRow[]>([]);
const loading = ref(false);
const busy = ref(false);
const pickerOpen = ref(false);
const pageOpen = ref(false);
const pageTitle = ref('');
const fileInput = ref<HTMLInputElement | null>(null);
const notice = ref('');
const filePreviewOpen = ref(false);
const filePreviewResource = ref<DiResource | null>(null);
const markdownPreviewOpen = ref(false);
const markdownPreviewResource = ref<DiResource | null>(null);

function fileExtension(name: string): string {
  const index = name.lastIndexOf('.');
  return index >= 0 ? name.slice(index + 1).toLowerCase() : '';
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result || '');
      resolve(result.includes(',') ? result.split(',')[1] : result);
    };
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

function scanBlockText(status: string | null | undefined): string {
  if (status === 'threat') return t('documentIntelligence.scanThreatMessage');
  if (status === 'failed') return t('documentIntelligence.scanFailedMessage');
  return t('documentIntelligence.scanPendingMessage');
}

async function load() {
  if (!props.meetingId) {
    rows.value = [];
    return;
  }
  loading.value = true;
  try {
    const links = await listOrgMeetingAttachments(props.meetingId);
    const next: AttachmentRow[] = [];
    for (const link of links) {
      try {
        const resource = await diGetById(link.resourceId);
        next.push({ link, resource, name: diPageResourceLabel(resource) });
      } catch {
        next.push({ link, resource: null, name: link.resourceId });
      }
    }
    rows.value = next;
  } catch (error) {
    panelError(error, 'meetings.attachments.loadFailed');
  } finally {
    loading.value = false;
  }
}

async function linkResource(resource: DiResource) {
  if (!props.meetingId || resource.type === DI_RESOURCE_TYPE.folder) return;
  if (rows.value.some((row) => row.link.resourceId === resource.id)) return;
  if (diScanBlocksContent(resource)) {
    notice.value = scanBlockText(resource.scanStatus);
    return;
  }
  busy.value = true;
  try {
    await createOrgMeetingAttachment(props.meetingId, resource.id);
    await load();
  } catch (error) {
    panelError(error, 'meetings.attachments.saveFailed');
  } finally {
    busy.value = false;
  }
}

async function onPicked(resource: DiResource) {
  pickerOpen.value = false;
  await linkResource(resource);
}

async function onFile(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file || !props.meetingId) return;
  if (file.size > maxBytes.value) {
    notice.value = t('documentIntelligence.errors.fileTooLarge', { max: maxMb.value });
    return;
  }
  busy.value = true;
  try {
    const created = await diCreateFileResource({
      parentId: null,
      name: file.name,
      originalFileName: file.name,
      content: await fileToBase64(file),
      mimeType: file.type || null,
      extension: fileExtension(file.name) || null,
      size: file.size,
    });
    const scanned = await diAwaitScan(created);
    await linkResource(scanned);
  } catch (error) {
    panelError(error, 'meetings.attachments.saveFailed');
  } finally {
    busy.value = false;
  }
}

async function addPage() {
  const title = pageTitle.value.trim();
  if (!title) {
    panelError(new Error('name'), 'meetings.attachments.nameRequired');
    return;
  }
  busy.value = true;
  try {
    const created = await diCreateMarkdown({ parentId: null, title, content: '', isDraft: false });
    pageOpen.value = false;
    pageTitle.value = '';
    await linkResource(created);
  } catch (error) {
    panelError(error, 'meetings.attachments.saveFailed');
  } finally {
    busy.value = false;
  }
}

async function removeLink(row: AttachmentRow) {
  busy.value = true;
  try {
    await deleteOrgMeetingAttachment(row.link.id);
    await load();
  } catch (error) {
    panelError(error, 'meetings.attachments.saveFailed');
  } finally {
    busy.value = false;
  }
}

function openRow(row: AttachmentRow) {
  if (!row.resource) return;
  if (diScanBlocksContent(row.resource)) {
    notice.value = scanBlockText(row.resource.scanStatus);
    return;
  }
  if (row.resource.type === DI_RESOURCE_TYPE.markdown) {
    markdownPreviewResource.value = row.resource;
    markdownPreviewOpen.value = true;
    return;
  }
  filePreviewResource.value = row.resource;
  filePreviewOpen.value = true;
}

watch(() => props.meetingId, () => {
  void load();
}, { immediate: true });
</script>

<template>
  <div>
    <p v-if="!meetingId" class="text-medium-emphasis">{{ t('meetings.attachments.afterSave') }}</p>
    <template v-else>
      <p class="text-caption text-medium-emphasis mb-3">{{ t('meetings.attachments.hint') }}</p>
      <v-alert v-if="notice" type="warning" variant="tonal" class="mb-3" closable @click:close="notice = ''">{{ notice }}</v-alert>
      <div class="d-flex flex-wrap ga-2 mb-3">
        <v-btn size="small" variant="tonal" :loading="busy" @click="pickerOpen = true">{{ t('meetings.attachments.pick') }}</v-btn>
        <v-btn size="small" variant="tonal" :loading="busy" @click="fileInput?.click()">{{ t('meetings.attachments.upload') }}</v-btn>
        <v-btn size="small" variant="tonal" :loading="busy" @click="pageOpen = true">{{ t('meetings.attachments.newPage') }}</v-btn>
        <input ref="fileInput" type="file" class="d-none" @change="onFile" />
      </div>
      <v-progress-linear v-if="loading" indeterminate class="mb-2" />
      <v-list v-if="rows.length" density="compact">
        <v-list-item v-for="row in rows" :key="row.link.id">
          <v-list-item-title>{{ row.name }}</v-list-item-title>
          <template #append>
            <v-btn size="small" variant="text" :disabled="!row.resource" @click="openRow(row)">{{ t('meetings.attachments.open') }}</v-btn>
            <v-btn icon variant="text" :aria-label="t('meetings.attachments.remove')" :loading="busy" @click="removeLink(row)">
              <TrashIcon size="18" />
            </v-btn>
          </template>
        </v-list-item>
      </v-list>
      <p v-else-if="!loading" class="text-medium-emphasis">{{ t('meetings.attachments.empty') }}</p>
    </template>

    <DiPickResourceDialog
      v-model="pickerOpen"
      :title="t('meetings.attachments.pick')"
      :exclude-resource-ids="rows.map((row) => row.link.resourceId)"
      @pick="onPicked"
    />

    <v-dialog v-model="pageOpen" max-width="480">
      <v-card>
        <v-card-title>{{ t('meetings.attachments.newPage') }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="pageTitle" :label="t('meetings.attachments.pageTitle')" variant="outlined" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="pageOpen = false">{{ t('meetings.cancel') }}</v-btn>
          <v-btn color="primary" :loading="busy" @click="addPage">{{ t('meetings.attachments.add') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <DiFilePreviewDialog v-model="filePreviewOpen" :resource="filePreviewResource" />
    <DiMarkdownPreviewDialog v-model="markdownPreviewOpen" :resource="markdownPreviewResource" />
  </div>
</template>
