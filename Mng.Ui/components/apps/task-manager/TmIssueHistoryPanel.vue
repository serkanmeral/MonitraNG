<script setup lang="ts">
import { computed } from 'vue';
import type { TmFieldDefinition, TmIssueHistoryEntry } from '@/types/apps/taskManager';
import { useUserStore } from '@/stores/apps/user';
import { formatIssueHistoryValue } from '@/utils/taskManagerIssueHistory';

const props = defineProps<{
  entries: TmIssueHistoryEntry[];
  fieldDefinitions: TmFieldDefinition[];
}>();

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const userStore = useUserStore();

function builtinLabel(fieldKey: string): string | null {
  switch (fieldKey) {
    case 'title':
      return mt('taskManager.issueTitle', 'Başlık');
    case 'description':
      return mt('taskManager.description', 'Açıklama');
    case 'issueTypeId':
      return mt('taskManager.workspaceOverviewIssueTypes', 'Görev tipi');
    case 'statusId':
      return mt('taskManager.workspaceOverviewStatuses', 'Durum');
    case 'priorityId':
      return mt('taskManager.workspaceOverviewPriorities', 'Öncelik');
    case 'assignee':
      return mt('taskManager.issueHistoryFieldAssignee', 'Atanan');
    case 'labels':
      return mt('taskManager.newIssueSectionLabels', 'Etiketler');
    case 'dueDate':
      return mt('taskManager.issueHistoryFieldDueDate', 'Bitiş tarihi');
    case 'storyPoints':
      return mt('taskManager.issueHistoryFieldStoryPoints', 'Story point');
    default:
      return null;
  }
}

function fieldLabel(fieldKey: string | undefined, explicit?: string | null): string {
  if (explicit && explicit.trim()) return explicit.trim();
  if (!fieldKey) return mt('taskManager.issueHistoryUnknownField', 'Alan');
  const def = props.fieldDefinitions.find((f) => f.key === fieldKey);
  if (def?.label) return def.label;
  return builtinLabel(fieldKey) ?? fieldKey;
}

function actorLabel(entry: TmIssueHistoryEntry): string {
  if (entry.userName && entry.userName.trim()) return entry.userName.trim();
  const uid = entry.userId?.trim();
  if (uid) {
    const u = userStore.getUserById(uid);
    if (u) {
      const n = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim();
      return n || u.username || uid;
    }
    return uid;
  }
  return mt('taskManager.issueHistoryUnknownActor', 'Bilinmeyen');
}

function formatWhen(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return String(iso);
  return d.toLocaleString();
}

const hasRows = computed(() => props.entries.length > 0);
</script>

<template>
  <div class="tm-issue-history-panel pa-3">
    <template v-if="!hasRows">
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ mt('taskManager.issueHistoryEmpty', 'Henüz geçmiş kaydı yok. Değişiklikler DG kaydındaki __history alanına yazıldığında burada listelenir.') }}
      </p>
    </template>
    <v-timeline v-else side="end" align="start" density="compact" truncate-line="both">
      <v-timeline-item v-for="(entry, idx) in entries" :key="idx" dot-color="primary" size="small">
        <div class="text-body-2 font-weight-medium">{{ actorLabel(entry) }}</div>
        <div class="text-caption text-medium-emphasis mb-2">{{ formatWhen(entry.changedAt) }}</div>
        <v-table v-if="entry.changes.length" density="compact" class="tm-history-changes rounded border">
          <thead>
            <tr>
              <th class="text-left text-caption">{{ mt('taskManager.issueHistoryColField', 'Alan') }}</th>
              <th class="text-left text-caption">{{ mt('taskManager.issueHistoryColOld', 'Önceki') }}</th>
              <th class="text-left text-caption">{{ mt('taskManager.issueHistoryColNew', 'Sonraki') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(c, j) in entry.changes" :key="j">
              <td class="text-body-2">{{ fieldLabel(c.field, c.label) }}</td>
              <td class="text-body-2 text-medium-emphasis text-break">{{ formatIssueHistoryValue(c.oldValue, c.field) }}</td>
              <td class="text-body-2 text-break">{{ formatIssueHistoryValue(c.newValue, c.field) }}</td>
            </tr>
          </tbody>
        </v-table>
      </v-timeline-item>
    </v-timeline>
  </div>
</template>

<style scoped>
.tm-history-changes :deep(th),
.tm-history-changes :deep(td) {
  vertical-align: top;
}
.text-break {
  word-break: break-word;
}
</style>
