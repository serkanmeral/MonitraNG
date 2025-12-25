export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const keeperUrl = config.public.keeperUrl || 'https://localhost:5001';
  
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
  
  // Debug: Log request details
  if (process.env.NODE_ENV === 'development') {
    console.log('[Keeper Proxy] ========================================');
    console.log('[Keeper Proxy] Request Details:');
    console.log('[Keeper Proxy] Path:', path);
    console.log('[Keeper Proxy] Method:', method);
    console.log('[Keeper Proxy] Query:', query);
    console.log('[Keeper Proxy] Full URL:', url);
    console.log('[Keeper Proxy] Auth Header:', authHeader ? 'Present' : 'Missing');
    if (authHeader) {
      // Token'ın ilk 30 karakterini göster (güvenlik için tam token'ı loglamıyoruz)
      const tokenPreview = authHeader.substring(0, 30) + '...';
      console.log('[Keeper Proxy] Token Preview:', tokenPreview);
    }
    if (body) {
      console.log('[Keeper Proxy] Request Body:', JSON.stringify(body, null, 2));
    }
  }
  
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
      } else {
        console.warn('[Keeper Proxy] No authorization header found!');
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
            if (process.env.NODE_ENV === 'development' && path.startsWith('group')) {
              console.log('[Keeper Proxy] ✅ DELETE işlemi başarılı (204 NoContent)');
              console.log('[Keeper Proxy] Status:', rawResponse.status);
              console.log('[Keeper Proxy] Response: { success: true, statusCode: 204 }');
              console.log('[Keeper Proxy] ========================================');
            }
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
      
      // LOG: Response (özellikle group endpoint'i için)
      if (process.env.NODE_ENV === 'development' && path.startsWith('group')) {
        console.log('[Keeper Proxy] ✅ Response alındı:');
        console.log('[Keeper Proxy] Response Type:', typeof response);
        if (response && typeof response === 'object') {
          console.log('[Keeper Proxy] Response Keys:', Object.keys(response));
          // Response'u JSON olarak logla (büyük objeler için kısaltılmış)
          try {
            const responseStr = JSON.stringify(response, null, 2);
            // Çok uzunsa kısalt
            if (responseStr.length > 2000) {
              console.log('[Keeper Proxy] Response (truncated):', responseStr.substring(0, 2000) + '...');
            } else {
              console.log('[Keeper Proxy] Full Response:', responseStr);
            }
          } catch (e) {
            console.log('[Keeper Proxy] Response (JSON stringify failed):', response);
          }
        } else {
          console.log('[Keeper Proxy] Response:', response);
        }
      }
      
      console.log('[Keeper Proxy] ========================================');
      
      return response;
    } catch (fetchError: any) {
      // Orijinal ayarı geri yükle
      if (originalRejectUnauthorized !== undefined) {
        process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
      } else {
        delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
      }
      
      // LOG: Error
      if (process.env.NODE_ENV === 'development') {
        console.error('[Keeper Proxy] ❌ Error occurred:');
        console.error('[Keeper Proxy] Error Type:', typeof fetchError);
        console.error('[Keeper Proxy] Error Message:', fetchError.message);
        console.error('[Keeper Proxy] Status Code:', fetchError.statusCode || fetchError.response?.status);
        console.error('[Keeper Proxy] Status Message:', fetchError.statusMessage);
        console.error('[Keeper Proxy] Error Data:', fetchError.data || fetchError.response?.data);
        console.error('[Keeper Proxy] Full Error:', fetchError);
        console.log('[Keeper Proxy] ========================================');
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

