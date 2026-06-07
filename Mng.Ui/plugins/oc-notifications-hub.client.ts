import { watch } from 'vue';
import { useHubStore } from '@/stores/hub';
import { useAuthStore } from '@/stores/auth';
import { useAppToast } from '@/composables/useAppToast';
import { useNotificationBell } from '@/composables/useNotificationBell';

const OC_NOTIFY_SUB_ID = 'oc-user-notifications';

/**
 * MO / alarm in-app bildirimleri: Hub ReceiveUserNotification → global toast + zil badge yenileme.
 */
export default defineNuxtPlugin(() => {
  if (import.meta.server) return;

  const hubStore = useHubStore();
  const authStore = useAuthStore();
  const { push } = useAppToast();
  const { requestRefresh } = useNotificationBell();

  async function ensureNotificationHubPipeline() {
    try {
      await hubStore.connectToHub();
    } catch {
      return;
    }

    hubStore.unsubscribeUserNotification(OC_NOTIFY_SUB_ID);
    hubStore.subscribeUserNotification(OC_NOTIFY_SUB_ID, (payload) => {
      push({
        title: payload.title ?? '',
        message: payload.message ?? '',
        notificationType: payload.notificationType,
        deepLink: payload.deepLink,
        severity: payload.severity,
      });
      requestRefresh();
    });
  }

  watch(
    () => authStore.accessToken,
    async (token) => {
      if (!token?.trim()) {
        hubStore.unsubscribeUserNotification(OC_NOTIFY_SUB_ID);
        return;
      }
      await ensureNotificationHubPipeline();
    },
    { immediate: true }
  );
});
