/** Domain katalog seed sağlayıcıları — modül-spesifik kod buraya import edilmez. */

import { hydrateReportingCatalog } from '@/utils/reportingCatalogDg';

export type ReportingCatalogSeedFn = (domainKey: string) => void | Promise<void>;

const seedProviders: ReportingCatalogSeedFn[] = [];

export function registerReportingCatalogSeed(fn: ReportingCatalogSeedFn): void {
  seedProviders.push(fn);
}

/** DG hydrate + kayıtlı seed'ler (idempotent). */
export async function bootstrapReportingCatalog(domainKey: string): Promise<void> {
  const key = domainKey?.trim() || 'default';
  await hydrateReportingCatalog(key);
  for (const seed of seedProviders) {
    await seed(key);
  }
}
