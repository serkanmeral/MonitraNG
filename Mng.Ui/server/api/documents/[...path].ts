import { defineEventHandler, readBody, getQuery, getMethod, getRouterParam } from 'h3';
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
    ? `${gatewayUrl}/documents/api/${path}`
    : `http://localhost:5095/api/${path}`;

  const query = getQuery(event);
  const queryString = new URLSearchParams(query as Record<string, string>).toString();
  const urlWithQuery = queryString ? `${fullUrl}?${queryString}` : fullUrl;

  const headers: Record<string, string> = {
    Authorization: `Bearer ${token}`,
    'Content-Type': 'application/json',
  };

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
    if (method === 'DELETE') {
      try {
        const rawResponse = await $fetch.raw(urlWithQuery, {
          method: method as 'DELETE',
          headers: requestOptions.headers as Record<string, string>,
          ...(requestOptions.body && { body: JSON.parse(requestOptions.body as string) }),
        });
        if (rawResponse.status === 204) {
          return { success: true, statusCode: 204 };
        }
        return rawResponse._data;
      } catch (fetchError: any) {
        if (fetchError.statusCode === 204 || fetchError.response?.status === 204) {
          return { success: true, statusCode: 204 };
        }
        throw fetchError;
      }
    }

    return await $fetch(urlWithQuery, {
      method: method as 'GET' | 'POST' | 'PUT' | 'PATCH',
      headers: requestOptions.headers as Record<string, string>,
      ...(requestOptions.body && { body: JSON.parse(requestOptions.body as string) }),
    });
  } catch (error: any) {
    const statusCode = error.statusCode || error.status || 500;
    const statusMessage = error.statusMessage || error.message || 'MngDocument API hatası';
    let errorData = error.data;

    if (errorData && typeof errorData === 'object' && 'error' in errorData) {
      throw createError({
        statusCode,
        statusMessage,
        data: errorData,
      });
    }

    throw createError({
      statusCode,
      statusMessage,
      data: errorData || { error: statusMessage },
    });
  } finally {
    if (originalRejectUnauthorized !== undefined) {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
    } else {
      delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    }
  }
});
