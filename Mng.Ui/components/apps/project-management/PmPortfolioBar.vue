<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { PmPortfolio } from '@/types/apps/projectManagement';

const props = defineProps<{
  pack: PmPortfolio | null;
  filter: 'all' | 'attention' | 'active';
}>();

const emit = defineEmits<{
  'update:filter': [value: 'all' | 'attention' | 'active'];
}>();

const { t } = useAppI18n();

const cards = computed(() => {
  const p = props.pack;
  return [
    { key: 'all' as const, label: t('projectManagement.portfolio.all'), value: p?.projectCount ?? 0, color: 'default' },
    { key: 'active' as const, label: t('projectManagement.portfolio.active'), value: p?.activeCount ?? 0, color: 'success' },
    { key: 'attention' as const, label: t('projectManagement.portfolio.attention'), value: p?.attentionCount ?? 0, color: 'error' },
    { key: 'delayed' as const, label: t('projectManagement.statusPack.flag.delayed'), value: p?.totals?.delayed ?? 0, color: 'error' },
    { key: 'openRisk' as const, label: t('projectManagement.statusPack.flag.openRisk'), value: p?.totals?.openRisk ?? 0, color: 'error' },
    { key: 'failedGate' as const, label: t('projectManagement.statusPack.flag.failedGate'), value: p?.totals?.failedGate ?? 0, color: 'error' },
    { key: 'overBudget' as const, label: t('projectManagement.statusPack.flag.overBudget'), value: p?.totals?.overBudget ?? 0, color: 'error' },
    { key: 'overdueObligation' as const, label: t('projectManagement.statusPack.flag.overdueObligation'), value: p?.totals?.overdueObligation ?? 0, color: 'error' },
  ];
});

function onCard(key: string) {
  if (key === 'all' || key === 'attention' || key === 'active') {
    emit('update:filter', props.filter === key ? 'all' : key);
  }
}
</script>

<template>
  <div>
    <div class="text-body-2 text-medium-emphasis mb-3">
      {{ t('projectManagement.portfolio.hint') }}
    </div>
    <div class="d-flex flex-wrap ga-2">
      <v-chip
        v-for="card in cards"
        :key="card.key"
        :color="card.color"
        :variant="filter === card.key ? 'flat' : 'tonal'"
        :disabled="!card.value && card.key !== 'all'"
        @click="card.value || card.key === 'all' ? onCard(card.key) : undefined"
      >
        {{ card.label }} · {{ card.value }}
      </v-chip>
    </div>
  </div>
</template>
