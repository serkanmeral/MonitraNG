// Server-side API route to proxy all keeper API requests
// This bypasses SSL certificate validation issues in development

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  
  // Use server-side URL (for Docker container-to-container communication)
  // Read directly from process.env first (runtime), then fallback to config (build-time)
  // In Docker, this will be https://mngkeeper:5001; in local dev, https://localhost:5001
  const keeperUrl = process.env.SERVER_KEEPER_URL 
    || process.env.KEEPER_URL 
    || config.serverKeeperUrl 
    || config.public.keeperUrl 
    || 'https://localhost:5001'
  
  // Always log the URL being used (for debugging)
  console.log('[Keeper Proxy] Using URL:', keeperUrl)
  console.log('[Keeper Proxy] Config check:', {
    envServerKeeperUrl: process.env.SERVER_KEEPER_URL,
    envKeeperUrl: process.env.KEEPER_URL,
    configServerKeeperUrl: config.serverKeeperUrl,
    publicKeeperUrl: config.public.keeperUrl,
    finalUrl: keeperUrl
  })
  
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
  const url = queryString ? `${keeperUrl}/api/${path}?${queryString}` : `${keeperUrl}/api/${path}`
  
  // Debug: Log request details in development
  if (process.dev) {
    console.log('[Keeper Proxy] Request:', method, url)
  }
  
  try {
    // Server-side fetch - Use native fetch with undici for proper SSL bypass
    // ofetch doesn't support agent directly, so we use native fetch with undici
    const https = await import('https')
    const httpsAgent = new https.Agent({ rejectUnauthorized: false })
    
    // Use native fetch with custom agent for SSL bypass
    const fetchOptions: RequestInit = {
      method: method,
      ...(body && { body: JSON.stringify(body) }),
      headers: {
        'Content-Type': 'application/json',
        ...(event.node.req.headers.authorization && {
          'Authorization': event.node.req.headers.authorization
        })
      },
      // @ts-ignore - agent is valid for node-fetch/undici
      agent: url.startsWith('https') ? httpsAgent : undefined
    }
    
    const response = await fetch(url, fetchOptions)
    
    if (!response.ok) {
      const errorText = await response.text().catch(() => 'Unknown error')
      throw new Error(`HTTP ${response.status}: ${errorText}`)
    }
    
    // Parse response based on content type
    const contentType = response.headers.get('content-type')
    if (contentType?.includes('application/json')) {
      return await response.json()
    } else {
      return await response.text()
    }
  } catch (error: any) {
    console.error('[Keeper Proxy] Error:', error.message, 'URL:', url)
    console.error('[Keeper Proxy] Error details:', error)
    throw createError({
      statusCode: error.statusCode || error.status || 500,
      statusMessage: error.message || 'API call failed',
    })
  }
})

