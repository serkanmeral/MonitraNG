import { computed, type Ref } from 'vue';
import type { Group } from '@/stores/apps/group';
import {
  canDeleteGroup,
  canEditGroup,
  canManageGroupMembers,
  isDirectoryGroup,
  provisioningSourceChipColor,
  provisioningSourceLabelKey,
} from '@/utils/groupFieldPolicy';

export function useGroupFieldPolicies(group: Ref<Group | null | undefined>) {
  const isDirectory = computed(() => isDirectoryGroup(group.value));
  const capabilities = computed(() => group.value?.capabilities);

  const canEdit = computed(() => canEditGroup(group.value));
  const canDelete = computed(() => canDeleteGroup(group.value));
  const canManageMembers = computed(() => canManageGroupMembers(group.value));

  const sourceLabelKey = computed(() =>
    provisioningSourceLabelKey(group.value?.provisioningSource)
  );
  const sourceChipColor = computed(() =>
    provisioningSourceChipColor(group.value?.provisioningSource)
  );

  return {
    isDirectory,
    capabilities,
    canEdit,
    canDelete,
    canManageMembers,
    sourceLabelKey,
    sourceChipColor,
  };
}
