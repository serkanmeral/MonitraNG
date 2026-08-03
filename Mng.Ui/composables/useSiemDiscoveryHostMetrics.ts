import { secEventQuery } from '@/services/secEventService';
import type { SecEventListItem } from '@/types/apps/secEvent';
import type { SiemDiscoveryHost } from '@/types/apps/siemDiscovery';
import {
  preferredSecEventSearchTerm,
  secEventMatchesDiscoveryHost,
  shortHostKey,
} from '@/utils/siemDiscoveryHostMatch';

/** Metrics older than this are shown as stale in Discovery host modal. */
export const DISCOVERY_METRICS_STALE_MS = 5 * 60 * 1000;

export interface MetricPoint {
  at: number;
  value: number;
}

export interface DiscoveryDiskMetric {
  volume: string;
  /** Null when only total was seen (avoid fake 100% used). */
  freeBytes: number | null;
  totalBytes: number | null;
  at: number;
}

export interface DiscoveryDiskSeries {
  volume: string;
  /** Used percent 0–100 over time */
  series: MetricPoint[];
}

export interface DiscoveryTopProcess {
  name: string;
  pid?: number;
  cpuPercent?: number | null;
  workingSetBytes?: number | null;
}

export interface DiscoveryHostMetricsSnapshot {
  cpuPercent: number | null;
  cpuAt: number | null;
  cpuSeries: MetricPoint[];
  memoryAvailableBytes: number | null;
  memoryAvailableMb: number | null;
  /** Present when agent ships MemTotal / totalBytes with available. */
  memoryTotalBytes: number | null;
  memoryUsedBytes: number | null;
  memoryUsedPercent: number | null;
  memoryAt: number | null;
  /** @deprecated prefer memoryUsedSeries — kept for callers that chart available bytes */
  memorySeries: MetricPoint[];
  /** Used memory percent 0–100 over time (when total known). */
  memoryUsedSeries: MetricPoint[];
  disks: DiscoveryDiskMetric[];
  diskSeries: DiscoveryDiskSeries[];
  topCpu: DiscoveryTopProcess[];
  topMemory: DiscoveryTopProcess[];
  topAt: number | null;
  /** Max timestamp across all loaded metric events */
  freshestAt: number | null;
}

function fromHours(hours: number): string {
  return new Date(Date.now() - hours * 60 * 60 * 1000).toISOString();
}

function asNumber(v: unknown): number | null {
  if (typeof v === 'number' && Number.isFinite(v)) return v;
  if (typeof v === 'string' && v.trim()) {
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
  }
  return null;
}

function asString(v: unknown): string | null {
  if (typeof v === 'string') {
    const t = v.trim();
    return t || null;
  }
  if (typeof v === 'number' || typeof v === 'boolean') return String(v);
  return null;
}

function itemTs(item: SecEventListItem): number | null {
  const ms = Date.parse(item.timestamp || item.ingestedAt || '');
  return Number.isFinite(ms) ? ms : null;
}

function fieldMetric(item: SecEventListItem): string | null {
  return asString(item.fields?.metric);
}

function pickForHost(
  items: SecEventListItem[],
  hostname: string,
  host?: Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> | null,
): SecEventListItem[] {
  return (items ?? []).filter((i) => secEventMatchesDiscoveryHost(i, hostname, host));
}

function parseTopCpu(fields: Record<string, unknown> | null | undefined): DiscoveryTopProcess[] {
  const raw = fields?.processes;
  if (!Array.isArray(raw)) return [];
  const out: DiscoveryTopProcess[] = [];
  for (const row of raw) {
    if (!row || typeof row !== 'object') continue;
    const r = row as Record<string, unknown>;
    const name = asString(r.name);
    if (!name) continue;
    out.push({
      name,
      pid: asNumber(r.pid) ?? undefined,
      cpuPercent: asNumber(r.cpuPercent),
    });
  }
  return out.slice(0, 8);
}

function parseTopMemory(fields: Record<string, unknown> | null | undefined): DiscoveryTopProcess[] {
  const raw = fields?.processes;
  if (!Array.isArray(raw)) return [];
  const out: DiscoveryTopProcess[] = [];
  for (const row of raw) {
    if (!row || typeof row !== 'object') continue;
    const r = row as Record<string, unknown>;
    const name = asString(r.name);
    if (!name) continue;
    out.push({
      name,
      pid: asNumber(r.pid) ?? undefined,
      workingSetBytes: asNumber(r.workingSetBytes),
    });
  }
  return out.slice(0, 8);
}

