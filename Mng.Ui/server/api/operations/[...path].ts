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
    ? `${gatewayUrl}/operations/api/${path}`
    : `http://localhost:5086/api/${path}`;

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
    let errorData = error.data;

    const resolveUserMessage = (data: unknown, fallback: string): string => {
      if (!data || typeof data !== 'object') return fallback;
      const d = data as Record<string, unknown>;
      if (typeof d.messageTr === 'string' && d.messageTr.trim()) return d.messageTr.trim();
      if (typeof d.message === 'string' && d.message.trim()) {
        const m = d.message.trim();
        const lower = m.toLowerCase();
        if (lower !== 'bad request' && lower !== 'internal server error') return m;
      }
      const nestedError = d.error;
      if (nestedError && typeof nestedError === 'object') {
        const e = nestedError as Record<string, unknown>;
        if (typeof e.message === 'string' && e.message.trim()) return e.message.trim();
      }
      return fallback;
    };

    const fallbackStatus = error.statusMessage || error.message || 'MngOperations API hatası';
    const statusMessage = resolveUserMessage(errorData, fallbackStatus);

    if (errorData && typeof errorData === 'object' && 'error' in errorData) {
      throw createError({
        statusCode,
        statusMessage,
        message: statusMessage,
        data: errorData,
      });
    }

    throw createError({
      statusCode,
      statusMessage,
      message: statusMessage,
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
