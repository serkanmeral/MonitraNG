/**
 * Token endpoint test - HTTP Auth Config doğrulaması için.
 * Harici URL'lere istek sunucu tarafından yapılır (CORS bypass).
 */
import { defineEventHandler, readBody, getCookie } from 'h3';

export default defineEventHandler(async (event) => {
  const token = getCookie(event, 'access_token');
  if (!token) {
    throw createError({ statusCode: 401, statusMessage: 'Unauthorized' });
  }

  const body = await readBody(event);
  const { tokenUrl, tokenMethod, tokenBodyType, tokenBody } = body || {};

  if (!tokenUrl || typeof tokenUrl !== 'string') {
    throw createError({ statusCode: 400, statusMessage: 'tokenUrl gerekli' });
  }

  const url = (tokenUrl as string).trim();
  if (!url.startsWith('http://') && !url.startsWith('https://')) {
    throw createError({ statusCode: 400, statusMessage: 'Geçerli http/https URL girin' });
  }

  const method = (tokenMethod === 'GET' ? 'GET' : 'POST') as 'GET' | 'POST';
  const isForm = tokenBodyType === 'form';

  const headers: Record<string, string> = {
    Accept: 'application/json',
  };

  let reqBody: string | undefined;
  if (method === 'POST' && tokenBody && typeof tokenBody === 'object') {
    if (isForm) {
      headers['Content-Type'] = 'application/x-www-form-urlencoded';
      reqBody = new URLSearchParams(
        Object.entries(tokenBody).reduce((acc, [k, v]) => {
          acc[k] = String(v ?? '');
          return acc;
        }, {} as Record<string, string>)
      ).toString();
    } else {
      headers['Content-Type'] = 'application/json';
      reqBody = JSON.stringify(tokenBody);
    }
  }

  const originalReject = process.env.NODE_TLS_REJECT_UNAUTHORIZED;
  if (process.env.NODE_ENV === 'development') {
    process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
  }

  try {
    const res = await $fetch.raw<unknown>(url, {
      method,
      headers,
      body: reqBody,
      ignoreResponseError: true,
    });
    const status = res.status;
    const data = res._data;
    if (status >= 400) {
      return { __success: false, __error: `HTTP ${status}`, __status: status, __response: data };
    }
    return { __success: true, __response: data };
  } catch (err: any) {
    const data = err.data ?? err.response?._data;
    const status = err.statusCode ?? err.status ?? 0;
    return { __success: false, __error: err.message || String(err), __status: status, __response: data };
  } finally {
    if (process.env.NODE_ENV === 'development') {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalReject;
    }
  }
});
