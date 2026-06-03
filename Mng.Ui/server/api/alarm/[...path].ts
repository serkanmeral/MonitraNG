import { defineEventHandler, readBody, getQuery, getMethod, getRouterParam, getHeader } from 'h3';
import { getCookie } from 'h3';

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
    ? `${gatewayUrl}/alarm/api/${path}`
    : `http://localhost:5087/api/${path}`;

  const query = getQuery(event);
  const queryString = new URLSearchParams(query as Record<string, string>).toString();
  const urlWithQuery = queryString ? `${fullUrl}?${queryString}` : fullUrl;

  const domainName = getHeader(event, 'x-domain-name');
  const headers: Record<string, string> = {
    Authorization: `Bearer ${token}`,
    'Content-Type': 'application/json',
  };
  if (domainName) {
    headers['X-Domain-Name'] = domainName;
  }

  const requestOptions: RequestInit = {
    method,
    headers,
  };

  if (method === 'POST' || method === 'PUT' || method === 'PATCH' || method === 'DELETE') {
    try {
      const body = await readBody(event);
      if (body) {
        requestOptions.body = JSON.stringify(body);
      }
    } catch {
      // GET — body yok
    }
  }

  const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED;
  if (process.env.NODE_ENV === 'development') {
    process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
  }

  try {
    return await $fetch(urlWithQuery, {
      method: method as 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE',
      headers: requestOptions.headers as Record<string, string>,
      ...(requestOptions.body && { body: JSON.parse(requestOptions.body as string) }),
    });
  } catch (error: any) {
    throw createError({
      statusCode: error.statusCode || error.status || 500,
      statusMessage: error.statusMessage || error.message || 'MngAlarm API hatası',
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
