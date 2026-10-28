<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import OcSelectParentDialog from '@/components/apps/operation-core/OcSelectParentDialog.vue';
import { ocListDatasetPage, ocUpdateWorkItem } from '@/services/operationCoreService';
import type { OcBoardCatalogs, OcWorkItemRelationSummary } from '@/types/apps/operationCore';
import { buildWorkItemProfilePath } from '@/utils/ocWorkItemProfileNav';

const props = defineProps<{
  workItemId: string;
  workspaceId: string;
  typeId?: string | null;
  canEdit?: boolean;
  canHaveChildren?: boolean;
  childTypeId?: string | null;
  parent?: OcWorkItemRelationSummary | null;
  children?: OcWorkItemRelationSummary[];
  catalogs?: OcBoardCatalogs | null;
  boardId?: string | null;
  workspaceIdQuery?: string | null;
}>();

const emit = defineEmits<{
  refresh: [];
}>();

const { t } = useAppI18n();
const router = useRouter();
const panelError = usePanelErrorNotify('errors.dg.generic');

const parentTypeIds = ref<string[]>([]);
const dialogOpen = ref(false);
const clearing = ref(false);
const actionError = ref<string | null>(null);

const isLeaf = computed(() => (props.children?.length ?? 0) === 0);
const canMove = computed(() => !!props.canEdit && isLeaf.value && parentTypeIds.value.length > 0);
const visible = computed(
  () => !!props.canHaveChildren || !!props.parent || (props.children?.length ?? 0) > 0 || canMove.value
);

function profilePath(relation: OcWorkItemRelationSummary) {
  return buildWorkItemProfilePath(relation.id, {
    boardId: props.boardId,
    workspaceId: props.workspaceIdQuery ?? props.workspaceId,
    from: props.workspaceIdQuery ? 'workspace' : 'board',
  });
}

function stateName(stateId?: string | null): string {
  if (!stateId) return '';
  return props.catalogs?.states?.[stateId]?.name?.trim() || '';
}

function stringList(value: unknown): string[] {
  if (Array.isArray(value)) return value.map((item) => String(item).trim()).filter(Boolean);
  if (typeof value === 'string' && value.trim()) return [value.trim()];
  return [];
}

function rowId(row: Record<string, unknown>): string {
  return String(row.__dataId ?? row.dataId ?? '').trim();
}

async function loadParentTypes() {
  parentTypeIds.value = [];
  const ws = props.workspaceId.trim();
  const typeId = props.typeId?.trim();
  if (!props.canEdit || !isLeaf.value || !ws || !typeId) return;

  try {
    const page = await ocListDatasetPage('op_work_item_types', {
      filter: `workspaceId:eq:${ws}`,
      limit: 100,
      expand: false,
    });
    const rows = page.items.filter((row): row is Record<string, unknown> => !!row && typeof row === 'object');
    const current = rows.find((row) => rowId(row) === typeId);
    const accepted = new Set(
      [typeId, typeof current?.packKey === 'string' ? current.packKey.trim() : '']
        .filter(Boolean)
        .map((value) => value.toLowerCase())
    );
    parentTypeIds.value = rows
      .filter((row) => stringList(row.childTypeKeys).some((key) => accepted.has(key.toLowerCase())))
      .map(rowId)
      .filter(Boolean);
  } catch {
    parentTypeIds.value = [];
  }
}

function addChild() {
  const query = new URLSearchParams({
    workspaceId: props.workspaceId,
    parentItemId: props.workItemId,
  });
  if (props.childTypeId) query.set('typeId', props.childTypeId);
  void router.push(`/apps/operation-core/work-items/new?${query.toString()}`);
}

async function clearParent() {
  clearing.value = true;
  actionError.value = null;
  try {
    await ocUpdateWorkItem(props.workItemId, { fields: { parentItemId: '' } });
    emit('refresh');
  } catch (e: unknown) {
    actionError.value = panelError(e, 'operationCore.profile.childrenCard.clearError');
  } finally {
    clearing.value = false;
  }
}

watch(
  () => [props.workspaceId, props.typeId, props.canEdit, props.children?.length ?? 0] as const,
  () => {
    void loadParentTypes();
  },
  { immediate: true }
);
</script>

<template>
  <v-card v-if="visible" variant="outlined" class="rounded-lg mb-4">
    <v-card-text class="pa-4">
      <v-alert v-if="actionError" type="error" variant="tonal" density="compact" class="mb-3">
        {{ actionError }}
      </v-alert>

      <div v-if="parent" class="mb-3">
        <div class="text-caption text-medium-emphasis mb-1">
          {{ t('operationCore.profile.childrenCard.parent') }}
        </div>
        <NuxtLink :to="profilePath(parent)" class="text-primary text-decoration-none text-body-2">
          <span class="font-weight-medium">{{ parent.key }}</span>
          <span v-if="parent.title" class="text-medium-emphasis"> — {{ parent.title }}</span>
        </NuxtLink>
        <div v-if="canMove" class="d-flex ga-2 mt-2">
          <v-btn size="small" variant="tonal" :disabled="clearing" @click="dialogOpen = true">
            {{ t('operationCore.profile.childrenCard.change') }}
          </v-btn>
          <v-btn size="small" variant="text" :loading="clearing" @click="clearParent">
            {{ t('operationCore.profile.childrenCard.clear') }}
          </v-btn>
        </div>
      </div>

      <template v-if="canHaveChildren || children?.length">
        <div class="d-flex align-center mb-2">
          <div class="text-subtitle-2 font-weight-bold">
            {{ t('operationCore.profile.childrenCard.title') }}
            <span class="text-medium-emphasis font-weight-regular">({{ children?.length ?? 0 }})</span>
          </div>
          <v-spacer />
          <v-btn
            v-if="canEdit && canHaveChildren && childTypeId"
            size="small"
            variant="tonal"
            color="primary"
            prepend-icon="mdi-plus"
            @click="addChild"
          >
            {{ t('operationCore.profile.childrenCard.add') }}
          </v-btn>
        </div>

        <p v-if="!children?.length" class="text-caption text-medium-emphasis mb-0">
          {{ t('operationCore.profile.childrenCard.empty') }}
        </p>
        <div v-for="child in children" :key="child.id" class="oc-child-row">
          <NuxtLink :to="profilePath(child)" class="text-primary text-decoration-none text-body-2">
            <span class="font-weight-medium">{{ child.key }}</span>
            <span v-if="child.title" class="text-medium-emphasis"> — {{ child.title }}</span>
          </NuxtLink>
          <div v-if="stateName(child.stateId)" class="text-caption text-medium-emphasis">
            {{ stateName(child.stateId) }}
          </div>
        </div>
      </template>

      <v-btn
        v-if="canMove && !parent"
        size="small"
        variant="tonal"
        color="primary"
        @click="dialogOpen = true"
      >
        {{ t('operationCore.profile.childrenCard.move') }}
      </v-btn>

      <OcSelectParentDialog
        v-model="dialogOpen"
        :work-item-id="workItemId"
        :workspace-id="workspaceId"
        :parent-type-ids="parentTypeIds"
        :current-parent-id="parent?.id"
        @moved="emit('refresh')"
      />
    </v-card-text>
  </v-card>
</template>

<style scoped>
.oc-child-row + .oc-child-row {
  margin-top: 0.5rem;
}
</style>
