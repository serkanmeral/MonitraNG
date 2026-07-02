import { ocListDatasetPage } from '@/services/operationCoreService';
import { ODAK_SIPARIS_CONFIG, type OdakCustomerRow, type OdakPackageRow } from '@/utils/odakSiparisConfig';
import { countOpenCapas, listCapasForPackages } from '@/utils/odakSiparisCapaService';
import { customerSektorLabel } from '@/utils/odakSiparisCustomerService';
import { listContactsForCustomer } from '@/utils/odakSiparisCustomerContactService';
import { listQualityReqsForCustomer } from '@/utils/odakSiparisCustomerQualityReqService';
import { countOpenNcrs, listNcrsForPackages } from '@/utils/odakSiparisNcrService';
import {
  aggregateLineQuantities,
  countCompletedShipments,
  listShipmentsForPackage,
  listShipmentLinesForPackage,
  sumShipmentLineQuantities,
} from '@/utils/odakSiparisShipmentService';
import {
  customerIdFromRow,
  customerLabelFromRow,
  fetchCustomerLabelMap,
  fetchPackageLineStatsMap,
  packageDataId,
  packageStatusLabel,
} from '@/utils/odakSiparisService';
import { listLinesForPackage } from '@/utils/odakSiparisLineService';

export interface OdakPackageDashboardMetrics {
  packageNo: string;
  name: string;
  status: string;
  statusLabel: string;
  customerLabel: string;
  lineCount: number;
  partCount: number;
  stockCount: number;
  shippedCount: number;
  remainingQuantity: number;
  fulfillmentPct: number | null;
  shipmentTotal: number;
  shipmentCompleted: number;
  openNcrCount: number;
  openCapaCount: number;
  deliveryDate: string | null;
  daysToDelivery: number | null;
  deliveryUrgency: 'ok' | 'soon' | 'overdue' | 'none';
  beginDate: string | null;
  closedAt: string | null;
}

export interface OdakGlobalDashboardMetrics {
  openCount: number;
  closedCount: number;
  totalCount: number;
  dueSoonCount: number;
  overdueCount: number;
  openNcrCount: number;
  openCapaCount: number;
  statusBreakdown: { open: number; closed: number };
  topCustomers: Array<{ customerId: string; label: string; count: number }>;
  upcomingDeliveries: Array<{
    packageId: string;
    packageNo: string;
    name: string;
    customerLabel: string;
    deliveryDate: string;
    daysLeft: number;
    status: string;
  }>;
}

export interface OdakCustomerDashboardMetrics {
  kod: string;
  unvan: string;
  sektorLabel: string;
  isActive: boolean;
  isCustomer: boolean;
  contactCount: number;
  qualityReqCount: number;
  packageTotal: number;
  packageOpen: number;
  packageClosed: number;
  dueSoonCount: number;
  overdueCount: number;
  openNcrCount: number;
  openCapaCount: number;
  recentPackages: Array<{
    packageId: string;
    packageNo: string;
    name: string;
    status: string;
    statusLabel: string;
    deliveryDate: string | null;
    daysLeft: number | null;
  }>;
}

export interface OdakGlobalCustomerDashboardMetrics {
  activeCustomers: number;
  inactiveCustomers: number;
  customersWithOpenPackages: number;
  totalOpenPackages: number;
  totalOverduePackages: number;
  sectorBreakdown: Array<{ sector: string; label: string; count: number }>;
  topCustomers: Array<{
    customerId: string;
    label: string;
    openCount: number;
    overdueCount: number;
  }>;
  atRiskCustomers: Array<{
    customerId: string;
    label: string;
    openCount: number;
    overdueCount: number;
  }>;
}

const MS_DAY = 86_400_000;

function parseDateOnly(v: unknown): Date | null {
  if (!v) return null;
  const d = new Date(String(v));
  return Number.isNaN(d.getTime()) ? null : d;
}

function startOfToday(): Date {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  return d;
}

export function daysUntilDelivery(deliveryDate: unknown): number | null {
  const d = parseDateOnly(deliveryDate);
  if (!d) return null;
  const today = startOfToday();
  return Math.ceil((d.getTime() - today.getTime()) / MS_DAY);
}

export function deliveryUrgency(
  status: unknown,
  daysLeft: number | null
): 'ok' | 'soon' | 'overdue' | 'none' {
  if (String(status) === 'closed') return 'none';
  if (daysLeft == null) return 'none';
  if (daysLeft < 0) return 'overdue';
  if (daysLeft <= 7) return 'soon';
  return 'ok';
}

