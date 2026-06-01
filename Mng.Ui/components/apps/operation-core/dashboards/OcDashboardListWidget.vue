<script setup lang="ts">
import { computed } from 'vue';
import { useRouter } from 'vue-router';
import { useAppI18n } from '@/composables/useAppI18n';
import OcBoardCatalogLabel from '@/components/apps/operation-core/OcBoardCatalogLabel.vue';
import type { OcCatalogDisplayItem } from '@/utils/ocCatalogDisplay';
import type {
  OcBoardCatalogs,
  OcDashboardWidget,
  OcPersonDisplay,
  OcWorkItemCard,
} from '@/types/apps/operationCore';

const props = defineProps<{
  widget: OcDashboardWidget;
  catalogs: OcBoardCatalogs;
  people: Record<string, OcPersonDisplay>;
  groups: Record<string, OcPersonDisplay>;
}>();

const { t } = useAppI18n();
const router = useRouter();

const execution = computed(() => props.widget.execution ?? null);
const failed = computed(() => execution.value != null && execution.value.success === false);
const items = computed<OcWorkItemCard[]>(() => execution.value?.items ?? []);
const title = computed(() => props.widget.title?.trim() || props.widget.key);
const total = computed(() => execution.value?.total ?? items.value.length);

function stateItem(stateId?: string): OcCatalogDisplayItem | null {
  if (!stateId) return null;
  const e = props.catalogs?.states?.[stateId];
  if (!e) return { id: stateId, name: stateId, color: null, icon: null };
  return { id: stateId, name: e.name, color: e.color ?? null, icon: e.icon ?? null };
}

function assigneeName(card: OcWorkItemCard): string | null {
  if (!card.assignee) return null;
  return props.people?.[card.assignee]?.name?.trim() || null;
}

function openItem(card: OcWorkItemCard) {
  router.push(`/apps/operation-core/work-items/${encodeURIComponent(card.id)}/profile`);
}
</script>

<template>
  <v-card variant="outlined" class="rounded-lg h-100 d-flex flex-column oc-dash-list">
    <v-card-title class="d-flex align-center py-2 px-4 ga-2">
      <span class="text-subtitle-2 font-weight-medium text-truncate">{{ title }}</span>
      <v-spacer />
      <v-chip size="x-small" variant="tonal" color="primary">{{ total }}</v-chip>
    </v-card-title>
    <v-divider />

    <v-card-text class="pa-0 flex-grow-1 overflow-auto">
      <div v-if="failed" class="pa-4 d-flex align-center ga-1 text-error">
        <v-icon icon="mdi-alert-circle-outline" size="18" />
        <span class="text-caption">
          {{ execution?.errorMessage || t('operationCore.dashboards.widgetError') }}
        </span>
      </div>

      <div v-else-if="!items.length" class="pa-4 text-body-2 text-medium-emphasis">
        {{ t('operationCore.dashboards.emptyWidget') }}
      </div>

      <v-list v-else density="compact" class="py-0">
        <v-list-item
          v-for="card in items"
          :key="card.id"
          class="oc-dash-list-row"
          @click="openItem(card)"
        >
          <div class="d-flex align-center ga-2 w-100" style="min-width: 0">
            <span class="text-caption text-medium-emphasis flex-shrink-0">{{ card.key }}</span>
            <span class="text-body-2 text-truncate flex-grow-1">{{ card.title }}</span>
            <OcBoardCatalogLabel
              v-if="card.stateId"
              :item="stateItem(card.stateId)"
              class="flex-shrink-0 text-caption"
            />
            <span
              v-if="assigneeName(card)"
              class="text-caption text-medium-emphasis flex-shrink-0 text-truncate"
              style="max-width: 120px"
            >
              {{ assigneeName(card) }}
            </span>
          </div>
        </v-list-item>
      </v-list>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.oc-dash-list {
  min-height: 220px;
}
.oc-dash-list-row {
  cursor: pointer;
}
.oc-dash-list-row:not(:last-child) {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
