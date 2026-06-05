<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import DOMPurify from 'dompurify';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcDynamicForm from '@/components/apps/operation-core/OcDynamicForm.vue';
import OcBoardCatalogLabel from '@/components/apps/operation-core/OcBoardCatalogLabel.vue';
import OcSlaStatusChip from '@/components/apps/operation-core/OcSlaStatusChip.vue';
import OcCommentComposer from '@/components/apps/operation-core/OcCommentComposer.client.vue';
import OcPolicyPanel from '@/components/apps/operation-core/OcPolicyPanel.vue';
import OcTransitionRequiredFields from '@/components/apps/operation-core/OcTransitionRequiredFields.vue';
import OcAttachmentPreviewDialog from '@/components/apps/operation-core/OcAttachmentPreviewDialog.vue';
import { isPreviewable } from '@/utils/ocAttachmentPreview';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import { useOcBoardListLookups } from '@/composables/useOcBoardListLookups';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  buildUpdateWorkItemRequest,
  collectOcFormValidationIssues,
  hasUpdateWorkItemChanges,
  initialFormModelFromContext,
  ocAddWorkItemAttachment,
  ocAddWorkItemComment,
  ocApplyTransition,
  ocDeleteWorkItemComment,
  ocDownloadAttachment,
  ocExtractDgErrorMessage,
  ocGetWorkItemProfileView,
  ocGetWorkItemTimeline,
  ocRemoveWorkItemAttachment,
  ocUpdateWorkItem,
  ocUpdateWorkItemComment,
} from '@/services/operationCoreService';
import { useAuthStore } from '@/stores/auth';
import type {
  OcAttachment,
  OcBoardCatalogs,
  OcFormRuntimeContext,
  OcProfileAction,
  OcResolvedPolicy,
  OcTimelineEntry,
  OcWorkItemProfile,
} from '@/types/apps/operationCore';
import { enrichFormRuntimeFields } from '@/utils/ocFormFieldLabels';
import { formatCellValue } from '@/utils/ocColumnFormat';

definePageMeta({ layout: 'default' });

const { t, locale } = useAppI18n();
const route = useRoute();
const store = useOperationCoreStore();

const workItemId = computed(() => String(route.params.id ?? ''));
const boardIdQuery = computed(() =>
  typeof route.query.boardId === 'string' ? route.query.boardId.trim() : ''
);

const loading = ref(false);
const errorLocal = ref<string | null>(null);
const formContext = ref<OcFormRuntimeContext | null>(null);
const formModel = ref<Record<string, unknown>>({});
const profile = ref<OcWorkItemProfile | null>(null);

// --- Detaylar sekmesi in-place düzenleme ---
const editMode = ref(false);
const initialModel = ref<Record<string, unknown>>({});
const savingEdit = ref(false);
const editError = ref<string | null>(null);
const editValidationAttempted = ref(false);

// Tek toplu profile-view payload'ından gelen çözülmüş veriler (ek fetch yapılmaz).
const catalogs = ref<OcBoardCatalogs | null>(null);
const fieldDisplays = ref<Record<string, string>>({});
const resolvedPolicy = ref<OcResolvedPolicy | null>(null);

const activeTab = ref<'details' | 'comments' | 'activity' | 'attachments'>('details');

// --- Aktivite / yorum ---
const timeline = ref<OcTimelineEntry[]>([]);
const timelineTotal = ref(0);
const timelineLoading = ref(false);
const commentSending = ref(false);
const commentError = ref<string | null>(null);
const composerRef = ref<{ reset: () => void } | null>(null);

// --- Yorum yanıtı (tek seviye thread) ---
const replyingToId = ref<string | null>(null);
const replyingToActor = ref<string | null>(null);

// --- Yorum düzenleme/silme (yalnızca kendi yorumları) ---
const authStore = useAuthStore();
const currentPersonId = computed(() => authStore.userInfo?.mng_person_id ?? null);
const editingId = ref<string | null>(null);
const editSending = ref(false);
const deleteTargetId = ref<string | null>(null);
const deleteBusy = ref(false);

function canModifyComment(entry: OcTimelineEntry): boolean {
  const me = currentPersonId.value;
  return !!me && !!entry.actorId && entry.actorId === me;
}

function startEdit(entry: OcTimelineEntry) {
  cancelReply();
  editingId.value = entry.id ?? null;
  commentError.value = null;
}

function cancelEdit() {
  editingId.value = null;
}

async function submitEdit(commentId: string, payload: { html: string }) {
  const body = (payload.html ?? '').trim();
  if (!body) return;
  editSending.value = true;
  commentError.value = null;
  try {
    await ocUpdateWorkItemComment(workItemId.value, commentId, body);
    editingId.value = null;
    await loadTimeline();
  } catch (e: unknown) {
    commentError.value = ocExtractDgErrorMessage(e, t('operationCore.profile.comments.error'));
  } finally {
    editSending.value = false;
  }
}

async function confirmDeleteComment() {
  const commentId = deleteTargetId.value;
  if (!commentId) return;
  deleteBusy.value = true;
  commentError.value = null;
  try {
    await ocDeleteWorkItemComment(workItemId.value, commentId);
    deleteTargetId.value = null;
    await loadTimeline();
  } catch (e: unknown) {
    commentError.value = ocExtractDgErrorMessage(e, t('operationCore.profile.comments.error'));
  } finally {
    deleteBusy.value = false;
  }
}

// Yalnızca yorum girdileri (root + tek seviye yanıtlar gruplanır).
const commentThreads = computed(() => {
  const all = timeline.value.filter((e) => e.type === 'comment');
  const ids = new Set(all.map((c) => c.id ?? ''));
  const byParent = new Map<string, OcTimelineEntry[]>();
  const roots: OcTimelineEntry[] = [];
  for (const c of all) {
    const pid = c.parentId ?? '';
    if (pid && ids.has(pid)) {
      const arr = byParent.get(pid) ?? [];
      arr.push(c);
      byParent.set(pid, arr);
    } else {
      roots.push(c);
    }
  }
  // Timeline en yeni önce gelir; yanıtları kronolojik (eski→yeni) göster.
  return roots.map((r) => ({
    root: r,
    replies: (byParent.get(r.id ?? '') ?? []).slice().reverse(),
  }));
});

const commentCount = computed(() => timeline.value.filter((e) => e.type === 'comment').length);

// Yorum dışı (durum/geçiş/sistem) girdiler — Aktivite sekmesi.
const activityEntries = computed(() => timeline.value.filter((e) => e.type !== 'comment'));