function buildDisks(items: SecEventListItem[]): DiscoveryDiskMetric[] {
  const byVol = new Map<string, DiscoveryDiskMetric>();
  for (const item of items) {
    const metric = fieldMetric(item);
    const ts = itemTs(item);
    if (ts == null) continue;
    const volume = asString(item.fields?.volume) || '?';
    if (metric === 'disk.free_bytes') {
      const free = asNumber(item.fields?.value);
      if (free == null) continue;
      const total = asNumber(item.fields?.totalBytes);
      const prev = byVol.get(volume);
      // Same-timestamp batches often emit total then free — must merge on ts === prev.at.
      if (!prev || ts >= prev.at) {
        byVol.set(volume, {
          volume,
          freeBytes: free,
          totalBytes: total ?? prev?.totalBytes ?? null,
          at: ts,
        });
      }
    } else if (metric === 'disk.total_bytes') {
      const total = asNumber(item.fields?.value);
      if (total == null) continue;
      const prev = byVol.get(volume);
      if (!prev) {
        // Total-only — free stays unknown until a free_bytes sample merges in.
        byVol.set(volume, { volume, freeBytes: null, totalBytes: total, at: ts });
      } else {
        // Enrich total only; never clear freeBytes (same-ms total/free batches).
        byVol.set(volume, {
          ...prev,
          totalBytes: total,
        });
      }
    }
  }
  return [...byVol.values()]
    .filter((d) => d.freeBytes != null && d.totalBytes != null && d.totalBytes > 0)
    .sort((a, b) => a.volume.localeCompare(b.volume));
}

/** Newest-first API items → ascending sparkline points (last maxPoints). */
function buildValueSeries(
  items: SecEventListItem[],
  metricName: string,
  maxPoints = 40,
): MetricPoint[] {
  const points: MetricPoint[] = [];
  for (const item of items) {
    if (fieldMetric(item) !== metricName) continue;
    const at = itemTs(item);
    const value = asNumber(item.fields?.value);
    if (at == null || value == null) continue;
    points.push({ at, value });
  }
  points.sort((a, b) => a.at - b.at);
  const deduped: MetricPoint[] = [];
  for (const p of points) {
    const last = deduped[deduped.length - 1];
    if (last && last.at === p.at) deduped[deduped.length - 1] = p;
    else deduped.push(p);
  }
  return deduped.slice(-maxPoints);
}

function buildDiskUsedSeries(items: SecEventListItem[], maxPoints = 40): DiscoveryDiskSeries[] {
  const byVol = new Map<string, MetricPoint[]>();
  for (const item of items) {
    if (fieldMetric(item) !== 'disk.free_bytes') continue;
    const at = itemTs(item);
    const free = asNumber(item.fields?.value);
    const total = asNumber(item.fields?.totalBytes);
    const volume = asString(item.fields?.volume) || '?';
    if (at == null || free == null || total == null || total <= 0) continue;
    const usedPct = Math.max(0, Math.min(100, ((total - free) / total) * 100));
    const list = byVol.get(volume) ?? [];
    list.push({ at, value: Math.round(usedPct * 10) / 10 });
    byVol.set(volume, list);
  }

  const out: DiscoveryDiskSeries[] = [];
  for (const [volume, pts] of byVol) {
    pts.sort((a, b) => a.at - b.at);
    const deduped: MetricPoint[] = [];
    for (const p of pts) {
      const last = deduped[deduped.length - 1];
      if (last && last.at === p.at) deduped[deduped.length - 1] = p;
      else deduped.push(p);
    }
    out.push({ volume, series: deduped.slice(-maxPoints) });
  }
  return out.sort((a, b) => a.volume.localeCompare(b.volume));
}

