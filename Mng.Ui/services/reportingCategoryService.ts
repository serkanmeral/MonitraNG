import type { DiTreeNode } from '@/types/apps/documentIntelligence';
import type {
  ReportingCategory,
  ReportingCreateCategoryRequest,
  ReportingRenameCategoryRequest,
} from '@/types/apps/reporting';
import {
  buildReportingCategoryTree,
  reportingCategoryHasChild,
} from '@/utils/reportingCategoryTree';
import {
  createReportingCategoryRecord,
  loadReportingCategories,
  renameReportingCategoryRecord,
  saveReportingCategories,
} from '@/utils/reportingCategoryStorage';
import { reportingDomainKey } from '@/services/reportingCatalogService';

export class ReportingCategoryService {
  constructor(private readonly domainKey: string) {}

  list(): ReportingCategory[] {
    return loadReportingCategories(this.domainKey);
  }

  getTree(): DiTreeNode[] {
    return buildReportingCategoryTree(this.list());
  }

  getById(id: string): ReportingCategory | undefined {
    return this.list().find((c) => c.id === id);
  }

  create(request: ReportingCreateCategoryRequest, createdBy?: string | null): ReportingCategory {
    const categories = this.list();
    const created = createReportingCategoryRecord(categories, {
      name: request.name,
      description: request.description,
      parentId: request.parentId ?? null,
      createdBy,
    });
    categories.push(created);
    saveReportingCategories(this.domainKey, categories);
    return created;
  }

  rename(id: string, request: ReportingRenameCategoryRequest): ReportingCategory {
    const categories = this.list();
    const updated = renameReportingCategoryRecord(categories, id, request.name);
    saveReportingCategories(this.domainKey, categories);
    return updated;
  }

  delete(id: string, hasReportsInCategory: (categoryId: string) => boolean): void {
    const categories = this.list();
    if (!categories.some((c) => c.id === id)) throw new Error('Category not found');
    if (reportingCategoryHasChild(categories, id)) {
      throw new Error('CATEGORY_HAS_CHILDREN');
    }
    if (hasReportsInCategory(id)) {
      throw new Error('CATEGORY_HAS_REPORTS');
    }
    saveReportingCategories(
      this.domainKey,
      categories.filter((c) => c.id !== id)
    );
  }
}

export function createReportingCategoryService(
  domainId?: string | null,
  domainName?: string | null
): ReportingCategoryService {
  return new ReportingCategoryService(reportingDomainKey(domainId, domainName));
}
