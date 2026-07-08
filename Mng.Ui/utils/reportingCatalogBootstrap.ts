/** Domain katalog seed sağlayıcıları — modül-spesifik kod buraya import edilmez. */

export type ReportingCatalogSeedFn = (domainKey: string) => void;

const seedProviders: ReportingCatalogSeedFn[] = [];

export function registerReportingCatalogSeed(fn: ReportingCatalogSeedFn): void {
  seedProviders.push(fn);
}

export function bootstrapReportingCatalog(domainKey: string): void {
  const key = domainKey?.trim() || 'default';
  for (const seed of seedProviders) {
    seed(key);
  }
}
