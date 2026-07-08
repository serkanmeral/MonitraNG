/**
 * Modül-spesifik rapor seed kayıtları (Odak Eğitim vb.).
 * Generic raporlama bileşenleri yalnızca bootstrapReportingCatalog kullanır.
 */
import { registerReportingCatalogSeed, bootstrapReportingCatalog } from '@/utils/reportingCatalogBootstrap';
import { reportingDomainKey } from '@/services/reportingCatalogService';
import { ensureOdakEgitimReportingSeeds } from '@/utils/reportingOdakEgitimSeeds';

registerReportingCatalogSeed(ensureOdakEgitimReportingSeeds);

export default defineNuxtPlugin(() => {
  try {
    const raw = localStorage.getItem('userInfo');
    const userInfo = raw ? (JSON.parse(raw) as { domain_id?: string; domain_name?: string }) : null;
    bootstrapReportingCatalog(reportingDomainKey(userInfo?.domain_id, userInfo?.domain_name));
  } catch {
    bootstrapReportingCatalog('default');
  }
});
