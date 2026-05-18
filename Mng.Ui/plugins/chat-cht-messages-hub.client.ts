import { watch } from 'vue';
import { useHubStore } from '@/stores/hub';
import { useAuthStore } from '@/stores/auth';
import { useChatRoomWorkspaceStore } from '@/stores/apps/chatRoomWorkspace';
import { isChtMessagesHubPayload } from '@/utils/chatRoomDgHub';

const CHAT_SUB_ID = 'cht-messages-global';
const CHAT_RECONNECT_ID = 'cht-messages-global-reconnect';

/**
 * `cht_messages` hub olayları yalnızca sohbet sayfası mount olduğunda değil, oturum boyunca işlenmeli;
 * aksi halde yalnızca `side-menu` aboneliği kalır ve `dataset.cht_messages.*` için hiçbir handler çalışmaz.
 */
export default defineNuxtPlugin(() => {
  if (import.meta.server) return;

  const hubStore = useHubStore();
  const authStore = useAuthStore();
  const chatWs = useChatRoomWorkspaceStore();

  async function ensureChatHubPipeline() {
    try {
      await hubStore.connectToHub();
    } catch {
      return;
    }
    hubStore.unregisterReconnectHandler(CHAT_RECONNECT_ID);
    hubStore.registerReconnectHandler(CHAT_RECONNECT_ID, () => {
      void chatWs.refreshAfterTransportGap({ silent: true });
    });
    hubStore.unsubscribe(CHAT_SUB_ID);
    hubStore.subscribe(CHAT_SUB_ID, {
      filter: isChtMessagesHubPayload,
      handler: (data) => {
        chatWs.onHubChtMessage(data);
      },
    });
  }

  watch(
    () => authStore.accessToken,
    async (token) => {
      if (!token?.trim()) {
        hubStore.unregisterReconnectHandler(CHAT_RECONNECT_ID);
        hubStore.unsubscribe(CHAT_SUB_ID);
        return;
      }
      await ensureChatHubPipeline();
    },
    { immediate: true }
  );

  watch(
    () => hubStore.connected,
    (isConnected, wasConnected) => {
      if (isConnected && wasConnected === false) {
        void chatWs.refreshAfterTransportGap({ silent: true });
      }
    }
  );
});
