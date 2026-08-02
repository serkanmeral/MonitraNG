import { defineEventHandler, getQuery, getMethod, getRouterParam, getRequestHeader, readBody } from 'h3';
import { getCookie } from 'h3';

/**
 * Dev/prod BFF → MngLogCollector (direct :5091).
 * Requires UI session cookie; forwards optional ingest API key for agent-gated routes.
 * GET/HEAD always; POST discovery/sync|scan*|hosts/clear; POST/PUT/DELETE policy/eventlog-packages*.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const method = getMethod(event);
  const path = getRouterParam(event, 'path') || '';
  const pathLower = path.toLowerCase();

  const token = getCookie(event, 'access_token');
  if (!token) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized: Token bulunamadı',
    });
  }

  const isEventLogPolicyWrite =
    pathLower.includes('policy/eventlog-packages')
    && (method === 'POST' || method === 'PUT' || method === 'DELETE');

  const isDiscoverySync = method === 'POST' && pathLower.includes('discovery/sync');
  const isDiscoveryScanWrite =
    method === 'POST'
    && (pathLower.includes('discovery/scan') || pathLower.endsWith('discovery/scan'));
  const isDiscoveryClear =
    method === 'POST'
    && (pathLower.includes('discovery/hosts/clear') || pathLower.endsWith('discovery/hosts/clear'));

  const allowed =
    method === 'GET'
    || method === 'HEAD'
    || isDiscoverySync
    || isDiscoveryScanWrite
    || isDiscoveryClear
    || isEventLogPolicyWrite;

  if (!allowed) {
    throw createError({
      statusCode: 405,
      statusMessage: 'Method Not Allowed',
    });
  }

  const base = (config.serverLogCollectorUrl as string | undefined)?.replace(/\/$/, '');
  if (!base) {
    throw createError({
      statusCode: 500,
      statusMessage: 'LogCollector URL yapılandırılmamış (LOGCOLLECTOR_URL / ODAK_HOST)',
    });
  }

  const fullUrl = `${base}/api/${path}`;
  const query = getQuery(event);
  const queryString = new URLSearchParams(query as Record<string, string>).toString();
  const urlWithQuery = queryString ? `${fullUrl}?${queryString}` : fullUrl;

  const headers: Record<string, string> = {
    Accept: 'application/json',
  };

  const apiKey = (config.logCollectorIngestApiKey as string | undefined)?.trim();
  if (apiKey) {
    headers['X-MngLogs-ApiKey'] = apiKey;
  }

  const domainName = getRequestHeader(event, 'x-domain-name');
  if (domainName) {
    headers['X-Domain-Name'] = domainName;
  }

  const ifNoneMatch = getRequestHeader(event, 'if-none-match');
  if (ifNoneMatch) {
    headers['If-None-Match'] = ifNoneMatch;
  }

  const hasBody = method === 'POST' || method === 'PUT';
  let body: string | undefined;
  if (hasBody) {
    headers['Content-Type'] = 'application/json';
    const raw = await readBody(event);
    // Always forward a JSON string — avoids ofetch serializing quirks with pre-set Content-Type.
    body = typeof raw === 'string' ? raw : JSON.stringify(raw ?? {});
  }

  try {
    return await $fetch(urlWithQuery, {
      method: method as 'GET' | 'HEAD' | 'POST' | 'PUT' | 'DELETE',
      headers,
      body,
    });
  } catch (error: any) {
    const status = error.statusCode || error.status || 500;
    throw createError({
      statusCode: status,
      statusMessage:
        error.statusMessage || error.message || 'MngLogCollector API hatası',
      data: error.data,
    });
  }
});
