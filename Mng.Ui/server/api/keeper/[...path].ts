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
  
  // Debug: Log authorization header (sadece development'ta)
  if (process.env.NODE_ENV === 'development') {
    console.log('[Keeper Proxy] Path:', path);
    console.log('[Keeper Proxy] Method:', method);
    console.log('[Keeper Proxy] Auth Header:', authHeader ? 'Present' : 'Missing');
    if (authHeader) {
      // Token'ın ilk 20 karakterini göster (güvenlik için tam token'ı loglamıyoruz)
      const tokenPreview = authHeader.substring(0, 30) + '...';
      console.log('[Keeper Proxy] Token Preview:', tokenPreview);
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
      
      // $fetch kullan (undici tabanlı, SSL ayarlarını process.env'den alır)
      const response = await $fetch(url, {
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

