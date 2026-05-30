import { onMounted, onUnmounted, readonly, ref } from 'vue';

// Paylaşılan "şimdi" ticker'ı: çok sayıda bileşen (ör. liste satırı başına bir SLA chip)
// kendi setInterval'ını kurmak yerine tek bir global timer'ı paylaşır. Refcount ile yalnız
// en az bir tüketici mount iken çalışır. Çıktı (now) tüm tüketiciler için ortaktır.
const sharedNow = ref(Date.now());
let refCount = 0;
let timer: ReturnType<typeof setInterval> | null = null;

function ensureTimer(intervalMs: number) {
  if (timer) return;
  sharedNow.value = Date.now();
  timer = setInterval(() => {
    sharedNow.value = Date.now();
  }, intervalMs);
}

function releaseTimer() {
  if (refCount > 0 || !timer) return;
  clearInterval(timer);
  timer = null;
}

/**
 * Tek global timer'a bağlı, salt-okunur reaktif "now" (ms). Varsayılan 60 sn.
 */
export function useSharedNow(intervalMs = 60_000) {
  onMounted(() => {
    refCount += 1;
    ensureTimer(intervalMs);
  });
  onUnmounted(() => {
    refCount = Math.max(0, refCount - 1);
    releaseTimer();
  });
  return readonly(sharedNow);
}
