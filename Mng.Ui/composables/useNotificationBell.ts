import { ref } from 'vue';

const refreshTick = ref(0);

export function useNotificationBell() {
  function requestRefresh() {
    refreshTick.value += 1;
  }

  return {
    refreshTick,
    requestRefresh,
  };
}
