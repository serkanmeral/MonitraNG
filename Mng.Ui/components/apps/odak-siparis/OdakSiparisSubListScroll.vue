<script setup lang="ts">
/**
 * Odak Sipariş alt listeleri — yatay scroll + sticky eylem sütunu / toolbar.
 * Scroll tek kaynak bu sarmalayıcıdır; Vuetify .v-table__wrapper overflow visible bırakılır.
 */
withDefaults(
  defineProps<{
    /** İlk sütun expand ikonu (kalemler listesi). */
    stickyExpandColumn?: boolean;
  }>(),
  {
    stickyExpandColumn: false,
  }
);
</script>

<template>
  <div class="odak-sub-list-root">
    <div class="odak-sub-list-scroll">
      <div class="odak-sub-list-inner">
        <div v-if="$slots.toolbar" class="odak-sub-list-toolbar-wrap">
          <slot name="toolbar" />
        </div>
        <div
          class="odak-sub-list-table-host"
          :class="{ 'odak-sub-list-table-host--expand': stickyExpandColumn }"
        >
          <slot />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.odak-sub-list-root {
  min-width: 0;
  max-width: 100%;
}

.odak-sub-list-scroll {
  display: block;
  width: 100%;
  max-width: 100%;
  min-width: 0;
  overflow-x: auto;
  overflow-y: visible;
  -webkit-overflow-scrolling: touch;
  padding-bottom: 2px;
}

.odak-sub-list-scroll::-webkit-scrollbar {
  height: 10px;
}

.odak-sub-list-scroll::-webkit-scrollbar-thumb {
  background: rgba(var(--v-theme-on-surface), 0.35);
  border-radius: 6px;
}

.odak-sub-list-scroll::-webkit-scrollbar-track {
  background: rgba(var(--v-theme-on-surface), 0.08);
  border-radius: 6px;
}

.odak-sub-list-inner {
  width: fit-content;
  min-width: 100%;
}

.odak-sub-list-toolbar-wrap {
  margin-bottom: 12px;
}

.odak-sub-list-toolbar-wrap :deep(.odak-sub-list-toolbar-row) {
  display: flex;
  align-items: center;
  flex-wrap: nowrap;
  gap: 8px;
  min-width: 100%;
}

.odak-sub-list-toolbar-wrap :deep(.odak-sub-list-toolbar-info) {
  flex: 1 1 auto;
  min-width: 0;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}

.odak-sub-list-toolbar-wrap :deep(.odak-sub-list-toolbar-actions) {
  position: sticky;
  right: 0;
  flex: 0 0 auto;
  z-index: 5;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  background: rgb(var(--v-theme-surface));
  padding-left: 12px;
  box-shadow: -8px 0 8px -8px rgba(0, 0, 0, 0.15);
}

.odak-sub-list-table-host :deep(.odak-sub-list-table) {
  display: block;
  width: fit-content;
  min-width: 100%;
}

.odak-sub-list-table-host :deep(.odak-sub-list-table .v-table),
.odak-sub-list-table-host :deep(.odak-sub-list-table .v-table__wrapper) {
  overflow: visible !important;
}

.odak-sub-list-table-host :deep(.odak-sub-list-table table) {
  width: auto !important;
  table-layout: auto !important;
}

.odak-sub-list-table-host :deep(.odak-sub-list-table th.v-data-table__th),
.odak-sub-list-table-host :deep(.odak-sub-list-table td.v-data-table__td) {
  white-space: nowrap;
}

.odak-sub-list-table-host :deep(.odak-sub-list-table thead th:last-child),
.odak-sub-list-table-host :deep(.odak-sub-list-table tbody tr:not(.v-data-table__expanded__content) > td:last-child) {
  min-width: 132px;
}

/* Eylemler sütunu — sağda sabit */
.odak-sub-list-table-host :deep(.odak-sub-list-table table > thead > tr > th:last-child),
.odak-sub-list-table-host :deep(.odak-sub-list-table table > tbody > tr:not(.v-data-table__expanded__content) > td:last-child) {
  position: sticky;
  right: 0;
  background: rgb(var(--v-theme-surface));
  box-shadow: -6px 0 6px -6px rgba(0, 0, 0, 0.18);
}

.odak-sub-list-table-host :deep(.odak-sub-list-table table > tbody > tr:not(.v-data-table__expanded__content) > td:last-child) {
  z-index: 1;
}

.odak-sub-list-table-host :deep(.odak-sub-list-table table > thead > tr > th:last-child) {
  z-index: 2;
}

/* Expand sütunu — solda sabit (kalemler) */
.odak-sub-list-table-host--expand :deep(.odak-sub-list-table table > thead > tr > th:first-child),
.odak-sub-list-table-host--expand :deep(.odak-sub-list-table table > tbody > tr:not(.v-data-table__expanded__content) > td:first-child) {
  position: sticky;
  left: 0;
  z-index: 3;
  background: rgb(var(--v-theme-surface));
  box-shadow: 6px 0 6px -6px rgba(0, 0, 0, 0.12);
}
</style>
