import { defineEventHandler, readBody, getQuery, getMethod, getRouterParam } from 'h3';
import { getCookie } from 'h3';

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const method = getMethod(event);
  const path = getRouterParam(event, 'path') || '';
  
  // Get token from cookie
  const token = getCookie(event, 'access_token');
  
  if (!token) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized: Token bulunamadı',
    });
  }

  // Determine base URL (DataGateway base URL)
  const baseUrl = config.public.gatewayUrl 
    ? `${config.public.gatewayUrl}/data`
    : (config.public.reactorUrl || 'https://localhost:5010');

  // Path formatı: 'v1/data/@side_menu' (client'tan gelen path'ten '/api/' kısmı çıkarılmış)
  // DataGateway endpoint: '/api/v1/data/@side_menu'
  // Full URL: baseUrl + '/api/' + path
  const fullUrl = `${baseUrl}/api/${path}`;
  
  // Get query parameters
  const query = getQuery(event);
  const queryString = new URLSearchParams(query as Record<string, string>).toString();
  const urlWithQuery = queryString ? `${fullUrl}?${queryString}` : fullUrl;

  // Prepare headers
  const headers: Record<string, string> = {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
  };

  // Prepare request options
  const requestOptions: RequestInit = {
    method,
    headers,
  };

  // Add body for POST, PUT, DELETE
  if (method === 'POST' || method === 'PUT' || method === 'DELETE') {
    try {
      const body = await readBody(event);
      if (body) {
        requestOptions.body = JSON.stringify(body);
      }
    } catch (error) {
      // Body okunamadı, devam et (GET istekleri için normal)
    }
  }

  // Development için SSL sertifika doğrulamasını geçici olarak devre dışı bırak
  const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED;
  if (process.env.NODE_ENV === 'development') {
    process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
  }

  try {
    // Use $fetch which handles SSL properly with process.env.NODE_TLS_REJECT_UNAUTHORIZED
    let response: any;
    
    // DELETE işlemleri için 204 NoContent response'unu handle et
    if (method === 'DELETE') {
      try {
        const rawResponse = await $fetch.raw(urlWithQuery, {
          method: method as any,
          headers: requestOptions.headers as Record<string, string>,
          ...(requestOptions.body && { body: JSON.parse(requestOptions.body as string) }),
        });
        
        // 204 NoContent durumu
        if (rawResponse.status === 204) {
          return { success: true, statusCode: 204 };
        }
        
        response = rawResponse._data;
      } catch (fetchError: any) {
        throw fetchError;
      }
    } else {
      // GET, POST, PUT için normal $fetch kullan
      response = await $fetch(urlWithQuery, {
        method: method as any,
        headers: requestOptions.headers as Record<string, string>,
        ...(requestOptions.body && { body: JSON.parse(requestOptions.body as string) }),
      });
    }
    
    // Orijinal ayarı geri yükle
    if (originalRejectUnauthorized !== undefined) {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
    } else {
      delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    }

    return response;
  } catch (error: any) {
    // Orijinal ayarı geri yükle
    if (originalRejectUnauthorized !== undefined) {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
    } else {
      delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    }

    // If it's already an H3 error, rethrow it
    if (error.statusCode) {
      throw error;
    }

    // Handle $fetch errors
    if (error.data) {
      const errorData = error.data;
      if (typeof errorData === 'object') {
        throw createError({
          statusCode: error.statusCode || error.status || 500,
          statusMessage: errorData.errorDescription || errorData.error || errorData.message || error.message || 'DataGateway request failed',
          data: errorData,
        });
      }
    }

    // Otherwise, create a new error
    throw createError({
      statusCode: error.statusCode || error.status || 500,
      statusMessage: error.statusMessage || error.message || 'DataGateway request failed',
      data: error.data || error,
    });
  }
});
