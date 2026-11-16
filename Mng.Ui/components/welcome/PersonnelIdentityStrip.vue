<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { fetchPersonnelIdentity, type PersonnelIdentity } from '@/services/personnelActivityFetch';

const props = defineProps<{ personId?: string | null }>();

const card = ref<PersonnelIdentity | null>(null);
const photoFailed = ref(false);

const initial = computed(() => {
  const name = card.value?.name?.trim() ?? '';
  return name ? name.slice(0, 1).toLocaleUpperCase('tr') : '';
});

const balanceText = computed(() => {
  const value = card.value?.leaveBalance;
  if (value == null) return '';
  return new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 1 }).format(value);
});

watch(
  () => props.personId?.trim() || '',
  async (personId) => {
    photoFailed.value = false;
    if (!personId) {
      card.value = null;
      return;
    }
    try {
      card.value = await fetchPersonnelIdentity(personId);
    } catch {
      card.value = null;
    }
  },
  { immediate: true },
);
</script>

<template>
  <v-card v-if="card" variant="outlined" class="mb-4">
    <v-card-text class="d-flex align-center ga-4 py-3">
      <v-avatar size="48" color="primary" variant="tonal">
        <v-img v-if="card.photoUrl && !photoFailed" :src="card.photoUrl" cover @error="photoFailed = true" />
        <span v-else class="text-title-medium">{{ initial }}</span>
      </v-avatar>
      <div>
        <div class="text-title-medium">{{ card.name || '—' }}</div>
        <div class="text-body-2 text-medium-emphasis">{{ card.department || 'Departman yok' }}</div>
      </div>
      <v-spacer />
      <div class="text-body-2 text-right">
        <div>Yönetici: {{ card.manager || '—' }}</div>
        <div>Yıllık izin: {{ balanceText === '' ? '—' : `${balanceText} gün` }}</div>
      </div>
    </v-card-text>
  </v-card>
</template>
