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

  // Determine base URL - use Gateway if available, otherwise use direct LLM URL
  const gatewayUrl = config.public.gatewayUrl;
  const llmUrl = config.public.llmUrl || 'https://localhost:5030';
  
  let fullUrl: string;
  if (gatewayUrl) {
    // Use Gateway: path='v1/chatbot/chat' -> Gateway: 'https://localhost:5040/llm/api/v1/chatbot/chat'
    // Gateway forwards to MngLLM as: 'http://mngllm:5030/api/v1/chatbot/chat'
    fullUrl = `${gatewayUrl}/llm/api/${path}`;
  } else {
    // Direct to MngLLM: path='v1/chatbot/chat' -> MngLLM: 'http://localhost:5030/api/v1/chatbot/chat'
    fullUrl = `${llmUrl}/api/${path}`;
  }
  
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
    const response = await $fetch(urlWithQuery, {
      method: method as any,
      headers: requestOptions.headers as Record<string, string>,
      ...(requestOptions.body && { body: JSON.parse(requestOptions.body as string) }),
    });
    
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

    // Error handling
    const statusCode = error.statusCode || error.status || 500;
    const statusMessage = error.statusMessage || error.message || 'Internal Server Error';
    
    throw createError({
      statusCode,
      statusMessage,
      data: error.data || error,
    });
  }
});
