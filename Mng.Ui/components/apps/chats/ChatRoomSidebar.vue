<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';
import { useChatRoomWorkspaceStore } from '@/stores/apps/chatRoomWorkspace';
import { useUserStore } from '@/stores/apps/user';
import type { ChtDirectConversationVm, ChtGroupChatVm, ChtTopicRoomVm } from '@/types/apps/chatRoom';
import { RefreshIcon } from 'vue-tabler-icons';
import ChatRoomNewDmDialog from '@/components/apps/chats/ChatRoomNewDmDialog.vue';

const { t } = useAppI18n();
const store = useChatRoomWorkspaceStore();
const authStore = useAuthStore();
const userStore = useUserStore();

const newDmOpen = ref(false);

const q = ref('');
const normQ = computed(() => q.value.trim().toLowerCase());

const topicTitleById = computed(() => {
  const m: Record<string, string> = {};
  for (const row of store.topicsAll) {
    m[row.dataId] = row.title;
  }
  return m;
});

function matches(s: string) {
  if (!normQ.value) return true;
  return s.toLowerCase().includes(normQ.value);
}

const filteredDm = computed(() => {
  void userStore.users;
  void userStore.viewingUser;
  void userStore.currentUser;
  return store.directForMe.filter((r) => matches(store.directTitle(r)));
});

const filteredRoots = computed(() => store.topicRoots.filter((r) => matches(r.title)));

const filteredBranches = computed(() =>
  store.topicBranches.filter((r) => {
    const ps = r.parentTopicRoomId ? topicTitleById.value[r.parentTopicRoomId] ?? '' : '';
    return matches(r.title) || matches(ps);
  })
);

const filteredGroups = computed(() => {
  void authStore.userInfo;
  void authStore.userGroups;
  return store.groupsAll.filter((r) => matches(store.groupTitle(r)));
});

onMounted(async () => {
  const sub = String(authStore.userInfo?.sub ?? '').trim();
  if (sub) {
    try {
      if (!userStore.getUserById(sub)) {
        await userStore.fetchUserById(sub);
      }
    } catch {
      /* DM katılımcı id’si için profil (JWT mng_person_id yoksa User.id) */
    }
  }
  try {
    await store.loadSidebar();
  } catch (e) {
    console.error('[ChatRoomSidebar] loadSidebar', e);
  }
});

function pickDm(row: ChtDirectConversationVm) {
  store.selectRoom({
    roomKind: 'direct',
    roomRecordId: row.dataId,
    title: store.directTitle(row),
    subtitle: t('chatRoom.waSectionDm'),
  });
}

function pickTopic(row: ChtTopicRoomVm, isBranch: boolean) {
  const subtitle = isBranch
    ? `${t('chatRoom.waSectionBranches')}${
        row.parentTopicRoomId
          ? ` · ${topicTitleById.value[row.parentTopicRoomId] ?? '…'}`
          : ''
      }`
    : t('chatRoom.waSectionTopics');
  store.selectRoom({
    roomKind: 'topic',
    roomRecordId: row.dataId,
    title: row.title,
    subtitle,
  });
}

function pickGroup(row: ChtGroupChatVm) {
  store.selectRoom({
    roomKind: 'group',
    roomRecordId: row.dataId,
    title: store.groupTitle(row),
    subtitle: t('chatRoom.waSectionGroups'),
  });
}

function isActive(kind: string, id: string) {
  const s = store.selection;
  return !!s && s.roomKind === kind && s.roomRecordId === id;
}
</script>