// Yorum gövdesi HTML olarak saklanır; render'da XSS'e karşı sanitize edilir (client-only).
function renderCommentHtml(html: string | null | undefined): string {
  if (!html) return '';
  if (!import.meta.client) return '';
  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: [
      'p', 'br', 'strong', 'b', 'em', 'i', 's', 'strike', 'del',
      'ul', 'ol', 'li', 'a', 'code', 'pre', 'blockquote', 'span',
    ],
    ALLOWED_ATTR: ['href', 'target', 'rel'],
  });
}

function startReply(entry: OcTimelineEntry) {
  replyingToId.value = entry.id ?? null;
  replyingToActor.value = entry.actor ?? null;
  activeTab.value = 'comments';
}

function cancelReply() {
  replyingToId.value = null;
  replyingToActor.value = null;
}

const workItemTitle = computed(() => {
  const fromProfile = profile.value?.workItem.title;
  if (fromProfile?.trim()) return fromProfile;
  const fromModel = formModel.value.title;
  return (typeof fromModel === 'string' && fromModel.trim()) || t('operationCore.profile.placeholderTitle');
});

const workItemKey = computed(() => profile.value?.workItem.key ?? '');

const pageTitle = computed(() => {
  const name = formContext.value?.formName?.trim();
  const base = t('operationCore.profile.title');
  return name ? `${base} — ${name}` : base;
});

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  tail: computed(() => ({
    text: workItemTitle.value,
    disabled: true,
  })),
});

const backToBoardTo = computed(() =>
  boardIdQuery.value ? `/apps/operation-core/boards/${encodeURIComponent(boardIdQuery.value)}` : null
);

// --- Lookups (durum/öncelik/tip + kişi adları) ---
const workspaceIdRef = computed(() => profile.value?.workspaceId ?? null);
const peopleRef = computed(() => profile.value?.people ?? null);
const catalogsRef = computed(() => catalogs.value);
const { resolveState, resolvePriority, resolveType, resolvePersonName } = useOcBoardListLookups(
  workspaceIdRef,
  catalogsRef,
  peopleRef
);

// Grup id → ad (readonly grup alanlarında OcDynamicForm'a verilir; MO ProfileRuntimeContext.groups).
const groupNames = computed<Record<string, string>>(() => {
  const out: Record<string, string> = {};
  const groups = profile.value?.groups ?? {};
  for (const [id, display] of Object.entries(groups)) {
    out[id] = display?.name?.trim() || id;
  }
  return out;
});

const summary = computed(() => profile.value?.workItem ?? null);
const canComment = computed(
  () =>
    profile.value?.permissions?.canComment === true ||
    formContext.value?.permissions?.canComment === true
);
const canEdit = computed(
  () =>
    profile.value?.permissions?.canEdit === true ||
    formContext.value?.permissions?.canEdit === true
);

// --- Detaylar sekmesi düzenleme (in-place) ---
const editValidationIssues = computed(() => {
  if (!editMode.value || !formContext.value) return [];
  return collectOcFormValidationIssues(formContext.value, formModel.value);
});

const editFieldErrors = computed(() => {
  if (!editValidationAttempted.value) return {} as Record<string, string>;
  const msg = t('operationCore.formUi.fieldRequired');
  const errors: Record<string, string> = {};
  for (const issue of editValidationIssues.value) errors[issue.fieldKey] = msg;
  return errors;
});

function startFormEdit() {
  if (!canEdit.value) return;
  activeTab.value = 'details';
  editError.value = null;
  editValidationAttempted.value = false;
  editMode.value = true;
}

function cancelFormEdit() {
  formModel.value = JSON.parse(JSON.stringify(initialModel.value));
  editError.value = null;
  editValidationAttempted.value = false;
  editMode.value = false;
}

function collectFormChanges(): Record<string, unknown> {
  const changed: Record<string, unknown> = {};
  const keys = new Set([...Object.keys(formModel.value), ...Object.keys(initialModel.value)]);
  for (const key of keys) {
    const current = formModel.value[key];
    const before = initialModel.value[key];
    if (JSON.stringify(current ?? null) !== JSON.stringify(before ?? null)) {
      changed[key] = current;
    }
  }
  return changed;
}

async function saveFormEdit() {
  const id = workItemId.value;
  if (!formContext.value || !id) return;
  editValidationAttempted.value = true;
  if (editValidationIssues.value.length) {
    editError.value = t('operationCore.create.validationRequired');
    return;
  }
  const patch = buildUpdateWorkItemRequest(collectFormChanges());
  if (!hasUpdateWorkItemChanges(patch)) {
    editMode.value = false;
    return;
  }
  savingEdit.value = true;
  editError.value = null;
  try {
    await ocUpdateWorkItem(id, patch);
    editMode.value = false;
    await loadProfile();
  } catch (e: unknown) {
    editError.value = ocExtractDgErrorMessage(e, t('operationCore.profile.edit.saveError'));
  } finally {
    savingEdit.value = false;
  }
}

// --- Ekler ---
const attachments = computed<OcAttachment[]>(() => profile.value?.attachments ?? []);
const fileInput = ref<HTMLInputElement | null>(null);
const attachUploading = ref(false);
const attachError = ref<string | null>(null);
const removingPath = ref<string | null>(null);

function fmtFileSize(kb: number | null | undefined): string {
  if (kb == null || !Number.isFinite(kb)) return '';
  if (kb < 1024) return `${Math.max(1, Math.round(kb))} KB`;
  return `${(kb / 1024).toFixed(1)} MB`;
}

function attachmentIcon(att: OcAttachment): string {
  const ext = (att.fileExt ?? '').toLowerCase();
  if (['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'svg'].includes(ext)) return 'mdi-file-image-outline';
  if (ext === 'pdf') return 'mdi-file-pdf-box';
  if (['doc', 'docx'].includes(ext)) return 'mdi-file-word-outline';
  if (['xls', 'xlsx', 'csv'].includes(ext)) return 'mdi-file-excel-outline';
  if (['zip', 'rar', '7z', 'gz'].includes(ext)) return 'mdi-folder-zip-outline';
  return 'mdi-file-outline';
}

function triggerFilePick() {
  attachError.value = null;
  fileInput.value?.click();
}

async function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file) return;
  attachUploading.value = true;
  attachError.value = null;
  try {
    profile.value = await ocAddWorkItemAttachment(workItemId.value, attachments.value, file);
  } catch (e: unknown) {
    attachError.value = ocExtractDgErrorMessage(e, t('operationCore.profile.attachments.uploadError'));
  } finally {
    attachUploading.value = false;
  }
}

