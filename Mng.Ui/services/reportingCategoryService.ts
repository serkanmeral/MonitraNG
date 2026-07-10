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
  renameReportingCategoryRecord,
} from '@/utils/reportingCategoryStorage';
import { reportingDomainKey } from '@/services/reportingCatalogService';
import {
  deleteCategoryFromDg,
  getReportingCatalogCache,
  hydrateReportingCatalog,
  upsertCategoryToDg,
} from '@/utils/reportingCatalogDg';

export class ReportingCategoryService {
  constructor(private readonly domainKey: string) {}

  async hydrate(): Promise<void> {
    await hydrateReportingCatalog(this.domainKey);
  }

  list(): ReportingCategory[] {
    return [...getReportingCatalogCache(this.domainKey).categories];
  }

  getTree(): DiTreeNode[] {
    return buildReportingCategoryTree(this.list());
  }

  getById(id: string): ReportingCategory | undefined {
    return this.list().find((c) => c.id === id);
  }

  async create(
    request: ReportingCreateCategoryRequest,
    createdBy?: string | null
  ): Promise<ReportingCategory> {
    await hydrateReportingCatalog(this.domainKey);
    const categories = this.list();
    const created = createReportingCategoryRecord(categories, {
      name: request.name,
      description: request.description,
      parentId: request.parentId ?? null,
      createdBy,
    });
    try {
      return await upsertCategoryToDg(this.domainKey, created);
    } catch (e) {
      const cache = getReportingCatalogCache(this.domainKey);
      cache.categories.push(created);
      console.warn('[reporting] create category DG failed, cached locally', e);
      return created;
    }
  }

  async rename(id: string, request: ReportingRenameCategoryRequest): Promise<ReportingCategory> {
    await hydrateReportingCatalog(this.domainKey);
    const categories = this.list();
    const updated = renameReportingCategoryRecord(categories, id, request.name);
    try {
      return await upsertCategoryToDg(this.domainKey, updated);
    } catch (e) {
      const cache = getReportingCatalogCache(this.domainKey);
      const idx = cache.categories.findIndex((c) => c.id === id);
      if (idx >= 0) cache.categories[idx] = updated;
      console.warn('[reporting] rename category DG failed, cached locally', e);
      return updated;
    }
  }

  async delete(id: string, hasReportsInCategory: (categoryId: string) => boolean): Promise<void> {
    await hydrateReportingCatalog(this.domainKey);
    const categories = this.list();
    if (!categories.some((c) => c.id === id)) throw new Error('Category not found');
    if (reportingCategoryHasChild(categories, id)) {
      throw new Error('CATEGORY_HAS_CHILDREN');
    }
    if (hasReportsInCategory(id)) {
      throw new Error('CATEGORY_HAS_REPORTS');
    }
    try {
      await deleteCategoryFromDg(this.domainKey, id);
    } catch (e) {
      const cache = getReportingCatalogCache(this.domainKey);
      cache.categories = cache.categories.filter((c) => c.id !== id);
      cache.categoryDataIds.delete(id);
      console.warn('[reporting] delete category DG failed, removed from cache', e);
    }
  }
}

export function createReportingCategoryService(
  domainId?: string | null,
  domainName?: string | null
): ReportingCategoryService {
  return new ReportingCategoryService(reportingDomainKey(domainId, domainName));
}