function fulfillmentPercent(partCount: number, shippedCount: number): number | null {
  if (partCount <= 0) return null;
  return Math.min(100, Math.round((shippedCount / partCount) * 100));
}

export async function fetchPackageDashboardMetrics(
  packageId: string,
  seedRow?: OdakPackageRow | null,
  customerLabels?: Record<string, string>
): Promise<OdakPackageDashboardMetrics | null> {
  if (!packageId) return null;

  const labels = customerLabels ?? (await fetchCustomerLabelMap());
  const pkg = seedRow ?? null;
  const row = pkg as OdakPackageRow | null;

  const [lines, shipments, shipmentLines, ncrs, capas, lineStatsMap] = await Promise.all([
    listLinesForPackage(packageId),
    listShipmentsForPackage(packageId),
    listShipmentLinesForPackage(packageId),
    listNcrsForPackage(packageId),
    listCapasForPackage(packageId),
    fetchPackageLineStatsMap([packageId]),
  ]);

  const lineStats = lineStatsMap.get(packageId);
  const lineAggregate = aggregateLineQuantities(lines);
  const lineCount = row?.lineCount ?? lineStats?.lineCount ?? lines.length;
  const partCount = Number(row?.partCount) || 0;
  const stockCount = Number(row?.stockCount) || 0;
  const shippedFromLines = sumShipmentLineQuantities(shipmentLines);
  const shippedCount = Number(row?.shippedCount) || lineAggregate.totalShipped || shippedFromLines || 0;
  const remainingQuantity = lineAggregate.totalRemaining;

  const daysLeft = daysUntilDelivery(row?.deliveryDate);
  const status = String(row?.status ?? 'open');

  return {
    packageNo: row?.packageNo ?? '—',
    name: row?.name ?? '—',
    status,
    statusLabel: packageStatusLabel(status),
    customerLabel: row ? customerLabelFromRow(row, labels) : '—',
    lineCount: Number(lineCount) || 0,
    partCount,
    stockCount,
    shippedCount,
    remainingQuantity,
    fulfillmentPct: fulfillmentPercent(partCount, shippedCount),
    shipmentTotal: shipments.length,
    shipmentCompleted: countCompletedShipments(shipments),
    openNcrCount: countOpenNcrs(ncrs),
    openCapaCount: countOpenCapas(capas),
    deliveryDate: row?.deliveryDate ? String(row.deliveryDate) : null,
    daysToDelivery: daysLeft,
    deliveryUrgency: deliveryUrgency(status, daysLeft),
    beginDate: row?.beginDate ? String(row.beginDate) : null,
    closedAt: row?.closedAt ? String(row.closedAt) : null,
  };
}

async function countPackages(filter?: string): Promise<number> {
  const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.packagesDataset, {
    limit: 1,
    filter,
  });
  return resp.total ?? 0;
}

async function countDataset(dataset: string, filter?: string): Promise<number> {
  const resp = await ocListDatasetPage(dataset, { limit: 1, filter });
  return resp.total ?? 0;
}

