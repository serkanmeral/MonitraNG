// Server-side API route to proxy all DataGateway API requests
// This bypasses SSL certificate validation issues in development

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  
  // Use server-side URL (for Docker container-to-container communication)
  // Read directly from process.env first (runtime), then fallback to config (build-time)
  // In Docker, this will be https://mngdatagateway:5010; in local dev, https://localhost:5010
  const datagatewayUrl = process.env.SERVER_DATAGATEWAY_URL 
    || process.env.DATAGATEWAY_URL 
    || config.serverDataGatewayUrl 
    || config.public.datagatewayUrl 
    || 'https://localhost:5010'
  
  // Get the path from the route
  const path = getRouterParam(event, 'path') || ''
  const method = getMethod(event)
  
  // Get request body if exists
  let body = null
  if (method === 'POST' || method === 'PUT' || method === 'PATCH') {
    try {
      body = await readBody(event)
    } catch {
      // No body
    }
  }
  
  // Get query parameters
  const query = getQuery(event)
  const queryString = new URLSearchParams(query as Record<string, string>).toString()
  const url = queryString ? `${datagatewayUrl}/api/${path}?${queryString}` : `${datagatewayUrl}/api/${path}`
  
  // Debug: Log request details in development
  if (process.dev) {
    console.log('[DataGateway Proxy] Request:', method, url)
  }
  
  try {
    // Server-side fetch - SSL validation is bypassed by nitro plugin
    // Use https agent for HTTPS URLs to bypass SSL validation
    const https = await import('https')
    const response = await $fetch(url, {
      method: method as any,
      ...(body && { body }),
      // SSL bypass for container-to-container HTTPS communication
      ...(url.startsWith('https') && {
        // @ts-ignore - httpsAgent is a valid option
        agent: new https.Agent({ rejectUnauthorized: false })
      })
    })

    return response
  } catch (error: any) {
    console.error('[DataGateway Proxy] Error:', error.message, 'URL:', url)
    throw createError({
      statusCode: error.statusCode || error.status || 500,
      statusMessage: error.message || 'API call failed',
    })
  }
})

