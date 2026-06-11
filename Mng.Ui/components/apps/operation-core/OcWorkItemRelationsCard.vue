<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcLinkWorkItemDialog from '@/components/apps/operation-core/OcLinkWorkItemDialog.vue';
import { ocDeleteWorkItemLink, ocExtractDgErrorMessage } from '@/services/operationCoreService';
import type { OcWorkItemLinkSummary, OcWorkItemRelationSummary } from '@/types/apps/operationCore';
import { buildWorkItemProfilePath } from '@/utils/ocWorkItemProfileNav';

const props = defineProps<{
  workItemId: string;
  workspaceId: string;
  canEdit?: boolean;
  parent?: OcWorkItemRelationSummary | null;
  children?: OcWorkItemRelationSummary[];
  links?: OcWorkItemLinkSummary[];
  boardId?: string | null;
  workspaceIdQuery?: string | null;
}>();

const emit = defineEmits<{
  refresh: [];
}>();

const { t } = useAppI18n();

const linkDialogOpen = ref(false);
const deleteBusyId = ref<string | null>(null);
const actionError = ref<string | null>(null);

const hasTree = computed(() => !!props.parent || (props.children?.length ?? 0) > 0);
const hasLinks = computed(() => (props.links?.length ?? 0) > 0);
const visible = computed(() => hasTree.value || hasLinks.value || props.canEdit);

function profilePath(relation: OcWorkItemRelationSummary | OcWorkItemLinkSummary, otherId?: string) {
  const id = 'id' in relation && relation.id ? relation.id : otherId ?? '';
  return buildWorkItemProfilePath(id, {
    boardId: props.boardId,
    workspaceId: props.workspaceIdQuery ?? props.workspaceId,
    from: props.workspaceIdQuery ? 'workspace' : 'board',
  });
}

function linkLabel(link: OcWorkItemLinkSummary): string {
  const key = link.otherWorkItemKey?.trim() || link.otherWorkItemId;
  const type = link.linkType?.trim().toLowerCase() || 'relates_to';
  const dir = link.direction?.trim().toLowerCase() || 'outgoing';
  const typeKey = `operationCore.profile.relations.linkTypes.${type}`;
  const typeLabel = t(typeKey);
  if (type === 'blocks') {
    if (dir === 'incoming') return t('operationCore.profile.relations.blockedBy', { key });
    return t('operationCore.profile.relations.blocks', { key });
  }
  if (type === 'duplicates') {
    if (dir === 'incoming') return t('operationCore.profile.relations.duplicatedBy', { key });
    return t('operationCore.profile.relations.duplicates', { key });
  }
  return `${typeLabel} — ${key}`;
}

function linkTitle(link: OcWorkItemLinkSummary): string | null {
  return link.otherWorkItemTitle?.trim() || null;
}

async function removeLink(link: OcWorkItemLinkSummary) {
  const linkId = link.id?.trim();
  if (!linkId) return;
  deleteBusyId.value = linkId;
  actionError.value = null;
  try {
    await ocDeleteWorkItemLink(props.workItemId, linkId);
    emit('refresh');
  } catch (e: unknown) {
    actionError.value = ocExtractDgErrorMessage(e, t('operationCore.profile.relations.deleteError'));
  } finally {
    deleteBusyId.value = null;
  }
}

function onLinked() {
  emit('refresh');
}
</script>

<template>
  <v-card v-if="visible" variant="outlined" class="rounded-lg mb-4">
    <v-card-text class="pa-4">
      <div class="d-flex align-center mb-2">
        <div class="text-subtitle-2 font-weight-bold">
          {{ t('operationCore.profile.relations.title') }}
        </div>
        <v-spacer />
        <v-btn
          v-if="canEdit"
          size="small"
          variant="tonal"
          color="primary"
          prepend-icon="mdi-link-plus"
          @click="linkDialogOpen = true"
        >
          {{ t('operationCore.profile.relations.linkAction') }}
        </v-btn>
      </div>

      <v-alert v-if="actionError" type="error" variant="tonal" density="compact" class="mb-3">
        {{ actionError }}
      </v-alert>

      <div v-if="parent" class="mb-3">
        <div class="text-caption text-medium-emphasis mb-1">
          {{ t('operationCore.profile.relations.parent') }}
        </div>
        <NuxtLink :to="profilePath(parent)" class="text-primary text-decoration-none text-body-2">
          <span class="font-weight-medium">{{ parent.key }}</span>
          <span v-if="parent.title" class="text-medium-emphasis"> — {{ parent.title }}</span>
        </NuxtLink>
      </div>

      <div v-if="children?.length" class="mb-3">
        <div class="text-caption text-medium-emphasis mb-1">
          {{ t('operationCore.profile.relations.children') }}
        </div>
        <div v-for="child in children" :key="child.id" class="oc-relation-row">
          <NuxtLink :to="profilePath(child)" class="text-primary text-decoration-none text-body-2">
            <span class="font-weight-medium">{{ child.key }}</span>
            <span v-if="child.title" class="text-medium-emphasis"> — {{ child.title }}</span>
          </NuxtLink>
        </div>
      </div>

      <div v-if="hasLinks">
        <div v-if="hasTree" class="text-caption text-medium-emphasis mb-1">
          {{ t('operationCore.profile.relations.linksSection') }}
        </div>
        <div v-for="link in links" :key="link.id" class="oc-relation-row d-flex align-center ga-2">
          <div class="flex-grow-1 min-width-0">
            <NuxtLink
              :to="profilePath(link, link.otherWorkItemId)"
              class="text-primary text-decoration-none text-body-2 d-block text-truncate"
            >
              {{ linkLabel(link) }}
            </NuxtLink>
            <div v-if="linkTitle(link)" class="text-caption text-medium-emphasis text-truncate">
              {{ linkTitle(link) }}
            </div>
          </div>
          <v-btn
            v-if="canEdit"
            icon="mdi-link-off"
            variant="text"
            size="x-small"
            color="medium-emphasis"
            :loading="deleteBusyId === link.id"
            :title="t('operationCore.profile.relations.unlink')"
            @click="removeLink(link)"
          />
        </div>
      </div>

      <div
        v-if="!hasTree && !hasLinks && canEdit"
        class="text-caption text-medium-emphasis"
      >
        {{ t('operationCore.profile.relations.empty') }}
      </div>
    </v-card-text>

    <OcLinkWorkItemDialog
      v-model="linkDialogOpen"
      :work-item-id="workItemId"
      :workspace-id="workspaceId"
      :existing-links="links"
      @linked="onLinked"
    />
  </v-card>
</template>

<style scoped>
.oc-relation-row + .oc-relation-row {
  margin-top: 0.35rem;
}

.min-width-0 {
  min-width: 0;
}
</style>
