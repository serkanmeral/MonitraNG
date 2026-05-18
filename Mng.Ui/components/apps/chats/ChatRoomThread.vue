<script setup lang="ts">
import { computed, ref, watch, nextTick } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { formatDistanceToNowStrict } from 'date-fns';
import { SendIcon } from 'vue-tabler-icons';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import { useChatRoomWorkspaceStore } from '@/stores/apps/chatRoomWorkspace';
import { displayNameForStoredPersonId, sessionMessageAuthorAliasSet } from '@/utils/chatRoomPersonId';

const { t } = useAppI18n();
const store = useChatRoomWorkspaceStore();
const auth = useAuthStore();
const userStore = useUserStore();

const draft = ref('');
const scrollEl = ref<HTMLElement | null>(null);

/** Auth / kullanıcı listesi / DM id değişince yeniden hesaplanır — ilk giriş ve hub sonrası sağ/sol hizası için gerekli */
const myMessageAuthorIds = computed(() => {
  void auth.userInfo;
  void userStore.users;
  void userStore.currentUser;
  void userStore.viewingUser;
  const dm = String(store.myDmParticipantId ?? '').trim();
  return sessionMessageAuthorAliasSet(dm ? [dm] : []);
});

function isMine(authorPersonId: string) {
  const a = String(authorPersonId ?? '').trim().toLowerCase();
  return myMessageAuthorIds.value.has(a);
}

const hasMessagesMissingAuthor = computed(() =>
  store.messages.some((m) => !String(m.authorPersonId ?? '').trim())
);

/** Başlık: seçim anındaki `title` donuk kalmasın; Keeper profilleri geldikçe `directTitle` / `groupTitle` yenilensin. */
const threadHeaderTitle = computed(() => {
  const s = store.selection;
  if (!s) return '';
  void userStore.users;
  void userStore.currentUser;
  void userStore.viewingUser;
  if (s.roomKind === 'direct') {
    const row = store.directForMe.find((r) => r.dataId === s.roomRecordId);
    if (row) return store.directTitle(row);
  }
  if (s.roomKind === 'group') {
    const row = store.groupsAll.find((r) => r.dataId === s.roomRecordId);
    if (row) return store.groupTitle(row);
  }
  return s.title;
});

function authorDisplayLabel(authorPersonId: string) {
  const raw = String(authorPersonId ?? '').trim();
  if (!raw) return t('chatRoom.waUnknownAuthor');
  return displayNameForStoredPersonId(raw);
}

function authorAvatarLetters(authorPersonId: string) {
  const raw = String(authorPersonId ?? '').trim();
  if (!raw) return '?';
  const dn = authorDisplayLabel(authorPersonId);
  if (dn && dn !== t('chatRoom.waUnknownAuthor')) {
    const parts = dn.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) return (parts[0].slice(0, 1) + parts[1].slice(0, 1)).toUpperCase();
    return dn.slice(0, 2).toUpperCase();
  }
  return raw.slice(0, 2).toUpperCase();
}

function relTime(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  try {
    return formatDistanceToNowStrict(d, { addSuffix: true });
  } catch {
    return iso;
  }
}

async function scrollToBottom() {
  await nextTick();
  const el = scrollEl.value;
  if (el) el.scrollTop = el.scrollHeight;
}

watch(
  () => store.messages.length,
  () => {
    void scrollToBottom();
  }
);

/** Hub ile aynı uzunlukta güncelleme (son mesaj değişimi) — aşağı kaydır */
watch(
  () => store.messages.at(-1)?.dataId,
  () => {
    void scrollToBottom();
  }
);

watch(
  () => store.selection?.roomRecordId,
  () => {
    void scrollToBottom();
  }
);

async function submit() {
  const text = draft.value.trim();
  if (!text || store.sendBusy) return;
  try {
    await store.sendMessage(text);
    draft.value = '';
    await scrollToBottom();
  } catch {
    /* store.messagesError doldurulur */
  }
}
</script>

