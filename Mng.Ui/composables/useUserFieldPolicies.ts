import { computed, type Ref } from 'vue';
import type { User } from '@/stores/apps/user';
import {
  isDirectoryUser,
  isFieldEditable,
  userProvisioningSourceLabelKey,
} from '@/utils/userFieldPolicy';
import { provisioningSourceChipColor } from '@/utils/provisioningSourceUi';

/** Reactive helpers for a user row / viewingUser driven by API fieldPolicies. */
export function useUserFieldPolicies(user: Ref<User | null | undefined>) {
  const isDirectory = computed(() => isDirectoryUser(user.value));
  const capabilities = computed(() => user.value?.capabilities);

  const canManageGroups = computed(
    () => capabilities.value?.canManageGroups !== false && !isDirectory.value
  );
  const canDeactivate = computed(
    () => capabilities.value?.canDeactivate !== false && !isDirectory.value
  );
  const canDelete = computed(() => {
    if (capabilities.value?.canDelete === false) return false;
    return !isDirectory.value;
  });
  const canChangePassword = computed(
    () => capabilities.value?.canChangePassword !== false && !isDirectory.value
  );

  function fieldEditable(field: string, defaultWhenMissing = true): boolean {
    return isFieldEditable(user.value, field, defaultWhenMissing);
  }

  const sourceLabelKey = computed(() =>
    userProvisioningSourceLabelKey(user.value?.provisioningSource)
  );
  const sourceChipColor = computed(() =>
    provisioningSourceChipColor(user.value?.provisioningSource)
  );

  return {
    isDirectory,
    capabilities,
    canManageGroups,
    canDeactivate,
    canDelete,
    canChangePassword,
    fieldEditable,
    sourceLabelKey,
    sourceChipColor,
  };
}
