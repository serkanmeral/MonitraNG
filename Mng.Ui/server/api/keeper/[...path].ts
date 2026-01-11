export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  // Use gateway URL if available, otherwise use direct keeper URL
  const keeperUrl = config.public.gatewayUrl 
    ? `${config.public.gatewayUrl}/keeper`
    : (config.public.keeperUrl || 'https://localhost:5001');
  
  // Get the path from the route
  const path = getRouterParam(event, 'path') || '';
  const method = getMethod(event);
  
  // Get request body if exists
  let body = null;
  if (method === 'POST' || method === 'PUT' || method === 'PATCH') {
    try {
      body = await readBody(event);
    } catch {
      // No body
    }
  }
  
  // Get query parameters
  const query = getQuery(event);
  const queryString = new URLSearchParams(query as Record<string, string>).toString();
  const url = queryString ? `${keeperUrl}/api/${path}?${queryString}` : `${keeperUrl}/api/${path}`;
  
  // Get authorization header from request
  const authHeader = getHeader(event, 'authorization');
  
  try {
    // Development için SSL sertifika doğrulamasını geçici olarak devre dışı bırak
    const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    if (process.env.NODE_ENV === 'development') {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
    }
    
    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
      };
      
      if (authHeader) {
        headers['Authorization'] = authHeader;
      }
      
      // Export endpoint'leri için binary response handling
      if (path.startsWith('group/export') || path.startsWith('user/export')) {
        try {
          // Binary response için responseType: 'arrayBuffer' kullan
          const rawResponse = await $fetch.raw(url, {
            method: method as any,
            headers,
            ...(body && { body }),
            responseType: 'arrayBuffer',
          });
          
          // Orijinal ayarı geri yükle
          if (originalRejectUnauthorized !== undefined) {
            process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
          } else {
            delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
          }
          
          // Binary response'u al (_data arrayBuffer içerir)
          const buffer = rawResponse._data;
          const contentType = rawResponse.headers.get('content-type') || 'application/octet-stream';
          const contentDisposition = rawResponse.headers.get('content-disposition') || '';
          
          // Response headers'ı ayarla
          setHeader(event, 'Content-Type', contentType);
          if (contentDisposition) {
            setHeader(event, 'Content-Disposition', contentDisposition);
          }
          
          // Nuxt'ta binary response döndürmek için Buffer kullan
          return Buffer.from(buffer);
        } catch (fetchError: any) {
          // Orijinal ayarı geri yükle
          if (originalRejectUnauthorized !== undefined) {
            process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
          } else {
            delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
          }
          throw fetchError;
        }
      }
      
      // $fetch kullan (undici tabanlı, SSL ayarlarını process.env'den alır)
      // 204 NoContent için özel handling: $fetch undefined döndürür, status code'u kontrol etmek için $fetch.raw kullan
      let response: any;
      
      // DELETE işlemleri için 204 NoContent response'unu handle etmek üzere $fetch.raw kullan
      if (method === 'DELETE') {
        try {
          const rawResponse = await $fetch.raw(url, {
            method: method as any,
            headers,
            ...(body && { body }),
          });
          
          // Orijinal ayarı geri yükle
          if (originalRejectUnauthorized !== undefined) {
            process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
          } else {
            delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
          }
          
          // 204 NoContent durumu
          if (rawResponse.status === 204) {
            // 204 NoContent için özel response döndür
            return { success: true, statusCode: 204 };
          }
          
          // Diğer başarılı durumlar (200-299)
          response = rawResponse._data;
        } catch (fetchError: any) {
          // Orijinal ayarı geri yükle
          if (originalRejectUnauthorized !== undefined) {
            process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
          } else {
            delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
          }
          throw fetchError;
        }
      } else {
        // GET, POST, PUT için normal $fetch kullan
        response = await $fetch(url, {
          method: method as any,
          headers,
          ...(body && { body }),
        });
        
        // Orijinal ayarı geri yükle
        if (originalRejectUnauthorized !== undefined) {
          process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
        } else {
          delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
        }
      }
      return response;
    } catch (fetchError: any) {
      // Orijinal ayarı geri yükle
      if (originalRejectUnauthorized !== undefined) {
        process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
      } else {
        delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
      }
      
      // Hata yanıtını düzgün formatla
      const statusCode = fetchError.statusCode || fetchError.response?.status || 500;
      const statusMessage = fetchError.statusMessage || fetchError.message || 'Request failed';
      const errorData = fetchError.data || fetchError.response?.data || fetchError;
      
      throw createError({
        statusCode,
        statusMessage,
        data: errorData,
      });
    }
  } catch (error: any) {
    // Eğer zaten createError ise, tekrar throw et
    if (error.statusCode) {
      throw error;
    }
    
    // Diğer hatalar için
    throw createError({
      statusCode: error.statusCode || 500,
      statusMessage: error.message || 'Request failed',
      data: error.data || error,
    });
  }
});

