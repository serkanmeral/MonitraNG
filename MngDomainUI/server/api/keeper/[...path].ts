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
    // Server-side fetch - Use undici for proper SSL bypass with agent support
    // Native fetch doesn't support agent, so we use undici directly
    const { request } = await import('undici')
    const https = await import('https')
    const httpsAgent = new https.Agent({ rejectUnauthorized: false })
    
    // Use undici request with custom agent for SSL bypass
    const response = await request(url, {
      method: method,
      ...(body && { body: JSON.stringify(body) }),
      headers: {
        'Content-Type': 'application/json',
        ...(event.node.req.headers.authorization && {
          'Authorization': event.node.req.headers.authorization as string
        })
      },
      // undici supports connect option for custom agent
      connect: url.startsWith('https') ? {
        rejectUnauthorized: false
      } : undefined
    })
    
    if (response.statusCode >= 400) {
      const errorText = await response.body.text().catch(() => 'Unknown error')
      throw new Error(`HTTP ${response.statusCode}: ${errorText}`)
    }
    
    // Parse response based on content type
    const contentType = response.headers['content-type']
    if (contentType?.includes('application/json')) {
      return await response.body.json()
    } else {
      return await response.body.text()
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