function emptySnapshot(): DiscoveryHostMetricsSnapshot {
  return {
    cpuPercent: null,
    cpuAt: null,
    cpuSeries: [],
    memoryAvailableBytes: null,
    memoryAvailableMb: null,
    memoryTotalBytes: null,
    memoryUsedBytes: null,
    memoryUsedPercent: null,
    memoryAt: null,
    memorySeries: [],
    memoryUsedSeries: [],
    disks: [],
    diskSeries: [],
    topCpu: [],
    topMemory: [],
    topAt: null,
    freshestAt: null,
  };
}

function latestMemoryTotalBytes(items: SecEventListItem[]): number | null {
  let best: { at: number; total: number } | null = null;
  for (const item of items) {
    if (fieldMetric(item) !== 'memory.available_bytes') continue;
    const total = asNumber(item.fields?.totalBytes);
    const at = itemTs(item);
    if (total == null || total <= 0 || at == null) continue;
    if (!best || at > best.at) best = { at, total };
  }
  return best?.total ?? null;
}

function buildMemoryUsedSeries(
  availableSeries: MetricPoint[],
  totalBytes: number | null,
): MetricPoint[] {
  if (totalBytes == null || totalBytes <= 0) return [];
  return availableSeries.map((p) => ({
    at: p.at,
    value: Math.max(0, Math.min(100, Math.round(((totalBytes - p.value) / totalBytes) * 1000) / 10)),
  }));
}

/**
 * Load host resource + top-process metrics (+ series) for Discovery / Host Analytics.
 */
export async function fetchDiscoveryHostMetrics(
  hostname: string,
  options?: {
    from?: string;
    to?: string;
    maxPoints?: number;
    limit?: number;
    host?: Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> | null;
  },
): Promise<DiscoveryHostMetricsSnapshot> {
  const hostName = hostname.trim();
  if (!hostName) return emptySnapshot();

  const from = options?.from || fromHours(2);
  const to = options?.to;
  const maxPoints = options?.maxPoints ?? 40;
  const limit = options?.limit ?? Math.max(60, maxPoints + 20);
  const hostHints = options?.host ?? { hostname: hostName, ip: hostName, agent: null };
  const search = preferredSecEventSearchTerm(hostName, hostHints);
  const base = {
    from,
    to,
    sourceType: 'metric' as const,
    excludeUnknown: false,
    search,
  };

  const [cpuRes, memRes, diskRes, topCpuRes, topMemRes] = await Promise.all([
    secEventQuery({ ...base, eventAction: 'host.cpu', limit }),
    secEventQuery({ ...base, eventAction: 'host.memory', limit: Math.max(limit, 80) }),
    secEventQuery({ ...base, eventAction: 'host.disk', limit: Math.max(limit, 120) }),
    secEventQuery({ ...base, eventAction: 'process.top_cpu', limit: 10 }),
    secEventQuery({ ...base, eventAction: 'process.top_memory', limit: 10 }),
  ]);

  const cpuItems = pickForHost(cpuRes.items, hostName, hostHints);
  const memItems = pickForHost(memRes.items, hostName, hostHints);
  const diskItems = pickForHost(diskRes.items, hostName, hostHints);
  const topCpuItems = pickForHost(topCpuRes.items, hostName, hostHints);
  const topMemItems = pickForHost(topMemRes.items, hostName, hostHints);

  const snap = emptySnapshot();
  const times: number[] = [];

  snap.cpuSeries = buildValueSeries(cpuItems, 'cpu.percent', maxPoints);
  if (snap.cpuSeries.length) {
    const last = snap.cpuSeries[snap.cpuSeries.length - 1]!;
    snap.cpuPercent = last.value;
    snap.cpuAt = last.at;
    times.push(last.at);
  }

  snap.memorySeries = buildValueSeries(memItems, 'memory.available_bytes', maxPoints);
  snap.memoryTotalBytes = latestMemoryTotalBytes(memItems);
  if (snap.memorySeries.length) {
    const last = snap.memorySeries[snap.memorySeries.length - 1]!;
    snap.memoryAvailableBytes = last.value;
    snap.memoryAvailableMb = Math.round(last.value / (1024 * 1024));
    snap.memoryAt = last.at;
    times.push(last.at);
  } else {
    const memHit = memItems.find((i) => fieldMetric(i) === 'memory.available_bytes') || null;
    if (memHit) {
      snap.memoryAvailableBytes = asNumber(memHit.fields?.value);
      snap.memoryAvailableMb =
        asNumber(memHit.fields?.availableMb)
        ?? (snap.memoryAvailableBytes != null
          ? Math.round(snap.memoryAvailableBytes / (1024 * 1024))
          : null);
      if (snap.memoryTotalBytes == null) {
        snap.memoryTotalBytes = asNumber(memHit.fields?.totalBytes);
      }
      snap.memoryAt = itemTs(memHit);
      if (snap.memoryAt != null) times.push(snap.memoryAt);
    }
  }
  if (
    snap.memoryAvailableBytes != null
    && snap.memoryTotalBytes != null
    && snap.memoryTotalBytes > 0
  ) {
    snap.memoryUsedBytes = Math.max(0, snap.memoryTotalBytes - snap.memoryAvailableBytes);
    snap.memoryUsedPercent = memoryUsedPercent(
      snap.memoryAvailableBytes,
      snap.memoryTotalBytes,
    );
  }
  snap.memoryUsedSeries = buildMemoryUsedSeries(snap.memorySeries, snap.memoryTotalBytes);

  snap.disks = buildDisks(diskItems);
  for (const d of snap.disks) times.push(d.at);
  snap.diskSeries = buildDiskUsedSeries(diskItems, maxPoints);

  const topCpuHit = topCpuItems[0] || null;
  if (topCpuHit) {
    snap.topCpu = parseTopCpu(topCpuHit.fields);
    snap.topAt = itemTs(topCpuHit);
    if (snap.topAt != null) times.push(snap.topAt);
  }

  const topMemHit = topMemItems[0] || null;
  if (topMemHit) {
    snap.topMemory = parseTopMemory(topMemHit.fields);
    const t = itemTs(topMemHit);
    if (t != null) {
      times.push(t);
      if (snap.topAt == null || t > snap.topAt) snap.topAt = t;
    }
  }

  snap.freshestAt = times.length ? Math.max(...times) : null;
  return snap;
}

