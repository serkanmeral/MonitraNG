// Nitro plugin to bypass SSL certificate validation in development
// This only works server-side and only in development mode

import https from 'https'

export default defineNitroPlugin((nitroApp) => {
  // Disable SSL certificate validation for development and Docker (with self-signed certificates)
  // WARNING: Only use in development/Docker environments, never in production with real certificates!
  if (process.dev || process.env.ENABLE_SSL_BYPASS === 'true') {
    process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'
    
    // Override global agent
    const httpsAgent = new https.Agent({
      rejectUnauthorized: false
    })
    
    // Set default agent for https requests
    https.globalAgent = httpsAgent
    
    console.log('[SSL Fix] SSL certificate validation disabled')
  }
})