<template>
  <div v-if="!store.selection" class="customHeight chat-room-thread d-flex flex-column align-center justify-center pa-8">
    <v-icon size="56" color="primary" class="mb-3" icon="mdi-chat-outline" />
    <p class="text-body-1 text-medium-emphasis text-center">{{ t('chatRoom.waPickRoom') }}</p>
  </div>

  <div v-else class="customHeight chat-room-thread d-flex flex-column" style="min-height: 0">
    <div class="d-flex align-center gap-3 pa-4 flex-shrink-0">
      <v-avatar color="primary" size="44" class="text-white">
        <span class="text-subtitle-2">{{ threadHeaderTitle.slice(0, 2).toUpperCase() }}</span>
      </v-avatar>
      <div class="min-width-0">
        <h5 class="text-h5 mb-n1 text-truncate">{{ threadHeaderTitle }}</h5>
        <small v-if="store.selection.subtitle" class="textPrimary text-truncate d-block">{{ store.selection.subtitle }}</small>
        <small class="text-caption text-medium-emphasis d-block mt-1">{{ t('chatRoom.waHistoryLiveHint') }}</small>
      </div>
      <v-spacer />
      <v-btn icon variant="text" class="text-medium-emphasis" :title="t('chatRoom.waReloadMessages')" @click="store.loadMessages()">
        <v-icon icon="mdi-refresh" />
      </v-btn>
    </div>
    <v-divider />

    <v-alert v-if="store.messagesError" type="error" density="compact" variant="tonal" class="ma-3 flex-shrink-0">
      {{ store.messagesError }}
    </v-alert>
    <v-alert v-else-if="hasMessagesMissingAuthor" type="info" density="compact" variant="tonal" class="ma-3 flex-shrink-0">
      {{ t('chatRoom.waMissingAuthorHint') }}
    </v-alert>

    <div ref="scrollEl" class="chat-room-messages-scroll rightpartHeight flex-grow-1 pa-3">
      <div v-if="store.messagesLoading" class="d-flex justify-center py-8">
        <v-progress-circular indeterminate color="primary" size="32" />
      </div>
      <template v-else>
        <p v-if="!store.messages.length" class="text-caption text-medium-emphasis text-center py-8">
          {{ t('chatRoom.waNoMessages') }}
        </p>
        <div v-for="m in store.messages" :key="m.dataId" class="chat-message-row mb-3">
          <div v-if="isMine(m.authorPersonId)" class="d-flex justify-end w-100 text-end mb-1">
            <div class="mw-chat-bubble d-flex flex-column align-end">
              <small class="text-medium-emphasis text-subtitle-2 d-block mb-1">{{ t('chatRoom.waYou') }} · {{ relTime(m.createdAt) }}</small>
              <v-sheet class="bg-grey100 rounded-md px-3 py-2 mb-1">
                <p class="text-body-1 text-pre-wrap mb-0 text-start">{{ m.body }}</p>
              </v-sheet>
            </div>
          </div>
          <div v-else class="d-flex align-start gap-2 w-100 mb-1">
            <v-avatar color="surface-variant" size="36" class="flex-shrink-0">
              <span class="text-caption">{{ authorAvatarLetters(m.authorPersonId) }}</span>
            </v-avatar>
            <div class="mw-chat-bubble">
              <small class="text-medium-emphasis text-subtitle-2 d-block mb-1">
                {{ authorDisplayLabel(m.authorPersonId) }}
                · {{ relTime(m.createdAt) }}
              </small>
              <v-sheet class="bg-grey100 rounded-md px-3 py-2 mb-1">
                <p class="text-body-1 text-pre-wrap mb-0">{{ m.body }}</p>
              </v-sheet>
            </div>
          </div>
        </div>
      </template>
    </div>

    <v-divider class="flex-shrink-0" />
    <form class="d-flex align-center pa-3 gap-2 flex-shrink-0" @submit.prevent="submit">
      <v-text-field
        v-model="draft"
        variant="solo-filled"
        flat
        hide-details
        density="comfortable"
        :placeholder="t('chatRoom.waMessagePlaceholder')"
        :disabled="store.sendBusy"
        class="shadow-none flex-grow-1"
        @keydown.enter.exact.prevent="submit"
      />
      <v-btn
        icon
        color="primary"
        type="submit"
        variant="flat"
        :loading="store.sendBusy"
        :disabled="!draft.trim()"
      >
        <SendIcon size="20" />
      </v-btn>
    </form>
  </div>
</template>

<style scoped>
.chat-message-row {
  width: 100%;
}

.mw-chat-bubble {
  max-width: min(560px, 85%);
}
</style>

<style>
.chat-room-thread .shadow-none .v-field--no-label {
  --v-field-padding-top: 4px;
}
</style>