export function hostMetricsEventsLink(
  hostname: string,
  host?: Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> | null,
): string {
  const q = new URLSearchParams();
  q.set('sourceType', 'metric');
  q.set('timeRange', '24h');
  const term = preferredSecEventSearchTerm(hostname, host ?? { hostname, ip: hostname, agent: null });
  if (term) q.set('search', term);
  return `/apps/siem-center/events?${q.toString()}`;
}

export function formatBytes(bytes: number | null | undefined, locale = 'tr-TR'): string {
  if (bytes == null || !Number.isFinite(bytes) || bytes < 0) return '—';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let n = bytes;
  let i = 0;
  while (n >= 1024 && i < units.length - 1) {
    n /= 1024;
    i += 1;
  }
  const digits = i === 0 ? 0 : n >= 10 ? 1 : 2;
  return `${n.toLocaleString(locale, { maximumFractionDigits: digits })} ${units[i]}`;
}

export function diskUsedBytes(disk: DiscoveryDiskMetric): number | null {
  if (disk.freeBytes == null || disk.totalBytes == null || disk.totalBytes <= 0) return null;
  return Math.max(0, disk.totalBytes - disk.freeBytes);
}

export function diskUsedPercent(disk: DiscoveryDiskMetric): number | null {
  const used = diskUsedBytes(disk);
  if (used == null || disk.totalBytes == null || disk.totalBytes <= 0) return null;
  return Math.max(0, Math.min(100, Math.round((used / disk.totalBytes) * 1000) / 10));
}

export function memoryUsedPercent(
  availableBytes: number | null | undefined,
  totalBytes: number | null | undefined,
): number | null {
  if (availableBytes == null || totalBytes == null || totalBytes <= 0) return null;
  const used = totalBytes - availableBytes;
  return Math.max(0, Math.min(100, Math.round((used / totalBytes) * 1000) / 10));
}

export function primaryDisk(disks: DiscoveryDiskMetric[]): DiscoveryDiskMetric | null {
  if (!disks.length) return null;
  const withUsage = disks.filter((d) => diskUsedPercent(d) != null);
  const pool = withUsage.length ? withUsage : disks;
  const c = pool.find((d) => /^c:?$/i.test(d.volume.replace(/\\/g, '')));
  if (c) return c;
  const root = pool.find((d) => d.volume === '/' || d.volume === '');
  return root || pool[0] || null;
}
