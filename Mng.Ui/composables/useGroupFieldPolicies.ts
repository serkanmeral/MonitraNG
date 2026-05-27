import { computed, type Ref } from 'vue';
import type { Group } from '@/stores/apps/group';
import {
  canDeleteGroup,
  canEditGroup,
  canManageGroupMembers,
  groupProvisioningSourceLabelKey,
  isDirectoryGroup,
} from '@/utils/groupFieldPolicy';
import { provisioningSourceChipColor } from '@/utils/provisioningSourceUi';

export function useGroupFieldPolicies(group: Ref<Group | null | undefined>) {
  const isDirectory = computed(() => isDirectoryGroup(group.value));
  const capabilities = computed(() => group.value?.capabilities);

  const canEdit = computed(() => canEditGroup(group.value));
  const canDelete = computed(() => canDeleteGroup(group.value));
  const canManageMembers = computed(() => canManageGroupMembers(group.value));

  const sourceLabelKey = computed(() =>
    groupProvisioningSourceLabelKey(group.value?.provisioningSource)
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
