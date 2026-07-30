import { defineEventHandler, getQuery, getMethod, getRouterParam, getRequestHeader } from 'h3';
import { getCookie } from 'h3';

/**
 * Dev/prod BFF → MngLogCollector (direct :5091).
 * Requires UI session cookie; forwards optional ingest API key for agent-gated routes.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const method = getMethod(event);
  const path = getRouterParam(event, 'path') || '';

  const token = getCookie(event, 'access_token');
  if (!token) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized: Token bulunamadı',
    });
  }

  if (method !== 'GET' && method !== 'HEAD') {
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

  const ifNoneMatch = getRequestHeader(event, 'if-none-match');
  if (ifNoneMatch) {
    headers['If-None-Match'] = ifNoneMatch;
  }

  try {
    return await $fetch(urlWithQuery, {
      method: method as 'GET' | 'HEAD',
      headers,
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
