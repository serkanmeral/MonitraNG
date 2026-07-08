import type { ReportingCatalog, ReportingReportDefinition } from '@/types/apps/reporting';
import {
  createEmptyReportDefinition,
  loadReportingCatalog,
  saveReportingCatalog,
} from '@/utils/reportingCatalogStorage';

export class ReportingCatalogService {
  constructor(private readonly domainKey: string) {}

  load(): ReportingCatalog {
    return loadReportingCatalog(this.domainKey).catalog;
  }

  private persist(catalog: ReportingCatalog): void {
    saveReportingCatalog(this.domainKey, catalog);
  }

  listReports(): ReportingReportDefinition[] {
    return [...this.load().reports];
  }

  getReport(id: string): ReportingReportDefinition | undefined {
    return this.load().reports.find((r) => r.id === id);
  }

  saveReport(report: ReportingReportDefinition): ReportingReportDefinition {
    const catalog = this.load();
    const idx = catalog.reports.findIndex((r) => r.id === report.id);
    if (idx >= 0) catalog.reports[idx] = report;
    else catalog.reports.push(report);
    this.persist(catalog);
    return report;
  }

  deleteReport(id: string): void {
    const catalog = this.load();
    catalog.reports = catalog.reports.filter((r) => r.id !== id);
    this.persist(catalog);
  }

  createReport(title: string, categoryId: string | null): ReportingReportDefinition {
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