export async function fetchGlobalDashboardMetrics(): Promise<OdakGlobalDashboardMetrics> {
  const [openCount, closedCount, customerLabels, openPackagesResp, openNcrCount, openCapaCount] =
    await Promise.all([
      countPackages('status:eq:open'),
      countPackages('status:eq:closed'),
      fetchCustomerLabelMap(),
      ocListDatasetPage(ODAK_SIPARIS_CONFIG.packagesDataset, {
        limit: 2000,
        sort: 'deliveryDate:asc',
        filter: 'status:eq:open',
      }),
      countDataset(ODAK_SIPARIS_CONFIG.ncrDataset).then(async (total) => {
        // Client-side open filter fallback when dataset is small enough
        if (total <= 500) {
          const all = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.ncrDataset, { limit: 500 });
          return countOpenNcrs((all.items ?? []) as Parameters<typeof countOpenNcrs>[0]);
        }
        return total;
      }),
      countDataset(ODAK_SIPARIS_CONFIG.capaDataset).then(async (total) => {
        if (total <= 500) {
          const all = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.capaDataset, { limit: 500 });
          return countOpenCapas((all.items ?? []) as Parameters<typeof countOpenCapas>[0]);
        }
        return total;
      }),
    ]);

  const openPackages = (openPackagesResp.items ?? []) as OdakPackageRow[];
  let dueSoonCount = 0;
  let overdueCount = 0;
  const customerCounts = new Map<string, { label: string; count: number }>();

  for (const pkg of openPackages) {
    const days = daysUntilDelivery(pkg.deliveryDate);
    if (days != null) {
      if (days < 0) overdueCount += 1;
      else if (days <= 7) dueSoonCount += 1;
    }
    const cid = customerIdFromRow(pkg);
    if (cid) {
      const label = customerLabelFromRow(pkg, customerLabels);
      const hit = customerCounts.get(cid);
      if (hit) hit.count += 1;
      else customerCounts.set(cid, { label, count: 1 });
    }
  }

  const topCustomers = [...customerCounts.entries()]
    .map(([customerId, v]) => ({ customerId, label: v.label, count: v.count }))
    .sort((a, b) => b.count - a.count)
    .slice(0, 8);

  const upcomingDeliveries = openPackages
    .map((pkg) => {
      const id = packageDataId(pkg);
      const daysLeft = daysUntilDelivery(pkg.deliveryDate);
      if (!id || daysLeft == null || !pkg.deliveryDate) return null;
      return {
        packageId: id,
        packageNo: pkg.packageNo ?? '—',
        name: pkg.name ?? '—',
        customerLabel: customerLabelFromRow(pkg, customerLabels),
        deliveryDate: String(pkg.deliveryDate),
        daysLeft,
        status: String(pkg.status ?? 'open'),
      };
    })
    .filter(Boolean)
    .sort((a, b) => a!.daysLeft - b!.daysLeft)
    .slice(0, 12) as OdakGlobalDashboardMetrics['upcomingDeliveries'];

  return {
    openCount,
    closedCount,
    totalCount: openCount + closedCount,
    dueSoonCount,
    overdueCount,
    openNcrCount,
    openCapaCount,
    statusBreakdown: { open: openCount, closed: closedCount },
    topCustomers,
    upcomingDeliveries,
  };
}

function packagesByCustomerFilter(customerId: string, status?: 'open' | 'closed'): string {
  const parts = [`customerId:eq:${customerId}`];
  if (status) parts.push(`status:eq:${status}`);
  return parts.join(',');
}

const QUALITY_COUNT_PACKAGE_CHUNK = 50;

async function countQualityAcrossPackages(packageIds: string[]): Promise<{ ncr: number; capa: number }> {
  const ids = [...new Set(packageIds.map((id) => id.trim()).filter(Boolean))];
  if (!ids.length) return { ncr: 0, capa: 0 };

  let ncr = 0;
  let capa = 0;

  for (let i = 0; i < ids.length; i += QUALITY_COUNT_PACKAGE_CHUNK) {
    const chunk = ids.slice(i, i + QUALITY_COUNT_PACKAGE_CHUNK);
    const [ncrs, capas] = await Promise.all([listNcrsForPackages(chunk), listCapasForPackages(chunk)]);
    ncr += countOpenNcrs(ncrs);
    capa += countOpenCapas(capas);
  }

  return { ncr, capa };
}

export async function fetchCustomerDashboardMetrics(
  customerId: string,
  seedRow?: OdakCustomerRow | null
): Promise<OdakCustomerDashboardMetrics | null> {
  if (!customerId) return null;

  const row = seedRow ?? null;
  const isCustomer = row?.isMusteri !== false;

  const [contacts, qualityReqs, packageTotal, packageOpen, packageClosed, openPackagesResp] =
    await Promise.all([
      listContactsForCustomer(customerId),
      listQualityReqsForCustomer(customerId, { activeOnly: false }),
      countPackages(packagesByCustomerFilter(customerId)),
      isCustomer ? countPackages(packagesByCustomerFilter(customerId, 'open')) : Promise.resolve(0),
      isCustomer ? countPackages(packagesByCustomerFilter(customerId, 'closed')) : Promise.resolve(0),
      isCustomer
        ? ocListDatasetPage(ODAK_SIPARIS_CONFIG.packagesDataset, {
            limit: 200,
            sort: '-__updatedAt',
            filter: packagesByCustomerFilter(customerId, 'open'),
          })
        : Promise.resolve({ items: [], total: 0 }),
    ]);

  const openPackages = (openPackagesResp.items ?? []) as OdakPackageRow[];
  let dueSoonCount = 0;
  let overdueCount = 0;

  for (const pkg of openPackages) {
    const days = daysUntilDelivery(pkg.deliveryDate);
    if (days == null) continue;
    if (days < 0) overdueCount += 1;
    else if (days <= 7) dueSoonCount += 1;
  }

  const packageIds = openPackages
    .map((p) => packageDataId(p))
    .filter(Boolean) as string[];
  const quality = isCustomer ? await countQualityAcrossPackages(packageIds) : { ncr: 0, capa: 0 };

  const recentPackages = [...openPackages]
    .slice(0, 8)
    .map((pkg) => {
      const id = packageDataId(pkg);
      if (!id) return null;
      const daysLeft = daysUntilDelivery(pkg.deliveryDate);
      const status = String(pkg.status ?? 'open');
      return {
        packageId: id,
        packageNo: pkg.packageNo ?? '—',
        name: pkg.name ?? '—',
        status,
        statusLabel: packageStatusLabel(status),
        deliveryDate: pkg.deliveryDate ? String(pkg.deliveryDate) : null,
        daysLeft,
      };
    })
    .filter(Boolean) as OdakCustomerDashboardMetrics['recentPackages'];

  return {
    kod: row?.kod ?? '—',
    unvan: row?.unvan ?? '—',
    sektorLabel: customerSektorLabel(row?.sektor),
    isActive: row?.aktif !== false,
    isCustomer,
    contactCount: contacts.length,
    qualityReqCount: qualityReqs.length,
    packageTotal,
    packageOpen,
    packageClosed,
    dueSoonCount,
    overdueCount,
    openNcrCount: quality.ncr,
    openCapaCount: quality.capa,
    recentPackages,
  };
}

