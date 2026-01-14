// Server-side API route to proxy all scheduler API requests
// This bypasses SSL certificate validation issues in development

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  
  // Use server-side URL (for Docker container-to-container communication)
  // Read directly from process.env first (runtime), then fallback to config (build-time)
  // In Docker, this will be http://mngscheduler:5090; in local dev, http://localhost:5090
  // Priority: SERVER_SCHEDULER_URL > SCHEDULER_URL > config.serverSchedulerUrl > config.public.schedulerUrl > default
  const schedulerUrl = process.env.SERVER_SCHEDULER_URL 
    || process.env.SCHEDULER_URL 
    || config.serverSchedulerUrl 
    || config.public.schedulerUrl 
    || 'http://localhost:5090'
  
  // Always log the URL being used (for debugging)
  console.log('[Scheduler Proxy] Using URL:', schedulerUrl)
  console.log('[Scheduler Proxy] Config check:', {
    envServerSchedulerUrl: process.env.SERVER_SCHEDULER_URL,
    envSchedulerUrl: process.env.SCHEDULER_URL,
    configServerSchedulerUrl: config.serverSchedulerUrl,
    publicSchedulerUrl: config.public.schedulerUrl,
    finalUrl: schedulerUrl
  })
  
  // Get the path from the route
  // The path will be like: "v1/system/jobs" (without /api prefix)
  let path = getRouterParam(event, 'path') || ''
  const method = getMethod(event)
  
  // Remove 'scheduler/' prefix if present (since we're already in /api/scheduler/ route)
  if (path.startsWith('scheduler/')) {
    path = path.substring('scheduler/'.length)
  }
  
  // Remove leading slash if present
  if (path.startsWith('/')) {
    path = path.substring(1)
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
  // Path already contains v1/system/jobs, so we add /api/ prefix
  const url = queryString ? `${schedulerUrl}/api/${path}?${queryString}` : `${schedulerUrl}/api/${path}`
  
  // Debug: Log request details in development
  if (process.dev) {
    console.log('[Scheduler Proxy] Request:', {
      method,
      path,
      url,
      hasBody: !!body
    })
  }
  
  try {
    // Use $fetch with SSL validation bypass for development
    const response = await $fetch(url, {
      method: method as any,
      body: body,
      headers: {
        'Content-Type': 'application/json',
      },
      // @ts-ignore - Nitro internal option for SSL bypass
      ...(process.dev && {
        httpsAgent: false, // Disable SSL validation in development
      }),
    })
    
    return response
  } catch (error: any) {
    // Log error details
    console.error('[Scheduler Proxy] Error:', {
      url,
      method,
      status: error.status || error.statusCode,
      message: error.message,
      data: error.data
    })
    
    // Return proper error response
    throw createError({
      statusCode: error.status || error.statusCode || 500,
      statusMessage: error.message || 'Scheduler API request failed',
      data: error.data || error.message
    })
  }
})
