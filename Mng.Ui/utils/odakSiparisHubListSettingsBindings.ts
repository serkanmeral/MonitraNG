import type { OdakHubListColumnConfig, OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import type { OdakHubListSettingsScope } from '@/utils/odakSiparisHubSettingsService';

export interface OdakHubListSettingsTabBindings {
  scope: OdakHubListSettingsScope;
  hintKey: string;
  fieldLabel: (fieldName: string) => string;
  defaultConfig: () => OdakHubListConfig;
  mergeConfig: (saved: unknown) => OdakHubListConfig;
  loadConfig: () => Promise<{ config: OdakHubListConfig; rowId: string | null }>;
  saveConfig: (config: OdakHubListConfig, rowId: string | null) => Promise<string>;
}

export type { OdakHubListColumnConfig, OdakHubListConfig };
