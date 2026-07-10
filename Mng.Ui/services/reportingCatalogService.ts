import type { ReportingCatalog, ReportingReportDefinition } from '@/types/apps/reporting';
import {
  createEmptyReportDefinition,
} from '@/utils/reportingCatalogStorage';
import {
  deleteReportFromDg,
  getReportingCatalogCache,
  hydrateReportingCatalog,
  upsertReportToDg,
} from '@/utils/reportingCatalogDg';

export class ReportingCatalogService {
  constructor(private readonly domainKey: string) {}

  /** DG + migrate + cache. Sayfa/plugin girişinde await edilmeli. */
  async hydrate(): Promise<void> {
    await hydrateReportingCatalog(this.domainKey);
  }

  load(): ReportingCatalog {
    return { reports: [...getReportingCatalogCache(this.domainKey).reports] };
  }

  listReports(): ReportingReportDefinition[] {
    return [...getReportingCatalogCache(this.domainKey).reports];
  }

  getReport(id: string): ReportingReportDefinition | undefined {
    return getReportingCatalogCache(this.domainKey).reports.find((r) => r.id === id);
  }

  async saveReport(report: ReportingReportDefinition): Promise<ReportingReportDefinition> {
    await hydrateReportingCatalog(this.domainKey);
    try {
      return await upsertReportToDg(this.domainKey, report);
    } catch (e) {
      // Offline / DG hata: cache'e yaz (oturum içi)
      const cache = getReportingCatalogCache(this.domainKey);
      const idx = cache.reports.findIndex((r) => r.id === report.id);
      if (idx >= 0) cache.reports[idx] = report;
      else cache.reports.push(report);
      console.warn('[reporting] saveReport DG failed, cached locally', e);
      return report;
    }
  }

  async deleteReport(id: string): Promise<void> {
    await hydrateReportingCatalog(this.domainKey);
    try {
      await deleteReportFromDg(this.domainKey, id);
    } catch (e) {
      const cache = getReportingCatalogCache(this.domainKey);
      cache.reports = cache.reports.filter((r) => r.id !== id);
      cache.reportDataIds.delete(id);
      console.warn('[reporting] deleteReport DG failed, removed from cache', e);
    }
  }

  async createReport(title: string, categoryId: string | null): Promise<ReportingReportDefinition> {
    const report = createEmptyReportDefinition(title, categoryId);
    return this.saveReport(report);
  }

  reportsInCategory(categoryId: string): ReportingReportDefinition[] {
    return this.listReports().filter((r) => r.categoryId === categoryId);
  }

  hasReportsInCategory(categoryId: string): boolean {
    return this.reportsInCategory(categoryId).length > 0;
  }
}

export function reportingDomainKey(domainId?: string | null, domainName?: string | null): string {
  return domainId?.trim() || domainName?.trim() || 'default';
}