export async function fetchGlobalCustomerDashboardMetrics(): Promise<OdakGlobalCustomerDashboardMetrics> {
  const [activeCustomers, inactiveCustomers, customerLabels, openPackagesResp, allCustomersResp] =
    await Promise.all([
      countDataset(ODAK_SIPARIS_CONFIG.customersDataset, 'isMusteri:eq:true,aktif:eq:true'),
      countDataset(ODAK_SIPARIS_CONFIG.customersDataset, 'isMusteri:eq:true,aktif:eq:false'),
      fetchCustomerLabelMap(),
      ocListDatasetPage(ODAK_SIPARIS_CONFIG.packagesDataset, {
        limit: 2000,
        filter: 'status:eq:open',
        sort: 'deliveryDate:asc',
      }),
      ocListDatasetPage(ODAK_SIPARIS_CONFIG.customersDataset, {
        limit: 2000,
        filter: 'isMusteri:eq:true',
        sort: 'unvan:asc',
      }),
    ]);

  const openPackages = (openPackagesResp.items ?? []) as OdakPackageRow[];
  const customers = (allCustomersResp.items ?? []) as OdakCustomerRow[];

  const sectorCounts = new Map<string, number>();
  for (const c of customers) {
    const key = c.sektor ? String(c.sektor) : 'diger';
    sectorCounts.set(key, (sectorCounts.get(key) ?? 0) + 1);
  }

  const sectorBreakdown = [...sectorCounts.entries()]
    .map(([sector, count]) => ({
      sector,
      label: customerSektorLabel(sector),
      count,
    }))
    .sort((a, b) => b.count - a.count);

  const byCustomer = new Map<
    string,
    { label: string; openCount: number; overdueCount: number }
  >();

  let totalOverduePackages = 0;

  for (const pkg of openPackages) {
    const cid = customerIdFromRow(pkg);
    if (!cid) continue;
    const label = customerLabelFromRow(pkg, customerLabels);
    const hit = byCustomer.get(cid) ?? { label, openCount: 0, overdueCount: 0 };
    hit.openCount += 1;
    const days = daysUntilDelivery(pkg.deliveryDate);
    if (days != null && days < 0) {
      hit.overdueCount += 1;
      totalOverduePackages += 1;
    }
    byCustomer.set(cid, hit);
  }

  const topCustomers = [...byCustomer.entries()]
    .map(([customerId, v]) => ({
      customerId,
      label: v.label,
      openCount: v.openCount,
      overdueCount: v.overdueCount,
    }))
    .sort((a, b) => b.openCount - a.openCount)
    .slice(0, 10);

  const atRiskCustomers = [...byCustomer.entries()]
    .filter(([, v]) => v.overdueCount > 0)
    .map(([customerId, v]) => ({
      customerId,
      label: v.label,
      openCount: v.openCount,
      overdueCount: v.overdueCount,
    }))
    .sort((a, b) => b.overdueCount - a.overdueCount)
    .slice(0, 10);

  return {
    activeCustomers,
    inactiveCustomers,
    customersWithOpenPackages: byCustomer.size,
    totalOpenPackages: openPackages.length,
    totalOverduePackages,
    sectorBreakdown,
    topCustomers,
    atRiskCustomers,
  };
}
