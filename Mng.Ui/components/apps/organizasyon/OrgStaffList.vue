<script setup lang="ts">
import { computed, ref, watch } from 'vue';

const props = defineProps<{ names: string[] }>();

const pageSize = 20;
const query = ref('');
const descending = ref(false);
const page = ref(1);

const filtered = computed(() => {
  const needle = query.value.trim().toLocaleLowerCase('tr');
  const source = needle
    ? props.names.filter((name) => name.toLocaleLowerCase('tr').includes(needle))
    : [...props.names];
  source.sort((left, right) => left.localeCompare(right, 'tr', { sensitivity: 'base' }));
  if (descending.value) source.reverse();
  return source;
});

const pageCount = computed(() => Math.max(1, Math.ceil(filtered.value.length / pageSize)));

const visible = computed(() => {
  const start = (page.value - 1) * pageSize;
  return filtered.value.slice(start, start + pageSize);
});

watch(query, () => {
  page.value = 1;
});

watch(pageCount, (count) => {
  if (page.value > count) page.value = count;
});

function toggleSort() {
  descending.value = !descending.value;
  page.value = 1;
}

function initial(name: string) {
  const clean = name.trim();
  if (!clean || clean === 'Kişi') return '';
  return clean.charAt(0).toLocaleUpperCase('tr');
}
</script>

<template>
  <div class="org-staff-list nodrag nopan nowheel" @mousedown.stop @pointerdown.stop @wheel.stop>
    <div class="org-staff-list__bar">
      <button type="button" class="org-staff-list__sort" @click="toggleSort">
        Ad
        <v-icon size="14" :icon="descending ? 'mdi-arrow-down' : 'mdi-arrow-up'" />
      </button>
      <span>{{ filtered.length }} kişi</span>
    </div>
    <v-text-field
      v-model="query"
      density="compact"
      variant="outlined"
      hide-details
      placeholder="Ad ara"
      prepend-inner-icon="mdi-magnify"
      class="org-staff-list__search"
    />
    <ul>
      <li v-for="(name, index) in visible" :key="`${page}-${index}-${name}`">
        <span class="org-staff-list__mark">{{ initial(name) }}</span>
        <span>{{ name }}</span>
      </li>
    </ul>
    <p v-if="!filtered.length" class="org-staff-list__empty">Eşleşen kişi yok.</p>
    <v-pagination
      v-if="pageCount > 1"
      v-model="page"
      :length="pageCount"
      density="compact"
      size="small"
      total-visible="5"
      class="org-staff-list__pages"
    />
  </div>
</template>

<style scoped>
.org-staff-list {
  width: 280px;
  max-height: 70vh;
  padding: 10px 12px 12px;
  border-radius: 12px;
  background: rgb(var(--v-theme-surface));
}
.org-staff-list__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
  color: rgba(var(--v-theme-on-surface), 0.62);
  font-size: 12px;
}
.org-staff-list__sort {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  padding: 0;
  border: 0;
  background: transparent;
  color: rgb(var(--v-theme-secondary));
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
}
.org-staff-list__search {
  margin-bottom: 8px;
}
.org-staff-list ul {
  max-height: 320px;
  margin: 0;
  padding: 0;
  overflow: auto;
  list-style: none;
}
.org-staff-list li {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 6px;
  color: rgba(var(--v-theme-on-surface), 0.9);
  font-size: 13px;
  line-height: 18px;
}
.org-staff-list__mark {
  display: inline-flex;
  flex: 0 0 22px;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: rgba(var(--v-theme-secondary), 0.16);
  color: rgb(var(--v-theme-secondary));
  font-size: 11px;
  font-weight: 700;
}
.org-staff-list__empty {
  margin: 8px 0 0;
  color: rgba(var(--v-theme-on-surface), 0.55);
  font-size: 12px;
}
.org-staff-list__pages {
  margin-top: 8px;
}
</style>
