<script setup lang="ts">
import { computed, onErrorCaptured, onMounted, onUnmounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AppBaseCard from '@/components/shared/AppBaseCard.vue';
import ChatRoomSidebar from '@/components/apps/chats/ChatRoomSidebar.vue';
import ChatRoomSidebarHeader from '@/components/apps/chats/ChatRoomSidebarHeader.vue';
import ChatRoomThread from '@/components/apps/chats/ChatRoomThread.vue';
import { useHubStore } from '@/stores/hub';
import { useChatRoomWorkspaceStore } from '@/stores/apps/chatRoomWorkspace';

const LOG = 'CHAT_ROOM_DEBUG';

onErrorCaptured((err, instance, info) => {
  const comp =
    (instance as unknown as { $?: { type?: { name?: string; __name?: string } } })?.$?.type;
  console.error(LOG, 'onErrorCaptured', {
    message: err instanceof Error ? err.message : String(err),
    stack: err instanceof Error ? err.stack : undefined,
    info,
    componentName: comp?.name ?? comp?.__name,
  });
  return false;
});

const { t } = useAppI18n();
const hubStore = useHubStore();
const chatRoomWs = useChatRoomWorkspaceStore();

definePageMeta({ layout: 'default' });

useHead({
  title: () => t('chatRoom.pageTitle'),
});

const breadcrumbs = computed(() => [{ title: t('chatRoom.breadcrumb'), disabled: true, href: '#' }]);

const hubConnectError = ref<string | null>(null);

/** Hub kesintisi / sekme arka planda kaldı: geçmişi DG ile yeniden yükle (sessiz). */
function syncChatFromServer() {
  void chatRoomWs.refreshAfterTransportGap({ silent: true });
}

let visibilityHandler: (() => void) | null = null;

onMounted(async () => {
  hubConnectError.value = null;
  try {
    await hubStore.connectToHub();
  } catch (e: unknown) {
    hubConnectError.value = e instanceof Error ? e.message : String(e);
    console.error(LOG, 'hub connectToHub hata', e);
  }

  visibilityHandler = () => {
    if (document.visibilityState === 'visible') syncChatFromServer();
  };
  document.addEventListener('visibilitychange', visibilityHandler);
});

onUnmounted(() => {
  if (visibilityHandler) {
    document.removeEventListener('visibilitychange', visibilityHandler);
    visibilityHandler = null;
  }
});
</script>

<template>
  <div class="chat-room-wa d-flex flex-column flex-grow-1 min-h-0 h-100">
    <div class="chat-room-breadcrumb flex-shrink-0 mb-3">
      <BaseBreadcrumb :title="t('chatRoom.pageTitle')" :breadcrumbs="breadcrumbs" />
    </div>

    <v-card flat class="chat-room-card overflow-hidden flex-grow-1 min-h-0 h-100 d-flex flex-column">
      <AppBaseCard>
        <template #leftpart>
          <ChatRoomSidebarHeader :connect-error="hubConnectError" />
          <ChatRoomSidebar />
        </template>
        <template #rightpart>
          <ChatRoomThread />
        </template>
        <template #mobileLeftContent>
          <ChatRoomSidebarHeader :connect-error="hubConnectError" />
          <ChatRoomSidebar />
        </template>
      </AppBaseCard>
    </v-card>
  </div>
</template>

<style src="@/assets/css/chat-room-wa.css"></style>
