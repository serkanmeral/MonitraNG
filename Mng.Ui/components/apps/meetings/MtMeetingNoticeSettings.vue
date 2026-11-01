<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAppToast } from '@/composables/useAppToast';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { listActiveMailTemplateOptions } from '@/services/notifier/mailTemplates';
import {
  MEETING_NOTICE_KINDS,
  ensureOrgMeetingNotices,
  listOrgMeetingNoticeSettings,
  updateOrgMeetingNoticeSetting,
  type MeetingNoticeKind,
} from '@/services/meetingsService';

const { t } = useAppI18n();
const toast = useAppToast();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const saving = ref(false);
const templates = ref<{ value: string; title: string }[]>([]);
const drafts = ref<Record<MeetingNoticeKind, { id: string; enabled: boolean; templateKey: string }>>({
  invite: { id: '', enabled: false, templateKey: '' },
  update: { id: '', enabled: false, templateKey: '' },
  cancel: { id: '', enabled: false, templateKey: '' },
});

onMounted(async () => {
  loading.value = true;
  try {
    await ensureOrgMeetingNotices();
    const [settings, options] = await Promise.all([
      listOrgMeetingNoticeSettings(),
      listActiveMailTemplateOptions().catch(() => [] as { value: string; title: string }[]),
    ]);
    templates.value = options;
    for (const kind of MEETING_NOTICE_KINDS) {
      const row = settings.find((item) => item.kind === kind);
      if (!row) continue;
      drafts.value[kind] = { id: row.id, enabled: row.enabled, templateKey: row.templateKey };
    }
  } catch (error) {
    panelError(error, 'meetings.notice.loadFailed');
  } finally {
    loading.value = false;
  }
});

async function save() {
  saving.value = true;
  try {
    for (const kind of MEETING_NOTICE_KINDS) {
      const row = drafts.value[kind];
      if (!row.id) continue;
      await updateOrgMeetingNoticeSetting(row.id, { kind, enabled: row.enabled, templateKey: row.templateKey });
    }
    toast.push({ title: t('meetings.notice.savedTitle'), message: t('meetings.notice.saved'), severity: 'success' });
  } catch (error) {
    panelError(error, 'meetings.notice.saveFailed');
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <div>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />
    <p class="text-body-2 text-medium-emphasis mb-4">{{ t('meetings.notice.settingsHint') }}</p>
    <p class="text-caption text-medium-emphasis mb-4">{{ t('meetings.notice.placeholders') }}</p>
    <div class="d-flex flex-column ga-4">
      <section v-for="kind in MEETING_NOTICE_KINDS" :key="kind" class="mt-notice-row rounded-lg pa-4">
        <div class="d-flex align-center justify-space-between ga-3 mb-3">
          <div class="text-subtitle-1">{{ t(`meetings.notice.kind.${kind}`) }}</div>
          <v-switch v-model="drafts[kind].enabled" :label="t('meetings.notice.enabled')" color="primary" hide-details density="compact" />
        </div>
        <div class="text-body-2 text-medium-emphasis mb-3">{{ t(`meetings.notice.kindHint.${kind}`) }}</div>
        <v-select
          v-model="drafts[kind].templateKey"
          :items="templates"
          item-title="title"
          item-value="value"
          :label="t('meetings.notice.template')"
          variant="outlined"
          density="comfortable"
          hide-details
          clearable
        />
      </section>
    </div>
    <div class="d-flex justify-end mt-4">
      <v-btn color="primary" :loading="saving" :disabled="loading" @click="save">{{ t('meetings.save') }}</v-btn>
    </div>
  </div>
</template>

<style scoped>
.mt-notice-row {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
