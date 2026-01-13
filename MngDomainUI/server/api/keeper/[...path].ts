// Server-side API route to proxy all keeper API requests
// This bypasses SSL certificate validation issues in development

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  
  // Use server-side URL (for Docker container-to-container communication)
  // Read directly from process.env first (runtime), then fallback to config (build-time)
  // In Docker, this will be https://mngkeeper:5001; in local dev, http://localhost:5001
  // Priority: SERVER_KEEPER_URL > KEEPER_URL > config.serverKeeperUrl > config.public.keeperUrl > default
  const keeperUrl = process.env.SERVER_KEEPER_URL 
    || process.env.KEEPER_URL 
    || config.serverKeeperUrl 
    || config.public.keeperUrl 
    || 'http://localhost:5001'
  
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
  let path = getRouterParam(event, 'path') || ''
  const method = getMethod(event)
  
  // Remove 'keeper/' prefix if present (since we're already in /api/keeper/ route)
  // The path should be relative to /api/ (e.g., 'domain' not 'keeper/domain')
  if (path.startsWith('keeper/')) {
    path = path.substring('keeper/'.length)
  }
  
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
    // Server-side fetch - Use $fetch with SSL bypass via NODE_TLS_REJECT_UNAUTHORIZED
    // Set environment variable temporarily for this request
    const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED
    try {
      // Temporarily disable SSL verification for container-to-container HTTPS
      if (url.startsWith('https')) {
        process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'
      }
      
      const response = await $fetch(url, {
        method: method as any,
        ...(body && { body }),
        headers: {
          ...(event.node.req.headers.authorization && {
            'Authorization': event.node.req.headers.authorization as string
          })
        }
      })

      return response
    } finally {
      // Restore original value
      if (originalRejectUnauthorized !== undefined) {
        process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized
      } else {
        delete process.env.NODE_TLS_REJECT_UNAUTHORIZED
      }
    }
  } catch (error: any) {
    console.error('[Keeper Proxy] Error:', error.message, 'URL:', url)
    console.error('[Keeper Proxy] Error details:', error)
    
    // Extract error message properly
    let errorMessage = 'API call failed'
    if (typeof error === 'string') {
      errorMessage = error
    } else if (error?.message) {
      errorMessage = String(error.message)
    } else if (error?.data?.message) {
      errorMessage = String(error.data.message)
    } else if (error?.data) {
      errorMessage = typeof error.data === 'string' ? error.data : JSON.stringify(error.data)
    }
    
    throw createError({
      statusCode: error.statusCode || error.status || 500,
      statusMessage: errorMessage,
      data: error.data || { error: errorMessage }
    })
  }
})