async function downloadAttachment(att: OcAttachment) {
  attachError.value = null;
  try {
    await ocDownloadAttachment(att);
  } catch (e: unknown) {
    attachError.value = ocExtractDgErrorMessage(e, t('operationCore.profile.attachments.downloadError'));
  }
}

// --- Ek önizleme (görsel/PDF/düz metin; modal) ---
const previewOpen = ref(false);
const previewAtt = ref<OcAttachment | null>(null);

function canPreview(att: OcAttachment): boolean {
  return isPreviewable(att);
}

function openPreview(att: OcAttachment) {
  previewAtt.value = att;
  previewOpen.value = true;
}

// Chip/satır tıklaması: önizlenebilirse aç, değilse doğrudan indir.
function previewOrDownload(att: OcAttachment) {
  if (canPreview(att)) openPreview(att);
  else void downloadAttachment(att);
}

async function removeAttachment(att: OcAttachment) {
  removingPath.value = att.path;
  attachError.value = null;
  try {
    profile.value = await ocRemoveWorkItemAttachment(workItemId.value, attachments.value, att.path);
  } catch (e: unknown) {
    attachError.value = ocExtractDgErrorMessage(e, t('operationCore.profile.attachments.removeError'));
  } finally {
    removingPath.value = null;
  }
}

function fmtDate(value: string | null | undefined): string {
  return formatCellValue(value, 'date', { locale: locale() });
}

function fmtAge(value: string | null | undefined, anchorEnd?: string | null): string {
  return formatCellValue(value, 'relativeTime', { locale: locale(), anchorEnd: anchorEnd ?? null });
}

const watcherNames = computed(() =>
  (profile.value?.watchers ?? []).map((id) => ({ id, name: resolvePersonName(id) }))
);

function timelineIcon(entry: OcTimelineEntry): string {
  switch (entry.type) {
    case 'comment':
      return 'mdi-comment-text-outline';
    case 'state':
    case 'transition':
      return 'mdi-swap-horizontal';
    default:
      return 'mdi-history';
  }
}

function timelineColor(entry: OcTimelineEntry): string {
  if (entry.type === 'comment') return 'primary';
  if (entry.type === 'state' || entry.type === 'transition') return 'info';
  return 'grey';
}

async function loadTimeline() {
  const id = workItemId.value;
  if (!id) return;
  timelineLoading.value = true;
  try {
    const page = await ocGetWorkItemTimeline(id, 0, 100);
    timeline.value = page.items;
    timelineTotal.value = page.total || page.items.length;
  } catch {
    timeline.value = [];
    timelineTotal.value = 0;
  } finally {
    timelineLoading.value = false;
  }
}

async function submitComment(payload: { html: string; mentions: string[]; files: File[] }) {
  const body = (payload.html ?? '').trim();
  const files = payload.files ?? [];
  if (!body && files.length === 0) return;
  commentSending.value = true;
  commentError.value = null;
  try {
    await ocAddWorkItemComment(workItemId.value, body, replyingToId.value, payload.mentions, files);
    composerRef.value?.reset();
    cancelReply();
    await loadTimeline();
  } catch (e: unknown) {
    commentError.value = ocExtractDgErrorMessage(e, t('operationCore.profile.comments.error'));
  } finally {
    commentSending.value = false;
  }
}

// --- Durum geçişleri (transition aksiyonları) ---
const transitionActions = computed<OcProfileAction[]>(() =>
  [...(profile.value?.actions ?? [])].sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
);
const transitionDialog = ref(false);
const transitionTarget = ref<OcProfileAction | null>(null);
const transitionComment = ref('');
const transitionBusy = ref(false);
const transitionError = ref<string | null>(null);
// Geçiş için zorunlu alanların ön-toplanması (MO 400 dönmeden önce kullanıcıdan al).
const transitionFieldModel = ref<Record<string, unknown>>({});

const transitionRequiredKeys = computed<string[]>(() => {
  const action = transitionTarget.value;
  const ctx = formContext.value;
  if (!action || !ctx) return [];
  return action.requiredFields.filter((key) => !!ctx.fields[key]);
});

// MO StateFlowCatalog.IsEmptyValue ile hizalı: null/boş metin → boş.
function isTransitionValueEmpty(value: unknown): boolean {
  if (value == null) return true;
  if (typeof value === 'string') return value.trim().length === 0;
  if (Array.isArray(value)) return value.length === 0;
  return false;
}

const transitionMissingRequired = computed<string[]>(() =>
  transitionRequiredKeys.value.filter((key) => isTransitionValueEmpty(transitionFieldModel.value[key]))
);

function actionLabel(action: OcProfileAction): string {
  const explicit = action.label?.trim();
  if (explicit) return explicit;
  return resolveState(action.toStateId, null)?.name?.trim() || action.toStateId;
}

function openTransition(action: OcProfileAction) {
  if (!action.enabled) return;
  transitionTarget.value = action;
  transitionComment.value = '';
  transitionError.value = null;
  const ctx = formContext.value;
  const seed: Record<string, unknown> = {};
  if (ctx) {
    for (const key of action.requiredFields) {
      if (ctx.fields[key]) seed[key] = formModel.value[key];
    }
  }
  transitionFieldModel.value = seed;
  transitionDialog.value = true;
}

async function confirmTransition() {
  const action = transitionTarget.value;
  if (!action || transitionMissingRequired.value.length) return;
  transitionBusy.value = true;
  transitionError.value = null;
  try {
    const keys = transitionRequiredKeys.value;
    const fields = keys.length
      ? Object.fromEntries(keys.map((key) => [key, transitionFieldModel.value[key]]))
      : null;
    await ocApplyTransition(workItemId.value, action.transitionKey, {
      comment: transitionComment.value,
      fields,
    });
    transitionDialog.value = false;
    transitionTarget.value = null;
    transitionFieldModel.value = {};
    await loadProfile();
  } catch (e: unknown) {
    transitionError.value = ocExtractDgErrorMessage(e, t('operationCore.profile.transitions.error'));
  } finally {
    transitionBusy.value = false;
  }
}

