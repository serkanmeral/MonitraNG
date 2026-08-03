import { defineEventHandler, readBody, getQuery, getMethod, getRouterParam, getHeader, getCookie } from 'h3';

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const method = getMethod(event);
  const path = getRouterParam(event, 'path') || '';

  const authHeader = getHeader(event, 'authorization');
  const bearerFromHeader =
    authHeader?.startsWith('Bearer ') ? authHeader.slice('Bearer '.length).trim() : null;
  const token = getCookie(event, 'access_token') || bearerFromHeader;
  if (!token) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized: Token bulunamadı',
    });
  }

  const gatewayUrl = config.public.gatewayUrl as string | undefined;
  const odakHost = process.env.ODAK_HOST?.trim() || '192.168.20.8';
  const fullUrl = gatewayUrl
    ? `${gatewayUrl}/notifier/api/${path}`
    : `http://${odakHost}:5070/api/${path}`;

  const query = getQuery(event);
  const queryString = new URLSearchParams(query as Record<string, string>).toString();
  const urlWithQuery = queryString ? `${fullUrl}?${queryString}` : fullUrl;

  const headers: Record<string, string> = {
    Authorization: `Bearer ${token}`,
    'Content-Type': 'application/json',
  };

  let body: unknown;
  if (method === 'POST' || method === 'PUT' || method === 'PATCH' || method === 'DELETE') {
    try {
      body = await readBody(event);
    } catch {
      body = undefined;
    }
  }

  const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED;
  if (process.env.NODE_ENV === 'development') {
    process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
  }

  try {
    return await $fetch(urlWithQuery, {
      method: method as 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE',
      headers,
      ...(body != null && { body }),
    });
  } catch (error: any) {
    const data = error?.data;
    const apiError =
      data && typeof data === 'object' && 'error' in data
        ? String((data as { error?: unknown }).error)
        : null;
    throw createError({
      statusCode: error.statusCode || error.status || 500,
      statusMessage: apiError || error.statusMessage || error.message || 'MngNotifier API hatası',
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
