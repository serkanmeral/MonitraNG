import { defineEventHandler, getQuery, getMethod, getRouterParam } from 'h3';
import { getCookie } from 'h3';

/**
 * Reactor API proxy. Forwards requests to MngReactor (config.public.reactorUrl)
 * with the user's access token. Used e.g. for GET /api/v1/engine/config-string?engineId=...
 * Health endpoint (v1/health) token gerektirmez – Reactor [AllowAnonymous].
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const method = getMethod(event);
  const path = getRouterParam(event, 'path') || '';

  const baseUrl = config.public.gatewayUrl
    ? `${config.public.gatewayUrl}/reactor`
    : (config.public.reactorUrl || 'https://localhost:5010');

  const token = getCookie(event, 'access_token');
  const isHealthEndpoint = /^v\d+\/health(\/|$)/.test(path);

  if (!token && !isHealthEndpoint) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized: Token bulunamadı',
    });
  }

  const query = getQuery(event);
  const queryString = new URLSearchParams(query as Record<string, string>).toString();
  const fullUrl = queryString ? `${baseUrl}/api/${path}?${queryString}` : `${baseUrl}/api/${path}`;

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  if (process.env.NODE_ENV === 'development') {
    process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
  }

  try {
    const response = await $fetch.raw(fullUrl, {
      method: method as any,
      headers,
    });
    return response._data;
  } catch (error: any) {
    if (error.statusCode) throw error;
    throw createError({
      statusCode: error.status || 502,
      statusMessage: error.message || 'Reactor request failed',
      data: error.data || error,
    });
  } finally {
    if (process.env.NODE_ENV === 'development') {
      delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    }
  }
});