async function loadProfile() {
  const id = workItemId.value;
  if (!id) return;

  loading.value = true;
  errorLocal.value = null;
  try {
    if (!store.workspaces.length) {
      await store.loadWorkspaces();
    }
    // Tek toplu çağrı: form + katalog + pool alan + alan görünen değerleri + politika + ilk sayfa timeline.
    const view = await ocGetWorkItemProfileView(id);
    formContext.value = enrichFormRuntimeFields(view.form, { poolFields: view.poolFields, translate: t });
    const model = initialFormModelFromContext(view.form);
    formModel.value = model;
    initialModel.value = JSON.parse(JSON.stringify(model));
    editMode.value = false;
    editValidationAttempted.value = false;
    editError.value = null;
    profile.value = view.profile;
    catalogs.value = view.catalogs;
    fieldDisplays.value = view.fieldDisplays;
    resolvedPolicy.value = view.policy;
    // Timeline payload'la birlikte geldi; ayrı çağrı yok (loadTimeline yorum CRUD sonrası yenilemede kalır).
    timeline.value = view.timeline.items;
    timelineTotal.value = view.timeline.total || view.timeline.items.length;
  } catch (e: unknown) {
    formContext.value = null;
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.profile.loadError'));
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadProfile();
});

watch(workItemId, () => {
  void loadProfile();
});
</script>

