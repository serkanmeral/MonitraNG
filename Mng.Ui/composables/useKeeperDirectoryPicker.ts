import type {
  KeeperDirectoryEntity,
  KeeperGroupValueKey,
} from '@/utils/keeperDirectoryPicker';
import {
  useKeeperGroupPicker,
  type KeeperGroupPickerApi,
  type KeeperGroupPickerOptions,
} from '@/composables/useKeeperGroupPicker';
import { useKeeperUserPicker, type KeeperUserPickerApi } from '@/composables/useKeeperUserPicker';

export type KeeperDirectoryPickerApi = KeeperUserPickerApi | KeeperGroupPickerApi;

export type KeeperDirectoryPickerOptions = {
  groupValueKey?: KeeperGroupValueKey;
};

export function useKeeperDirectoryPicker(entity: 'user'): KeeperUserPickerApi;
export function useKeeperDirectoryPicker(
  entity: 'group',
  options?: KeeperDirectoryPickerOptions
): KeeperGroupPickerApi;
export function useKeeperDirectoryPicker(
  entity: KeeperDirectoryEntity,
  options?: KeeperDirectoryPickerOptions
): KeeperDirectoryPickerApi {
  if (entity === 'user') return useKeeperUserPicker();
  const groupOptions: KeeperGroupPickerOptions = {
    valueKey: options?.groupValueKey ?? 'id',
  };
  return useKeeperGroupPicker(groupOptions);
}
