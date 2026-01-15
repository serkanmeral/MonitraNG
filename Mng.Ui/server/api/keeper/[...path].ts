import { getCookie } from 'h3';

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  // Use gateway URL if available, otherwise use direct keeper URL
  // If gatewayUrl is set, backend is accessed through gateway at /keeper path
  // If keeperUrl is set, backend is accessed directly
  const useGateway = !!config.public.gatewayUrl;
  const backendBaseUrl = useGateway 
    ? `${config.public.gatewayUrl}/keeper`
    : (config.public.keeperUrl || 'https://localhost:5001');
  
  // Get the path from the route
  const path = getRouterParam(event, 'path') || '';
  const method = getMethod(event);
  
  // Get request body if exists
  let body = null;
  let isFormData = false;
  if (method === 'POST' || method === 'PUT' || method === 'PATCH') {
    try {
      // Check if request is multipart/form-data
      const contentType = getHeader(event, 'content-type') || '';
      console.log('[Nuxt Proxy] Content-Type:', contentType);
      console.log('[Nuxt Proxy] Method:', method);
      console.log('[Nuxt Proxy] Path:', path);
      
      if (contentType.includes('multipart/form-data')) {
        console.log('[Nuxt Proxy] Detected multipart/form-data, parsing...');
        // FormData için readMultipartFormData kullan
        const formData = await readMultipartFormData(event);
        console.log('[Nuxt Proxy] Parsed formData fields:', formData?.length || 0);
        if (formData && formData.length > 0) {
          // FormData'yı FormData object'ine çevir (backend'e göndermek için)
          const formDataObj = new FormData();
          for (const field of formData) {
            if (field.filename) {
              // File field
              const blob = new Blob([field.data], { type: field.type || 'application/octet-stream' });
              formDataObj.append(field.name, blob, field.filename);
              console.log('[Nuxt Proxy] Added file field:', field.name, field.filename, field.type);
            } else {
              // Text field
              formDataObj.append(field.name, field.data.toString());
              console.log('[Nuxt Proxy] Added text field:', field.name);
            }
          }
          body = formDataObj;
          isFormData = true;
          console.log('[Nuxt Proxy] FormData created successfully');
        }
      } else {
        // JSON body için readBody kullan
        body = await readBody(event);
        console.log('[Nuxt Proxy] JSON body:', body ? 'present' : 'empty');
      }
    } catch (error) {
      console.error('[Nuxt Proxy] Error reading body:', error);
      // No body
    }
  }
  
  // Get query parameters
  const query = getQuery(event);
  const queryString = new URLSearchParams(query as Record<string, string>).toString();
  // Backend route is always /api/{path}, regardless of gateway or direct access
  const url = queryString ? `${backendBaseUrl}/api/${path}?${queryString}` : `${backendBaseUrl}/api/${path}`;
  
  // Get authorization header from request
  let authHeader = getHeader(event, 'authorization');
  
  // If no Authorization header, try to get token from cookie (for <img> tags)
  if (!authHeader) {
    try {
      // Try to get token from cookie using getCookie (h3 function)
      const tokenCookie = getCookie(event, 'access_token');
      if (tokenCookie) {
        authHeader = `Bearer ${tokenCookie}`;
      }
    } catch (cookieError) {
      // Cookie read error is not critical, continue without token
      console.warn('[Nuxt Proxy] Could not read access_token cookie:', cookieError);
    }
  }
  
  try {
    // Development için SSL sertifika doğrulamasını geçici olarak devre dışı bırak
    const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    if (process.env.NODE_ENV === 'development') {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
    }
    
    try {
      const headers: Record<string, string> = {};
      
      // FormData için Content-Type header'ını set etme (boundary otomatik eklenir)
      if (!isFormData) {
        headers['Content-Type'] = 'application/json';
      }
      
      if (authHeader) {
        headers['Authorization'] = authHeader;
      }
      
      // Export endpoint'leri ve photo GET endpoint'leri için binary response handling
      // POST /user/{userId}/photo JSON döndürür, GET /user/{userId}/photo binary döndürür
      // Note: user/[userId]/photo.ts was deleted to allow POST requests to go through [...path].ts
      if (method === 'GET' && (path.startsWith('group/export') || path.startsWith('user/export') || (path.includes('/photo') && !path.includes('/photo.')))) {
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
        // FormData için özel handling
        if (isFormData && body instanceof FormData) {
          console.log('[Nuxt Proxy] Sending FormData to backend:', url);
          console.log('[Nuxt Proxy] Method:', method);
          // FormData için $fetch kullan, ancak Content-Type header'ını set etme
          // $fetch FormData'yı handle edebilir, ancak Content-Type'ı otomatik ayarlar
          const fetchHeaders: Record<string, string> = {};
          if (authHeader) {
            fetchHeaders['Authorization'] = authHeader;
          }
          // Content-Type header'ını set etme - $fetch otomatik olarak multipart/form-data ile boundary ekler
          
          try {
            response = await $fetch(url, {
              method: method as any,
              headers: fetchHeaders,
              body: body,
            });
            console.log('[Nuxt Proxy] Backend response received:', response ? 'success' : 'empty');
          } catch (fetchError: any) {
            console.error('[Nuxt Proxy] Backend fetch error:', fetchError);
            throw fetchError;
          }
          
          // Orijinal ayarı geri yükle
          if (originalRejectUnauthorized !== undefined) {
            process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized;
          } else {
            delete process.env.NODE_TLS_REJECT_UNAUTHORIZED;
          }
        } else {
          // GET, POST, PUT için normal $fetch kullan (FormData değilse)
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

