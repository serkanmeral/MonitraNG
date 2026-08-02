export interface EventLogPackageDto {
  name: string;
  channel: string;
  eventIds: number[];
}

export interface EventLogPackageCatalogResponse {
  version: string;
  source: string;
  generatedUtc: string;
  packages: EventLogPackageDto[];
  optionalPackages: EventLogPackageDto[];
}

export interface EventLogPackageManageItem {
  id: string;
  name: string;
  channel: string;
  eventIds: number[];
  isDefault: boolean;
  updatedAtUtc: string;
}

export interface EventLogPackageManageListResponse {
  version: string;
  publishedUtc: string | null;
  hasUnpublishedChanges: boolean;
  items: EventLogPackageManageItem[];
}

export interface EventLogPackageUpsertPayload {
  name: string;
  channel: string;
  eventIds: number[];
  isDefault: boolean;
}

export interface EventLogKnownId {
  id: number;
  label: string;
}

export interface EventLogChannelDictionary {
  channel: string;
  label: string;
  knownEventIds: EventLogKnownId[];
}

export interface EventLogPackagePreset {
  id: string;
  title: string;
  description: string;
  suggestedName: string;
  channel: string;
  isDefault: boolean;
  eventIds: number[];
}

export interface EventLogHostAssignment {
  hostname: string;
  hostKey: string;
  enabledOptionalPackages: string[];
  disabledServerPackages: string[];
  updatedAtUtc: string | null;
}

export interface EventLogHostAssignmentUpsertPayload {
  enabledOptionalPackages: string[];
  disabledServerPackages: string[];
}
