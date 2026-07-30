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
