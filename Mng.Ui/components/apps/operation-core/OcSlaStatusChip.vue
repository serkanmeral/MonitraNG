<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { formatDistanceStrict } from 'date-fns';
import { enUS, tr } from 'date-fns/locale';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcSlaSnapshot } from '@/types/apps/operationCore';

const props = defineProps<{
  sla?: OcSlaSnapshot | null;
  stateId?: string | null;
  initialStateId?: string | null;
  closedAt?: string | null;
  /** Yoğun liste hücresi için küçük chip. */
  dense?: boolean;
}>();

const { t, locale } = useAppI18n();

const dfLocale = computed(() => (locale().toLowerCase().startsWith('tr') ? tr : enUS));

// Canlı kalan/gecikme için "şimdi"yi periyodik güncelle.
const now = ref(Date.now());
let timer: ReturnType<typeof setInterval> | null = null;
onMounted(() => {
  timer = setInterval(() => {
    now.value = Date.now();
  }, 60_000);
});
onUnmounted(() => {
  if (timer) clearInterval(timer);
});

function toMs(v: string | null | undefined): number | null {
  if (!v) return null;
  const d = new Date(v).getTime();
  return Number.isNaN(d) ? null : d;
}

type Phase = 'none' | 'closed' | 'response' | 'resolve';

const model = computed(() => {
  const sla = props.sla;
  const responseDue = toMs(sla?.responseDueAt);
  const resolveDue = toMs(sla?.resolveDueAt);
  const hasPolicy = !!sla?.slaPolicyId || responseDue != null || resolveDue != null;
  if (!hasPolicy) {
    return { phase: 'none' as Phase };
  }

  const closedMs = toMs(props.closedAt);
  if (closedMs != null) {
    // Kapanış anında resolve hedefi aşılmış mıydı?
    const late = resolveDue != null ? closedMs > resolveDue : !!sla?.resolveBreached;
    return { phase: 'closed' as Phase, late };
  }

  // Akıllı faz: hâlâ başlangıç state'inde ve response hedefi varsa response; aksi halde resolve.
  const inInitial =
    !!props.initialStateId && !!props.stateId && props.stateId === props.initialStateId;
  let phase: Phase = 'resolve';
  let due = resolveDue;
  if (inInitial && responseDue != null) {
    phase = 'response';
    due = responseDue;
  }
  if (due == null) {
    due = resolveDue ?? responseDue;
    phase = due === responseDue ? 'response' : 'resolve';
  }
  if (due == null) return { phase: 'none' as Phase };

  const remainingMs = due - now.value;
  const overdue = remainingMs < 0;

  // Uyarı eşiği: kalan, toplam sürenin %20'sinden azsa (calculatedAt baz).
  const startMs = toMs(sla?.calculatedAt);
  let warning = false;
  if (!overdue) {
    if (startMs != null && due > startMs) {
      warning = remainingMs / (due - startMs) < 0.2;
    } else {
      warning = remainingMs < 8 * 60 * 60 * 1000;
    }
  }

  return { phase, due, overdue, warning, remainingMs };
});

const distanceText = computed(() => {
  const m = model.value;
  if (m.due == null) return '';
  return formatDistanceStrict(now.value, m.due, { locale: dfLocale.value });
});

const chip = computed(() => {
  const m = model.value;
  switch (m.phase) {
    case 'none':
      return null;
    case 'closed':
      return m.late
        ? { color: 'error', icon: 'mdi-flag-checkered', text: t('operationCore.board.sla.closedLate') }
        : { color: 'success', icon: 'mdi-flag-checkered', text: t('operationCore.board.sla.closedOnTime') };
    default: {
      const phaseLabel =
        m.phase === 'response'
          ? t('operationCore.board.sla.phaseResponse')
          : t('operationCore.board.sla.phaseResolve');
      if (m.overdue) {
        return {
          color: 'error',
          icon: 'mdi-alert',
          text: t('operationCore.board.sla.overdue', { time: distanceText.value }),
          tooltip: phaseLabel,
        };
      }
      return {
        color: m.warning ? 'warning' : 'success',
        icon: m.warning ? 'mdi-clock-alert-outline' : 'mdi-clock-outline',
        text: t('operationCore.board.sla.remaining', { time: distanceText.value }),
        tooltip: phaseLabel,
      };
    }
  }
});
</script>

<template>
  <span v-if="!chip" class="text-medium-emphasis">—</span>
  <v-chip
    v-else
    :color="chip.color"
    :size="dense ? 'x-small' : 'small'"
    variant="tonal"
    label
    :title="chip.tooltip"
  >
    <v-icon :icon="chip.icon" start :size="dense ? 12 : 14" />
    {{ chip.text }}
  </v-chip>
</template>