<template>
  <div class="oc-flow oc-profile-page">
    <BaseBreadcrumb :title="pageTitle" :breadcrumbs="breadcrumbs" />

    <v-card variant="outlined" class="rounded-lg mb-4">
      <v-card-title class="d-flex align-center flex-wrap gap-2 py-3">
        <v-btn
          v-if="backToBoardTo"
          icon="mdi-arrow-left"
          variant="text"
          size="small"
          :to="backToBoardTo"
          :title="t('operationCore.board.backToBoard')"
        />
        <div class="min-width-0">
          <div class="d-flex align-center ga-2">
            <span v-if="workItemKey" class="text-caption text-primary font-weight-bold">{{ workItemKey }}</span>
            <div class="text-subtitle-1 font-weight-bold text-truncate">{{ workItemTitle }}</div>
          </div>
          <div class="text-caption text-medium-emphasis">
            {{ editMode ? t('operationCore.profile.edit.hint') : t('operationCore.profile.readonlyHint') }}
          </div>
        </div>
        <v-spacer />
        <template v-if="editMode">
          <v-chip size="small" variant="tonal" color="warning" prepend-icon="mdi-pencil" class="me-1">
            {{ t('operationCore.profile.edit.editingChip') }}
          </v-chip>
          <v-btn
            variant="text"
            size="small"
            class="text-none"
            :disabled="savingEdit"
            @click="cancelFormEdit"
          >
            {{ t('operationCore.profile.edit.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            size="small"
            rounded="lg"
            class="text-none"
            prepend-icon="mdi-content-save"
            :loading="savingEdit"
            @click="saveFormEdit"
          >
            {{ t('operationCore.profile.edit.save') }}
          </v-btn>
        </template>
        <template v-else>
          <v-btn
            v-if="canEdit"
            color="primary"
            variant="tonal"
            size="small"
            rounded="lg"
            class="text-none"
            prepend-icon="mdi-pencil"
            @click="startFormEdit"
          >
            {{ t('operationCore.profile.edit.edit') }}
          </v-btn>
          <v-chip v-else size="small" variant="tonal" color="primary" prepend-icon="mdi-lock-outline">
            {{ t('operationCore.profile.readonlyChip') }}
          </v-chip>
        </template>
      </v-card-title>
      <template v-if="transitionActions.length && !editMode">
        <v-divider />
        <v-card-text class="d-flex align-center flex-wrap ga-2 py-2">
          <span class="text-caption text-medium-emphasis me-1">
            {{ t('operationCore.profile.transitions.title') }}
          </span>
          <v-btn
            v-for="action in transitionActions"
            :key="action.transitionKey"
            size="small"
            variant="flat"
            color="primary"
            rounded="lg"
            class="text-none"
            prepend-icon="mdi-swap-horizontal"
            :disabled="!action.enabled || transitionBusy"
            @click="openTransition(action)"
          >
            {{ actionLabel(action) }}
          </v-btn>
        </v-card-text>
      </template>
    </v-card>

    <!-- Durum geçişi onay dialog'u -->
    <v-dialog v-model="transitionDialog" max-width="520" persistent>
      <v-card rounded="lg">
        <v-card-title class="text-subtitle-1 font-weight-bold d-flex align-center ga-2">
          <v-icon icon="mdi-swap-horizontal" color="primary" size="20" />
          {{ t('operationCore.profile.transitions.confirmTitle') }}
        </v-card-title>
        <v-card-text>
          <p class="text-body-2 mb-3">
            {{ t('operationCore.profile.transitions.confirmBody') }}
            <strong v-if="transitionTarget">{{ actionLabel(transitionTarget) }}</strong>
          </p>

          <template v-if="formContext && transitionRequiredKeys.length">
            <div class="d-flex align-center ga-2 mb-2">
              <v-icon icon="mdi-form-textbox" color="primary" size="16" />
              <span class="text-caption font-weight-medium">
                {{ t('operationCore.profile.transitions.requiredTitle') }}
              </span>
            </div>
            <OcTransitionRequiredFields
              v-model="transitionFieldModel"
              :context="formContext"
              :field-keys="transitionRequiredKeys"
              class="mb-1"
            />
            <v-divider class="my-3" />
          </template>

          <v-textarea
            v-model="transitionComment"
            :label="t('operationCore.profile.transitions.commentLabel')"
            :placeholder="t('operationCore.profile.transitions.commentPlaceholder')"
            variant="outlined"
            rows="2"
            auto-grow
            hide-details="auto"
            density="comfortable"
          />
          <v-alert
            v-if="transitionError"
            type="error"
            variant="tonal"
            density="compact"
            class="mt-3 rounded-lg"
          >
            {{ transitionError }}
          </v-alert>
        </v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn
            variant="text"
            class="text-none"
            :disabled="transitionBusy"
            @click="transitionDialog = false"
          >
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="transitionBusy"
            :disabled="transitionMissingRequired.length > 0"
            @click="confirmTransition"
          >
            {{ t('operationCore.profile.transitions.confirm') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="!!deleteTargetId" max-width="420" persistent>
      <v-card rounded="lg">
        <v-card-title class="text-h6">{{ t('operationCore.profile.comments.deleteTitle') }}</v-card-title>
        <v-card-text class="text-body-2">{{ t('operationCore.profile.comments.deleteConfirm') }}</v-card-text>
        <v-card-actions class="px-4 pb-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" :disabled="deleteBusy" @click="deleteTargetId = null">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" class="text-none" :loading="deleteBusy" @click="confirmDeleteComment">
            {{ t('operationCore.profile.comments.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <OcAttachmentPreviewDialog
      v-model="previewOpen"
      :attachment="previewAtt"
      @download="downloadAttachment"
    />

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4 rounded-lg" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <!-- Yüklenirken iskelet panel: boş ekran yerine form/yan panel yerleşimini taklit eder. -->
    <v-row v-if="loading" class="oc-profile-skeleton">
      <v-col cols="12" md="8">
        <v-card variant="outlined" class="rounded-lg pa-4">
          <div class="d-flex align-center ga-2 mb-2 text-medium-emphasis">
            <v-progress-circular indeterminate color="primary" size="18" width="2" />
            <span class="text-caption">{{ t('operationCore.profile.loadingHint') }}</span>
          </div>
          <div class="d-flex align-center ga-2 mb-4">
            <v-skeleton-loader type="chip" />
            <v-skeleton-loader type="chip" />
            <v-skeleton-loader type="chip" />
            <v-skeleton-loader type="chip" />
          </div>
          <v-row dense>
            <v-col v-for="n in 8" :key="n" cols="12" sm="6">
              <v-skeleton-loader type="list-item-two-line" class="rounded-lg" />
            </v-col>
          </v-row>
        </v-card>
      </v-col>
      <v-col cols="12" md="4">
        <v-card variant="outlined" class="rounded-lg pa-4 mb-4">
          <v-skeleton-loader type="heading" class="mb-3" />
          <v-skeleton-loader type="list-item-two-line, list-item-two-line" />
        </v-card>
        <v-card variant="outlined" class="rounded-lg pa-4">
          <v-skeleton-loader type="heading" class="mb-3" />
          <v-skeleton-loader type="list-item-two-line, list-item-two-line, list-item-two-line" />
        </v-card>
      </v-col>
    </v-row>

    <v-row v-if="!loading && (formContext || profile)">
      <!-- Ana kolon: tab'lar -->
      <v-col cols="12" md="8">
        <v-card variant="outlined" class="rounded-lg">
          <v-tabs v-model="activeTab" color="primary" density="comfortable" class="px-2">
            <v-tab value="details" class="text-none">
              <v-icon icon="mdi-form-select" start size="18" />
              {{ t('operationCore.profile.tabs.details') }}
            </v-tab>
            <v-tab value="comments" class="text-none">
              <v-icon icon="mdi-comment-multiple-outline" start size="18" />
              {{ t('operationCore.profile.tabs.comments') }}
              <v-chip v-if="commentCount" size="x-small" class="ml-2" color="primary" variant="tonal">
                {{ commentCount }}
              </v-chip>
            </v-tab>
            <v-tab value="activity" class="text-none">
              <v-icon icon="mdi-history" start size="18" />
              {{ t('operationCore.profile.tabs.activity') }}
              <v-chip v-if="activityEntries.length" size="x-small" class="ml-2" color="primary" variant="tonal">
                {{ activityEntries.length }}
              </v-chip>
            </v-tab>
            <v-tab value="attachments" class="text-none">
              <v-icon icon="mdi-paperclip" start size="18" />
              {{ t('operationCore.profile.tabs.attachments') }}
              <v-chip v-if="attachments.length" size="x-small" class="ml-2" color="primary" variant="tonal">
                {{ attachments.length }}
              </v-chip>
            </v-tab>
          </v-tabs>
          <v-divider />
          <v-window v-model="activeTab">
            <v-window-item value="details">
              <v-card-text class="pa-4 pa-md-5">
                <v-alert
                  v-if="editMode && editValidationAttempted && editValidationIssues.length"
                  type="warning"
                  variant="tonal"
                  class="mb-4 rounded-lg"
                  :title="t('operationCore.create.validationSummaryTitle')"
                >
                  <p class="text-body-2 mb-2">{{ t('operationCore.create.validationRequired') }}</p>
                  <ul class="pl-4 mb-0" style="list-style: disc">
                    <li v-for="issue in editValidationIssues" :key="issue.fieldKey">{{ issue.label }}</li>
                  </ul>
                </v-alert>

                <v-alert
                  v-if="editMode && editError"
                  type="error"
                  variant="tonal"
                  class="mb-4 rounded-lg"
                  closable
                  @click:close="editError = null"
                >
                  {{ editError }}
                </v-alert>

                <OcDynamicForm
                  v-if="formContext"
                  v-model="formModel"
                  :context="formContext"
                  :group-names="groupNames"
                  :field-displays="fieldDisplays"
                  :field-errors="editMode ? editFieldErrors : undefined"
                  :readonly="!editMode"
                />

                <div v-if="editMode" class="d-flex justify-end ga-2 mt-4">
                  <v-btn variant="text" class="text-none" :disabled="savingEdit" @click="cancelFormEdit">
                    {{ t('operationCore.profile.edit.cancel') }}
                  </v-btn>
                  <v-btn
                    color="primary"
                    variant="flat"
                    rounded="lg"
                    class="text-none px-6"
                    prepend-icon="mdi-content-save"
                    :loading="savingEdit"
                    @click="saveFormEdit"
                  >
                    {{ t('operationCore.profile.edit.save') }}
                  </v-btn>
                </div>
              </v-card-text>
            </v-window-item>

            <v-window-item value="comments">
              <v-card-text class="pa-4">
                <div v-if="canComment" class="mb-4">
                  <div v-if="replyingToId" class="d-flex align-center ga-2 mb-2">
                    <v-chip size="small" color="primary" variant="tonal" prepend-icon="mdi-reply">
                      {{ t('operationCore.profile.comments.replyingTo', { name: replyingToActor || '—' }) }}
                    </v-chip>
                    <v-btn size="x-small" variant="text" class="text-none" @click="cancelReply">
                      {{ t('operationCore.definitions.cancel') }}
                    </v-btn>
                  </div>
                  <client-only>
                    <OcCommentComposer
                      ref="composerRef"
                      :placeholder="t('operationCore.profile.comments.placeholder')"
                      :send-label="t('operationCore.profile.comments.send')"
                      :sending="commentSending"
                      @submit="submitComment"
                    />
                  </client-only>
                  <v-alert v-if="commentError" type="error" variant="tonal" density="compact" class="mt-2 rounded-lg">
                    {{ commentError }}
                  </v-alert>
                </div>

                <v-divider v-if="canComment" class="mb-3" />

                <div v-if="timelineLoading" class="d-flex justify-center py-6">
                  <v-progress-circular indeterminate color="primary" size="28" />
                </div>
                <div v-else-if="!commentThreads.length" class="text-body-2 text-medium-emphasis text-center py-6">
                  {{ t('operationCore.profile.comments.empty') }}
                </div>
                <div v-else class="d-flex flex-column ga-3">
                  <div v-for="thread in commentThreads" :key="thread.root.id ?? ''" class="oc-comment-thread">
                    <!-- Kök yorum -->
                    <div class="oc-comment-card">
                      <div class="d-flex align-center flex-wrap ga-2">
                        <v-avatar size="26" color="primary" variant="tonal">
                          <span class="text-caption font-weight-bold">{{ (thread.root.actor || '?').slice(0, 1).toUpperCase() }}</span>
                        </v-avatar>
                        <span class="text-body-2 font-weight-medium">{{ thread.root.actor || '—' }}</span>
                        <span v-if="thread.root.at" class="text-caption text-medium-emphasis">{{ fmtDate(thread.root.at) }}</span>
                        <span v-if="thread.root.editedAt" class="text-caption text-medium-emphasis font-italic">{{ t('operationCore.profile.comments.edited') }}</span>
                      </div>

                      <template v-if="editingId === thread.root.id">
                        <client-only>
                          <OcCommentComposer
                            class="mt-2"
                            :placeholder="t('operationCore.profile.comments.placeholder')"
                            :send-label="t('operationCore.profile.comments.save')"
                            :sending="editSending"
                            :initial-html="thread.root.text ?? ''"
                            :allow-attachments="false"
                            show-cancel
                            @submit="(p) => submitEdit(thread.root.id ?? '', p)"
                            @cancel="cancelEdit"
                          />
                        </client-only>
                      </template>
                      <template v-else>
                        <div class="oc-comment-body text-body-2 mt-1" v-html="renderCommentHtml(thread.root.text)" />
                        <div v-if="thread.root.attachments && thread.root.attachments.length" class="d-flex flex-wrap ga-2 mt-2">
                          <v-chip
                            v-for="att in thread.root.attachments"
                            :key="att.path"
                            size="small"
                            variant="outlined"
                            rounded="lg"
                            :prepend-icon="canPreview(att) ? 'mdi-file-eye-outline' : 'mdi-paperclip'"
                            link
                            @click="previewOrDownload(att)"
                          >
                            {{ att.fileName }}
                          </v-chip>
                        </div>
                        <div class="mt-1 d-flex align-center flex-wrap ga-1">
                          <v-btn
                            v-if="canComment"
                            size="x-small"
                            variant="text"
                            class="text-none"
                            prepend-icon="mdi-reply"
                            @click="startReply(thread.root)"
                          >
                            {{ t('operationCore.profile.comments.reply') }}
                          </v-btn>
                          <template v-if="canModifyComment(thread.root)">
                            <v-btn
                              size="x-small"
                              variant="text"
                              class="text-none"
                              prepend-icon="mdi-pencil"
                              @click="startEdit(thread.root)"
                            >
                              {{ t('operationCore.profile.comments.edit') }}
                            </v-btn>
                            <v-btn
                              size="x-small"
                              variant="text"
                              color="error"
                              class="text-none"
                              prepend-icon="mdi-delete-outline"
                              @click="deleteTargetId = thread.root.id ?? null"
                            >
                              {{ t('operationCore.profile.comments.delete') }}
                            </v-btn>
                          </template>
                        </div>
                      </template>
                    </div>

                    <!-- Yanıtlar (tek seviye, girintili) -->
                    <div
                      v-for="reply in thread.replies"
                      :key="reply.id ?? ''"
                      class="oc-comment-card oc-comment-reply"
                    >
                      <div class="text-caption text-medium-emphasis d-flex align-center ga-1 mb-1">
                        <v-icon icon="mdi-reply" size="13" />
                        {{ t('operationCore.profile.comments.inReplyTo', { name: thread.root.actor || '—' }) }}
                      </div>
                      <div class="d-flex align-center flex-wrap ga-2">
                        <v-avatar size="22" color="secondary" variant="tonal">
                          <span class="text-caption font-weight-bold">{{ (reply.actor || '?').slice(0, 1).toUpperCase() }}</span>
                        </v-avatar>
                        <span class="text-body-2 font-weight-medium">{{ reply.actor || '—' }}</span>
                        <span v-if="reply.at" class="text-caption text-medium-emphasis">{{ fmtDate(reply.at) }}</span>
                        <span v-if="reply.editedAt" class="text-caption text-medium-emphasis font-italic">{{ t('operationCore.profile.comments.edited') }}</span>
                      </div>

                      <template v-if="editingId === reply.id">
                        <client-only>
                          <OcCommentComposer
                            class="mt-2"
                            :placeholder="t('operationCore.profile.comments.placeholder')"
                            :send-label="t('operationCore.profile.comments.save')"
                            :sending="editSending"
                            :initial-html="reply.text ?? ''"
                            :allow-attachments="false"
                            show-cancel
                            @submit="(p) => submitEdit(reply.id ?? '', p)"
                            @cancel="cancelEdit"
                          />
                        </client-only>
                      </template>
                      <template v-else>
                        <div class="oc-comment-body text-body-2 mt-1" v-html="renderCommentHtml(reply.text)" />
                        <div v-if="reply.attachments && reply.attachments.length" class="d-flex flex-wrap ga-2 mt-2">
                          <v-chip
                            v-for="att in reply.attachments"
                            :key="att.path"
                            size="small"
                            variant="outlined"
                            rounded="lg"
                            :prepend-icon="canPreview(att) ? 'mdi-file-eye-outline' : 'mdi-paperclip'"
                            link
                            @click="previewOrDownload(att)"
                          >
                            {{ att.fileName }}
                          </v-chip>
                        </div>
                        <div v-if="canModifyComment(reply)" class="mt-1 d-flex align-center flex-wrap ga-1">
                          <v-btn
                            size="x-small"
                            variant="text"
                            class="text-none"
                            prepend-icon="mdi-pencil"
                            @click="startEdit(reply)"
                          >
                            {{ t('operationCore.profile.comments.edit') }}
                          </v-btn>
                          <v-btn
                            size="x-small"
                            variant="text"
                            color="error"
                            class="text-none"
                            prepend-icon="mdi-delete-outline"
                            @click="deleteTargetId = reply.id ?? null"
                          >
                            {{ t('operationCore.profile.comments.delete') }}
                          </v-btn>
                        </div>
                      </template>
                    </div>
                  </div>
                </div>
              </v-card-text>
            </v-window-item>

            <v-window-item value="activity">
              <v-card-text class="pa-4">
                <div v-if="timelineLoading" class="d-flex justify-center py-6">
                  <v-progress-circular indeterminate color="primary" size="28" />
                </div>
                <div v-else-if="!activityEntries.length" class="text-body-2 text-medium-emphasis text-center py-6">
                  {{ t('operationCore.profile.activity.empty') }}
                </div>
                <v-timeline v-else side="end" density="compact" align="start" truncate-line="both">
                  <v-timeline-item
                    v-for="(entry, i) in activityEntries"
                    :key="entry.id ?? i"
                    :dot-color="timelineColor(entry)"
                    size="x-small"
                  >
                    <template #icon>
                      <v-icon :icon="timelineIcon(entry)" size="14" />
                    </template>
                    <div class="d-flex align-center flex-wrap ga-2">
                      <span class="text-body-2 font-weight-medium">{{ entry.actor || '—' }}</span>
                      <span v-if="entry.at" class="text-caption text-medium-emphasis">{{ fmtDate(entry.at) }}</span>
                    </div>
                    <div v-if="entry.changes?.length" class="mt-1 d-flex flex-column ga-1">
                      <div
                        v-for="(chg, ci) in entry.changes"
                        :key="ci"
                        class="text-body-2 d-flex align-center flex-wrap ga-1"
                      >
                        <span class="font-weight-medium">{{ chg.label || chg.field }}:</span>
                        <span class="text-medium-emphasis text-decoration-line-through">{{ chg.fromDisplay || '—' }}</span>
                        <v-icon icon="mdi-arrow-right-thin" size="14" class="text-medium-emphasis" />
                        <span>{{ chg.toDisplay || '—' }}</span>
                      </div>
                    </div>
                    <div v-else class="text-body-2 mt-1" style="white-space: pre-wrap">{{ entry.text }}</div>
                  </v-timeline-item>
                </v-timeline>
              </v-card-text>
            </v-window-item>

            <v-window-item value="attachments">
              <v-card-text class="pa-4">
                <input
                  ref="fileInput"
                  type="file"
                  class="d-none"
                  @change="onFileSelected"
                />
                <div class="d-flex align-center justify-space-between mb-3">
                  <span class="text-body-2 text-medium-emphasis">
                    {{ t('operationCore.profile.attachments.hint') }}
                  </span>
                  <v-btn
                    v-if="canEdit"
                    color="primary"
                    size="small"
                    variant="flat"
                    rounded="lg"
                    class="text-none"
                    :loading="attachUploading"
                    prepend-icon="mdi-upload"
                    @click="triggerFilePick"
                  >
                    {{ t('operationCore.profile.attachments.add') }}
                  </v-btn>
                </div>

                <v-alert v-if="attachError" type="error" variant="tonal" density="compact" class="mb-3 rounded-lg">
                  {{ attachError }}
                </v-alert>

                <div v-if="!attachments.length" class="text-body-2 text-medium-emphasis text-center py-6">
                  {{ t('operationCore.profile.attachments.empty') }}
                </div>
                <v-list v-else class="py-0" density="comfortable">
                  <v-list-item
                    v-for="att in attachments"
                    :key="att.path"
                    class="px-2 rounded-lg oc-attach-row"
                  >
                    <template #prepend>
                      <v-icon :icon="attachmentIcon(att)" color="primary" />
                    </template>
                    <v-list-item-title
                      class="text-body-2 font-weight-medium text-truncate"
                      :class="{ 'oc-attach-name-link': canPreview(att) }"
                      @click="canPreview(att) && openPreview(att)"
                    >
                      {{ att.fileName }}
                    </v-list-item-title>
                    <v-list-item-subtitle class="text-caption">
                      <span v-if="att.fileSizeKb">{{ fmtFileSize(att.fileSizeKb) }}</span>
                      <span v-if="att.uploadTime"> · {{ fmtDate(att.uploadTime) }}</span>
                      <span v-if="att.uploadPerson"> · {{ att.uploadPerson }}</span>
                    </v-list-item-subtitle>
                    <template #append>
                      <v-btn
                        v-if="canPreview(att)"
                        icon="mdi-eye-outline"
                        variant="text"
                        size="small"
                        :title="t('operationCore.profile.attachments.preview')"
                        @click="openPreview(att)"
                      />
                      <v-btn
                        icon="mdi-download"
                        variant="text"
                        size="small"
                        :title="t('operationCore.profile.attachments.download')"
                        @click="downloadAttachment(att)"
                      />
                      <v-btn
                        v-if="canEdit"
                        icon="mdi-delete-outline"
                        variant="text"
                        size="small"
                        color="error"
                        :loading="removingPath === att.path"
                        :title="t('operationCore.profile.attachments.remove')"
                        @click="removeAttachment(att)"
                      />
                    </template>
                  </v-list-item>
                </v-list>
              </v-card-text>
            </v-window-item>
          </v-window>
        </v-card>
      </v-col>

      <!-- Sidebar: SLA + meta -->
      <v-col cols="12" md="4">
        <!-- SLA panel -->
        <v-card v-if="summary" variant="outlined" class="rounded-lg mb-4">
          <v-card-text class="pa-4">
            <div class="d-flex align-center justify-space-between mb-2">
              <span class="text-subtitle-2 font-weight-bold">{{ t('operationCore.profile.sla.title') }}</span>
              <OcSlaStatusChip
                :sla="profile?.sla"
                :state-id="summary.stateId"
                :closed-at="summary.closedAt"
              />
            </div>
            <div v-if="profile?.sla?.responseDueAt || profile?.sla?.resolveDueAt" class="oc-meta-list">
              <div v-if="profile?.sla?.responseDueAt" class="oc-meta-row">
                <span class="oc-meta-label">{{ t('operationCore.profile.sla.responseDue') }}</span>
                <span class="oc-meta-value" :class="{ 'text-error': profile?.sla?.responseBreached }">
                  {{ fmtDate(profile?.sla?.responseDueAt) }}
                </span>
              </div>
              <div v-if="profile?.sla?.resolveDueAt" class="oc-meta-row">
                <span class="oc-meta-label">{{ t('operationCore.profile.sla.resolveDue') }}</span>
                <span class="oc-meta-value" :class="{ 'text-error': profile?.sla?.resolveBreached }">
                  {{ fmtDate(profile?.sla?.resolveDueAt) }}
                </span>
              </div>
            </div>
            <div v-else class="text-caption text-medium-emphasis">
              {{ t('operationCore.profile.sla.none') }}
            </div>
          </v-card-text>
        </v-card>

        <!-- Politikalar (SLA politikası + uygulanan kurallar) -->
        <v-card v-if="summary" variant="outlined" class="rounded-lg mb-4">
          <v-card-text class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-3">{{ t('operationCore.policies.title') }}</div>
            <OcPolicyPanel
              :workspace-id="profile?.workspaceId"
              :type-id="summary.typeId"
              :priority-id="summary.priorityId"
              :board-id="summary.boardId"
              :state-id="summary.stateId"
              :sla-policy-id="profile?.sla?.slaPolicyId"
              :resolved-policy="resolvedPolicy"
            />
          </v-card-text>
        </v-card>

        <!-- Meta -->
        <v-card v-if="summary" variant="outlined" class="rounded-lg mb-4">
          <v-card-text class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-3">{{ t('operationCore.profile.meta.title') }}</div>
            <div class="oc-meta-list">
              <div class="oc-meta-row">
                <span class="oc-meta-label">{{ t('operationCore.workspaceDefinitions.boards.listTableColumns.stateId') }}</span>
                <OcBoardCatalogLabel :item="resolveState(summary.stateId, null)" />
              </div>
              <div class="oc-meta-row">
                <span class="oc-meta-label">{{ t('operationCore.workspaceDefinitions.boards.listTableColumns.priorityId') }}</span>
                <OcBoardCatalogLabel :item="resolvePriority(summary.priorityId)" />
              </div>
              <div class="oc-meta-row">
                <span class="oc-meta-label">{{ t('operationCore.workspaceDefinitions.boards.listTableColumns.typeId') }}</span>
                <OcBoardCatalogLabel :item="resolveType(summary.typeId)" />
              </div>
              <div class="oc-meta-row">
                <span class="oc-meta-label">{{ t('operationCore.workspaceDefinitions.boards.listTableColumns.assignee') }}</span>
                <span class="oc-meta-value">{{ resolvePersonName(summary.assignee) }}</span>
              </div>
              <div class="oc-meta-row">
                <span class="oc-meta-label">{{ t('operationCore.workspaceDefinitions.boards.listTableColumns.createdBy') }}</span>
                <span class="oc-meta-value">{{ resolvePersonName(profile?.createdBy) }}</span>
              </div>
              <div class="oc-meta-row">
                <span class="oc-meta-label">{{ t('operationCore.workspaceDefinitions.boards.listTableColumns.createdAt') }}</span>
                <span class="oc-meta-value">{{ fmtDate(summary.createdAt) }}</span>
              </div>
              <div class="oc-meta-row">
                <span class="oc-meta-label">{{ t('operationCore.workspaceDefinitions.boards.listTableColumns.age') }}</span>
                <span class="oc-meta-value">{{ fmtAge(summary.createdAt, summary.closedAt) }}</span>
              </div>
              <div v-if="summary.closedAt" class="oc-meta-row">
                <span class="oc-meta-label">{{ t('operationCore.workspaceDefinitions.boards.listTableColumns.closedAt') }}</span>
                <span class="oc-meta-value">{{ fmtDate(summary.closedAt) }}</span>
              </div>
            </div>
          </v-card-text>
        </v-card>

        <!-- Watchers -->
        <v-card v-if="watcherNames.length" variant="outlined" class="rounded-lg mb-4">
          <v-card-text class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-2">{{ t('operationCore.profile.watchers.title') }}</div>
            <div class="d-flex flex-wrap ga-2">
              <v-chip v-for="w in watcherNames" :key="w.id" size="small" variant="tonal" prepend-icon="mdi-account">
                {{ w.name }}
              </v-chip>
            </div>
          </v-card-text>
        </v-card>

        <!-- Links -->
        <v-card v-if="profile?.links?.length" variant="outlined" class="rounded-lg">
          <v-card-text class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-2">{{ t('operationCore.profile.links.title') }}</div>
            <div v-for="link in profile.links" :key="link.id" class="oc-meta-row">
              <span class="oc-meta-label">{{ link.linkType }}</span>
              <NuxtLink
                :to="`/apps/operation-core/work-items/${encodeURIComponent(link.otherWorkItemId)}/profile`"
                class="text-primary text-decoration-none oc-meta-value"
              >
                {{ link.otherWorkItemId }}
              </NuxtLink>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<style scoped>
.min-width-0 {
  min-width: 0;
}

.oc-profile-skeleton :deep(.v-skeleton-loader) {
  background: transparent;
}
.oc-profile-skeleton :deep(.v-skeleton-loader__chip) {
  width: 96px;
}

.oc-meta-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.oc-meta-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.oc-meta-label {
  font-size: 0.8125rem;
  color: rgba(var(--v-theme-on-surface), 0.6);
  flex-shrink: 0;
}

.oc-meta-value {
  font-size: 0.875rem;
  text-align: right;
  min-width: 0;
}

.oc-attach-name-link {
  cursor: pointer;
}

.oc-attach-name-link:hover {
  color: rgb(var(--v-theme-primary));
  text-decoration: underline;
}

.oc-comment-card {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 12px;
  padding: 10px 12px;
}

.oc-comment-reply {
  margin-left: 2rem;
  margin-top: 0.5rem;
  background: rgba(var(--v-theme-on-surface), 0.03);
}

.oc-comment-body :deep(p) {
  margin: 0 0 0.4em;
}

.oc-comment-body :deep(p:last-child) {
  margin-bottom: 0;
}

.oc-comment-body :deep(ul),
.oc-comment-body :deep(ol) {
  padding-left: 1.25rem;
  margin: 0.25em 0;
}

.oc-comment-body :deep(blockquote) {
  border-left: 3px solid rgba(var(--v-theme-primary), 0.4);
  padding-left: 0.75rem;
  margin: 0.4em 0;
  color: rgba(var(--v-theme-on-surface), 0.75);
}

.oc-comment-body :deep(code) {
  background: rgba(var(--v-theme-on-surface), 0.08);
  padding: 0.1em 0.35em;
  border-radius: 4px;
  font-size: 0.85em;
}
</style>
