import { defineEventHandler, getQuery, getMethod, getRouterParam, getHeader, readBody, getRequestURL } from 'h3';
import { getCookie } from 'h3';

function buildForwardQueryString(event: Parameters<typeof getRequestURL>[0]): string {
  // Prefer the raw incoming query string so commas/special chars are not
  // re-parsed/re-serialized incorrectly by getQuery + URLSearchParams.
  try {
    const search = getRequestURL(event).search;
    if (search && search.length > 1) return search.startsWith('?') ? search.slice(1) : search;
  } catch {
    /* fall through */
  }

  const query = getQuery(event);
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null) continue;
    if (Array.isArray(value)) {
      // Re-join comma-split arrays (ufo may split CSV values).
      qs.set(key, value.map(String).join(','));
    } else {
      qs.set(key, String(value));
    }
  }
  return qs.toString();
}

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

  const gatewayUrl = config.public.gatewayUrl as string | undefined;
  const fullUrl = gatewayUrl
    ? `${gatewayUrl}/reactor/api/${path}`
    : `http://localhost:5003/api/${path}`;

  const queryString = buildForwardQueryString(event);
  const urlWithQuery = queryString ? `${fullUrl}?${queryString}` : fullUrl;

  const domainName = getHeader(event, 'x-domain-name');
  const headers: Record<string, string> = {
    Authorization: `Bearer ${token}`,
    Accept: 'application/json',
  };
  if (domainName) {
    headers['X-Domain-Name'] = domainName;
  }

  const hasBody = method === 'POST' || method === 'PUT' || method === 'PATCH';
  let body: string | undefined;
  if (hasBody) {
    headers['Content-Type'] = 'application/json';
    const raw = await readBody(event);
    body = typeof raw === 'string' ? raw : JSON.stringify(raw ?? {});
  }

  const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED;
  if (process.env.NODE_ENV === 'development') {
    process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
  }

  try {
    return await $fetch(urlWithQuery, {
      method: method as 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE',
      headers,
      body,
    });
  } catch (error: any) {
    throw createError({
      statusCode: error.statusCode || error.status || 500,
      statusMessage: error.statusMessage || error.message || 'MngReactor API hatası',
      data: error.data,
    });
  } finally {
    if (originalRejectUnauthorized !== undefined) {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
    } else {
      delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    }
  }
});