<template>
  <div class="chat-room-sidebar-inner d-flex flex-column flex-grow-1" style="min-height: 0">
    <div class="px-3 pt-2 pb-2 d-flex align-center gap-2">
      <v-text-field
        v-model="q"
        density="compact"
        variant="solo-filled"
        flat
        hide-details
        clearable
        :placeholder="t('chatRoom.waSearchPlaceholder')"
        prepend-inner-icon="mdi-magnify"
        class="flex-grow-1"
      />
      <v-btn
        icon
        variant="tonal"
        size="small"
        :loading="store.sidebarLoading"
        :title="t('chatRoom.waRefreshRooms')"
        @click="store.loadSidebar()"
      >
        <RefreshIcon size="18" />
      </v-btn>
    </div>

    <v-alert v-if="store.sidebarError" type="error" density="compact" variant="tonal" class="mx-3 mb-2">
      {{ store.sidebarError }}
    </v-alert>

    <div class="flex-grow-1 overflow-y-auto px-1 pb-2">
      <v-skeleton-loader v-if="store.sidebarLoading && !store.topicsAll.length" type="list-item@6" />

      <v-expansion-panels v-else multiple variant="accordion" class="chat-room-wa-panels">
        <v-expansion-panel>
          <v-expansion-panel-title class="text-body-2 font-weight-medium d-flex align-center pr-2">
            <span class="flex-grow-1">{{ t('chatRoom.waSectionDm') }}</span>
            <v-btn
              size="x-small"
              variant="tonal"
              color="primary"
              class="flex-shrink-0"
              @click.stop="newDmOpen = true"
            >
              {{ t('chatRoom.waNewDmButton') }}
            </v-btn>
          </v-expansion-panel-title>
          <v-expansion-panel-text class="pa-0">
            <v-list density="compact" class="bg-transparent">
              <v-list-item v-if="!filteredDm.length" class="text-caption text-medium-emphasis">
                {{ t('chatRoom.waNoDm') }}
              </v-list-item>
              <v-list-item
                v-for="row in filteredDm"
                :key="`dm-${row.dataId}`"
                :active="isActive('direct', row.dataId)"
                color="primary"
                rounded="lg"
                class="mb-1"
                @click="pickDm(row)"
              >
                <template #prepend>
                  <v-avatar color="primary" size="36" class="text-white">
                    <span class="text-caption">{{ store.directTitle(row).slice(0, 2).toUpperCase() }}</span>
                  </v-avatar>
                </template>
                <v-list-item-title class="text-body-2 text-truncate">{{ store.directTitle(row) }}</v-list-item-title>
                <v-list-item-subtitle class="text-caption">{{ t('chatRoom.waSectionDm') }}</v-list-item-subtitle>
              </v-list-item>
            </v-list>
          </v-expansion-panel-text>
        </v-expansion-panel>

        <v-expansion-panel>
          <v-expansion-panel-title class="text-body-2 font-weight-medium">
            {{ t('chatRoom.waSectionTopics') }}
          </v-expansion-panel-title>
          <v-expansion-panel-text class="pa-0">
            <v-list density="compact" class="bg-transparent">
              <v-list-item v-if="!filteredRoots.length" class="text-caption text-medium-emphasis">
                {{ t('chatRoom.waNoTopics') }}
              </v-list-item>
              <v-list-item
                v-for="row in filteredRoots"
                :key="`tr-${row.dataId}`"
                :active="isActive('topic', row.dataId)"
                color="primary"
                rounded="lg"
                class="mb-1"
                @click="pickTopic(row, false)"
              >
                <template #prepend>
                  <v-avatar color="teal-darken-2" size="36" class="text-white">
                    <v-icon size="20" icon="mdi-forum" />
                  </v-avatar>
                </template>
                <v-list-item-title class="text-body-2 text-truncate">{{ row.title }}</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-expansion-panel-text>
        </v-expansion-panel>

        <v-expansion-panel>
          <v-expansion-panel-title class="text-body-2 font-weight-medium">
            {{ t('chatRoom.waSectionBranches') }}
          </v-expansion-panel-title>
          <v-expansion-panel-text class="pa-0">
            <v-list density="compact" class="bg-transparent">
              <v-list-item v-if="!filteredBranches.length" class="text-caption text-medium-emphasis">
                {{ t('chatRoom.waNoBranches') }}
              </v-list-item>
              <v-list-item
                v-for="row in filteredBranches"
                :key="`br-${row.dataId}`"
                :active="isActive('topic', row.dataId)"
                color="primary"
                rounded="lg"
                class="mb-1"
                @click="pickTopic(row, true)"
              >
                <template #prepend>
                  <v-avatar color="teal" size="36" class="text-white">
                    <v-icon size="20" icon="mdi-source-branch" />
                  </v-avatar>
                </template>
                <v-list-item-title class="text-body-2 text-truncate">{{ row.title }}</v-list-item-title>
                <v-list-item-subtitle v-if="row.parentTopicRoomId" class="text-caption text-truncate">
                  {{ topicTitleById[row.parentTopicRoomId] ?? '…' }}
                </v-list-item-subtitle>
              </v-list-item>
            </v-list>
          </v-expansion-panel-text>
        </v-expansion-panel>

        <v-expansion-panel>
          <v-expansion-panel-title class="text-body-2 font-weight-medium">
            {{ t('chatRoom.waSectionGroups') }}
          </v-expansion-panel-title>
          <v-expansion-panel-text class="pa-0">
            <v-list density="compact" class="bg-transparent">
              <v-list-item v-if="!filteredGroups.length" class="text-caption text-medium-emphasis">
                {{ t('chatRoom.waNoGroups') }}
              </v-list-item>
              <v-list-item
                v-for="row in filteredGroups"
                :key="`gr-${row.dataId}`"
                :active="isActive('group', row.dataId)"
                color="primary"
                rounded="lg"
                class="mb-1"
                @click="pickGroup(row)"
              >
                <template #prepend>
                  <v-avatar color="deep-purple-darken-2" size="36" class="text-white">
                    <v-icon size="20" icon="mdi-account-group" />
                  </v-avatar>
                </template>
                <v-list-item-title class="text-body-2 text-truncate">{{ store.groupTitle(row) }}</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>
    </div>

    <ChatRoomNewDmDialog v-model="newDmOpen" @created="pickDm" />
  </div>
</template>

<style scoped>
.chat-room-wa-panels :deep(.v-expansion-panel-title) {
  min-height: 44px;
  padding-top: 8px;
  padding-bottom: 8px;
}
</style>
