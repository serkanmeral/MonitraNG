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
      // GET, POST, PUT için $fetch.raw kullanarak response header'larına eriş
      const rawResponse = await $fetch.raw(urlWithQuery, {
        method: method as any,
        headers: requestOptions.headers as Record<string, string>,
        ...(requestOptions.body && { body: JSON.parse(requestOptions.body as string) }),
      });
      
      response = rawResponse._data;
      
      // X-Total-Count header'ını response body'ye ekle (pagination için)
      const totalCountHeader = rawResponse.headers.get('x-total-count');
      if (totalCountHeader && Array.isArray(response)) {
        // Response array ise, totalCount'u response objesine ekle
        // Ancak response array olduğu için, wrapper objesi oluştur
        // Frontend'de response._totalCount veya response.totalCount olarak erişilebilir
        // Ama daha iyi: response header'ı event header'ına ekle
        setHeader(event, 'X-Total-Count', totalCountHeader);
      }
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
    // Preserve the full error structure from DataGateway
    if (error.data) {
      const errorData = error.data;
      if (typeof errorData === 'object') {
        // Preserve the full error structure: { success: false, error: { code, message, details } }
        throw createError({
          statusCode: error.statusCode || error.status || 500,
          statusMessage: errorData.error?.message || errorData.errorDescription || (typeof errorData.error === 'string' ? errorData.error : '') || errorData.message || error.message || 'DataGateway request failed',
          data: errorData, // Preserve full structure including error.error.details.innerException
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
