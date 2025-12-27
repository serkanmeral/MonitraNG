export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  // Use gateway URL if available, otherwise use direct keeper URL
  const keeperUrl = config.public.gatewayUrl 
    ? `${config.public.gatewayUrl}/keeper`
    : (config.public.keeperUrl || 'https://localhost:5001');
  
  const body = await readBody(event);
  
  try {
    // Development için SSL sertifika doğrulamasını geçici olarak devre dışı bırak
    const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED;
    if (process.env.NODE_ENV === 'development') {
      process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
    }
    
    try {
      // $fetch kullan (undici tabanlı, SSL ayarlarını process.env'den alır)
      const response = await $fetch(`${keeperUrl}/api/auth/token`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: body,
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
      const statusMessage = fetchError.statusMessage || fetchError.message || 'Authentication failed';
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
      statusMessage: error.message || 'Authentication failed',
      data: error.data || error,
    });
  }
});

