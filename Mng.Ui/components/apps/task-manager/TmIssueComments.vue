<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue';
import type { TmIssueComment } from '@/types/apps/taskManager';
import { useTaskManagerStore } from '@/stores/apps/taskManager';
import { useUserStore } from '@/stores/apps/user';
import { assigneeUserId, assigneeDisplayLabel } from '@/composables/useTaskManagerHelpers';
import {
  flattenCommentsWithDepth,
  mentionTokenForUserId,
  parseCommentBodySegments,
} from '@/utils/taskManagerIssueComments';

const props = defineProps<{
  issueId: string;
  projectId: string;
  /** Oturum kullanıcı kimliği (Keycloak `sub`); yoksa yorum yazılamaz */
  currentUserId: string;
  isManager?: boolean;
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

const store = useTaskManagerStore();
const userStore = useUserStore();

const composerBody = ref('');
const composerRef = ref<{ $el?: HTMLElement } | null>(null);
const replyingTo = ref<TmIssueComment | null>(null);
const submitting = ref(false);
const loadError = ref(false);
const sendError = ref(false);

const mentionMenu = ref(false);
const mentionUsers = computed(() => {
  const q = mentionQuery.value.trim().toLowerCase();
  const list = userStore.activeUsers ?? [];
  if (!q) return list.slice(0, 20);
  return list
    .filter((u) => {
      const id = (u.id || u.userId || '').toLowerCase();
      const name = `${u.firstName ?? ''} ${u.lastName ?? ''} ${u.username ?? ''}`.toLowerCase();
      return id.includes(q) || name.includes(q);
    })
    .slice(0, 20);
});
const mentionQuery = ref('');

const COMMON_EMOJIS = ['👍', '👎', '❤️', '🎉', '🔥', '✅', '👀', '💡', '🙏', '😀', '😅', '🤔'];

const rows = computed(() => {
  const list = store.commentsByIssueId[props.issueId] ?? [];
  return flattenCommentsWithDepth(list);
});

const editDialog = ref(false);
const editBody = ref('');
const editingComment = ref<TmIssueComment | null>(null);
const savingEdit = ref(false);

const deleteDialog = ref(false);
const deletingComment = ref<TmIssueComment | null>(null);
const deleting = ref(false);

function getComposerTextarea(): HTMLTextAreaElement | null {
  const el = composerRef.value?.$el;
  if (!el) return null;
  return el.querySelector('textarea');
}

/** Keeper `id` / `userId` veya Keycloak `sub` (keycloakUserId) ile eşleşir — `activeUsers` yalnızca id eşitlediği için yetmez */
function userLookup(id: string) {
  return userStore.getUserById(id) ?? null;
}

/** @mention ve yazar satırı — DG `persons` içindeki adları da kullanır */
function displayNameForUser(userId: string): string {
  return assigneeDisplayLabel(userId, userLookup);
}

function authorLabel(c: TmIssueComment): string {
  return assigneeDisplayLabel(c.author, userLookup);
}

function canModify(c: TmIssueComment): boolean {
  const aid = assigneeUserId(c.author);
  const sub = String(props.currentUserId ?? '').trim();
  if (sub && aid === sub) return true;
  if (sub && aid) {
    const me = userStore.getUserById(sub);
    const keeperId = String(me?.id || me?.userId || '').trim();
    if (keeperId && aid === keeperId) return true;
  }
  return !!props.isManager;
}

function onComposerKeyup() {
  const ta = getComposerTextarea();
  if (!ta) return;
  const pos = ta.selectionStart ?? 0;
  const text = composerBody.value;
  const before = text.slice(0, pos);
  const at = before.lastIndexOf('@');
  if (at < 0) {
    mentionMenu.value = false;
    return;
  }
  const frag = before.slice(at + 1);
  if (frag.includes(']') || /^\s/.test(frag)) {
    mentionMenu.value = false;
    return;
  }
  mentionQuery.value = frag;
  mentionMenu.value = mentionUsers.value.length > 0;
}

function insertMention(userId: string) {
  const ta = getComposerTextarea();
  const token = mentionTokenForUserId(userId);
  if (!ta || !token) {
    mentionMenu.value = false;
    return;
  }
  const pos = ta.selectionStart ?? composerBody.value.length;
  const text = composerBody.value;
  const before = text.slice(0, pos);
  const after = text.slice(pos);
  const at = before.lastIndexOf('@');
  const newBefore = at < 0 ? before + token + ' ' : before.slice(0, at) + token + ' ';
  composerBody.value = newBefore + after;
  mentionMenu.value = false;
  const cursorPos = newBefore.length;
  nextTick(() => {
    const t2 = getComposerTextarea();
    if (!t2) return;
    t2.focus();
    t2.setSelectionRange(cursorPos, cursorPos);
  });
}

function appendEmoji(ch: string) {
  composerBody.value += ch;
}

async function refresh() {
  if (!props.issueId) return;
  loadError.value = false;
  try {
    await store.loadIssueComments(props.issueId);
  } catch {
    loadError.value = true;
  }
}

watch(
  () => props.issueId,
  (id) => {
    if (id) void refresh();
  },
  { immediate: true }
);

async function submit() {
  if (!props.currentUserId || !composerBody.value.trim()) return;
  submitting.value = true;
  sendError.value = false;
  try {
    await store.createIssueComment({
      issueId: props.issueId,
      projectId: props.projectId,
      authorId: props.currentUserId,
      body: composerBody.value,
      parentCommentId: replyingTo.value?.__dataId ?? undefined,
    });
    composerBody.value = '';
    replyingTo.value = null;
  } catch {
    sendError.value = true;
  } finally {
    submitting.value = false;
  }
}

function startReply(c: TmIssueComment) {
  replyingTo.value = c;
  nextTick(() => getComposerTextarea()?.focus());
}

function openEdit(c: TmIssueComment) {
  editingComment.value = c;
  editBody.value = c.body;
  editDialog.value = true;
}

async function saveEdit() {
  if (!editingComment.value || !editBody.value.trim()) return;
  savingEdit.value = true;
  try {
    await store.updateIssueComment(props.issueId, editingComment.value.__dataId, editBody.value);
    editDialog.value = false;
    editingComment.value = null;
  } finally {
    savingEdit.value = false;
  }
}

function confirmDelete(c: TmIssueComment) {
  deletingComment.value = c;
  deleteDialog.value = true;
}

async function doDelete() {
  if (!deletingComment.value) return;
  deleting.value = true;
  try {
    await store.deleteIssueComment(props.issueId, deletingComment.value.__dataId);
    deleteDialog.value = false;
    deletingComment.value = null;
  } finally {
    deleting.value = false;
  }
}

function formatWhen(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return d.toLocaleString();
}
</script>

<template>
  <v-card class="tm-panel pa-2 pa-md-4" rounded="xl" flat>
    <v-card-title class="text-h6 font-weight-medium px-0 pt-0">
      {{ mt('taskManager.issueCommentsTitle', 'Yorumlar') }}
    </v-card-title>

    <v-alert v-if="loadError" type="warning" variant="tonal" density="compact" class="mb-2">
      {{ mt('taskManager.issueCommentsLoadError', 'Yorumlar yüklenemedi.') }}
    </v-alert>
    <v-alert v-else-if="!currentUserId" type="info" variant="tonal" density="compact" class="mb-2">
      {{ mt('taskManager.issueCommentsLoginHint', 'Yorum yazmak için oturum açın.') }}
    </v-alert>

    <div v-if="!rows.length && !loadError" class="text-body-2 text-medium-emphasis mb-4">
      {{ mt('taskManager.issueCommentsEmpty', 'Henüz yorum yok.') }}
    </div>

    <div v-for="{ comment: c, depth } in rows" :key="c.__dataId" class="mb-3" :style="{ marginInlineStart: `${Math.min(depth, 8) * 16}px` }">
      <v-sheet border rounded="lg" class="pa-3 bg-surface">
        <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-1">
          <div class="text-body-2 font-weight-medium">{{ authorLabel(c) }}</div>
          <div class="text-caption text-medium-emphasis">
            {{ formatWhen(c.createdAt) }}
            <span v-if="c.updatedAt && c.updatedAt !== c.createdAt"> · {{ mt('taskManager.issueCommentsEdited', 'düzenlendi') }}</span>
          </div>
        </div>
        <div class="text-body-2 tm-issue-comment-body">
          <span
            v-for="(seg, si) in parseCommentBodySegments(c.body)"
            :key="`${c.__dataId}-${si}`"
            class="tm-issue-comment-seg"
          >
            <span v-if="seg.type === 'text'" class="tm-issue-comment-text">{{ seg.text }}</span>
            <v-chip v-else size="x-small" variant="tonal" color="primary" class="mx-0 my-0 align-middle">
              @{{ displayNameForUser(seg.userId) }}
            </v-chip>
          </span>
        </div>
        <div class="d-flex flex-wrap gap-1 mt-2">
          <v-btn size="small" variant="text" class="text-none" :disabled="!currentUserId" @click="startReply(c)">
            {{ mt('taskManager.issueCommentsReply', 'Yanıtla') }}
          </v-btn>
          <template v-if="canModify(c)">
            <v-btn size="small" variant="text" class="text-none" @click="openEdit(c)">
              {{ mt('taskManager.issueCommentsEdit', 'Düzenle') }}
            </v-btn>
            <v-btn size="small" variant="text" color="error" class="text-none" @click="confirmDelete(c)">
              {{ mt('taskManager.issueCommentsDelete', 'Sil') }}
            </v-btn>
          </template>
        </div>
      </v-sheet>
    </div>

    <v-divider class="my-4" />

    <div v-if="replyingTo" class="d-flex align-center gap-2 mb-2">
      <v-chip size="small" closable @click:close="replyingTo = null">
        {{ mt('taskManager.issueCommentsReplyingTo', 'Yanıt:') }} {{ authorLabel(replyingTo) }}
      </v-chip>
    </div>

    <v-alert v-if="sendError" type="error" variant="tonal" density="compact" class="mb-2">
      {{ mt('taskManager.issueCommentsSendError', 'Yorum gönderilemedi.') }}
    </v-alert>

    <div class="d-flex flex-wrap align-center gap-1 mb-2">
      <span class="text-caption text-medium-emphasis me-2">{{ mt('taskManager.issueCommentsMention', 'Kişi ekle') }}</span>
      <v-menu location="bottom">
        <template #activator="{ props: menuProps }">
          <v-btn v-bind="menuProps" size="small" variant="outlined" class="text-none">
            @
          </v-btn>
        </template>
        <v-list density="compact" max-height="280" class="overflow-y-auto" style="min-width: 220px">
          <v-list-item
            v-for="u in userStore.activeUsers.slice(0, 40)"
            :key="u.id || u.userId"
            :title="`${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username || u.id"
            :subtitle="u.id || u.userId"
            @click="insertMention(String(u.id || u.userId || ''))"
          />
        </v-list>
      </v-menu>
      <span class="text-caption text-medium-emphasis ms-3 me-2">{{ mt('taskManager.issueCommentsEmoji', 'Emoji') }}</span>
      <v-btn v-for="em in COMMON_EMOJIS" :key="em" size="x-small" variant="text" class="px-1" @click="appendEmoji(em)">{{ em }}</v-btn>
    </div>

    <div class="position-relative mb-2">
      <v-textarea
        ref="composerRef"
        v-model="composerBody"
        :disabled="!currentUserId"
        :placeholder="mt('taskManager.issueCommentsPlaceholder', 'Yorum yazın… @ ile kişi ekleyebilirsiniz.')"
        rows="3"
        variant="outlined"
        density="comfortable"
        hide-details="auto"
        @keyup="onComposerKeyup"
      />
      <v-card
        v-show="mentionMenu && mentionUsers.length"
        class="tm-mention-flyout pa-0"
        elevation="6"
        rounded="lg"
      >
        <v-list density="compact" max-height="220" class="overflow-y-auto py-0">
          <v-list-item
            v-for="u in mentionUsers"
            :key="u.id || u.userId"
            :title="`${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.username"
            :subtitle="u.id || u.userId"
            @click="insertMention(String(u.id || u.userId || ''))"
          />
        </v-list>
      </v-card>
    </div>

    <v-btn color="primary" rounded="lg" class="text-none" :loading="submitting" :disabled="!currentUserId || !composerBody.trim()" @click="submit">
      {{ mt('taskManager.issueCommentsSubmit', 'Gönder') }}
    </v-btn>

    <v-dialog v-model="editDialog" max-width="520">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.issueCommentsEdit', 'Düzenle') }}</v-card-title>
        <v-card-text>
          <v-textarea v-model="editBody" rows="5" variant="outlined" hide-details="auto" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="editDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="primary" :loading="savingEdit" :disabled="!editBody.trim()" @click="saveEdit">
            {{ mt('taskManager.issueCommentsSaveEdit', 'Kaydet') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card rounded="xl">
        <v-card-title>{{ mt('taskManager.issueCommentsDeleteConfirmTitle', 'Yorum silinsin mi?') }}</v-card-title>
        <v-card-text>{{ mt('taskManager.issueCommentsDeleteConfirmBody', 'Bu işlem geri alınamaz.') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ mt('taskManager.cancel', 'İptal') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="doDelete">{{ mt('taskManager.delete', 'Sil') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-card>
</template>

<style scoped>
.tm-issue-comment-body {
  white-space: pre-wrap;
  word-break: break-word;
}
.tm-issue-comment-seg {
  display: inline;
}
.tm-issue-comment-text {
  white-space: pre-wrap;
}
.tm-mention-flyout {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 100%;
  margin-bottom: 4px;
  z-index: 20;
  min-width: 240px;
}
</style>
