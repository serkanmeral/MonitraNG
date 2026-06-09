import type { H3Event } from 'h3';
import { getCookie, getHeader } from 'h3';
import type { SurfaceContext, ManifestDataBinding } from '@/types/apps/widgetManifest';
import type { Widget } from '@/stores/apps/widget';
import type { WidgetDataResponse } from '@/services/widgetDataService';
import {
  adaptWidgetForRuntime,
  parseQueryRef,
  resolveManifestBindingForFetch,
  type WidgetLike,
} from '@/utils/widgets/widgetManifestAdapter';
import { resolveManifestWidgetData } from '@/services/widgetManifestFetchCore';

interface GatewayFetchOptions {
  method?: 'GET' | 'POST';
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined>;
}

function buildGatewayHeaders(event: H3Event, token: string): Record<string, string> {
  const headers: Record<string, string> = {
    Authorization: `Bearer ${token}`,
    'Content-Type': 'application/json',
  };
  const domain = getHeader(event, 'x-domain-name') ?? getCookie(event, 'domain_name');
  if (domain) headers['X-Domain-Name'] = domain;
  return headers;
}

async function createGatewayTransport(event: H3Event) {
  const config = useRuntimeConfig();
  const gatewayUrl = config.public.gatewayUrl as string | undefined;
  const token = getCookie(event, 'access_token');
  if (!gatewayUrl) throw new Error('gatewayUrl yapılandırılmamış');
  if (!token) throw new Error('Unauthorized');

  return {
    async request(path: string, options: GatewayFetchOptions = {}) {
      const q = new URLSearchParams();
      if (options.query) {
        for (const [k, v] of Object.entries(options.query)) {
          if (v === undefined || v === null || v === '') continue;
          q.set(k, String(v));
        }
      }
      const qs = q.toString();
      const url = `${gatewayUrl}${path}${qs ? `?${qs}` : ''}`;
      return $fetch(url, {
        method: options.method ?? 'GET',
        headers: buildGatewayHeaders(event, token),
        body: options.body ? JSON.stringify(options.body) : undefined,
      });
    },
  };
}

export async function fetchDashboardWidgetsBatchOnServer(
  event: H3Event,
  widgets: Widget[],
  context: SurfaceContext = {},
): Promise<Record<string, WidgetDataResponse>> {
  const transport = await createGatewayTransport(event);
  const results = await Promise.allSettled(
    widgets.map(async (widget) => {
      const config = (widget.config ?? {}) as Record<string, unknown>;
      const id =
        widget.__dataId ??
        widget.dataId ??
        (config.templateId as string | undefined) ??
        widget.name ??
        '';
      if (!id) throw new Error('Widget id eksik');

      const adapted = adaptWidgetForRuntime(widget as WidgetLike, context);
      const binding = resolveManifestBindingForFetch(adapted, context);
      if (!binding || (binding.kind !== 'serviceRef' && binding.kind !== 'queryRef')) {
        throw new Error(`BFF batch: desteklenmeyen widget ${id}`);
      }
      const data = await resolveManifestWidgetData(binding, transport);
      return { id, data };
    }),
  );

  const out: Record<string, WidgetDataResponse> = {};
  for (const result of results) {
    if (result.status === 'fulfilled') {
      out[result.value.id] = result.value.data;
    }
  }
  return out;
}
