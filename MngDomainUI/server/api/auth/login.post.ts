import https from 'https'
import { buildKeycloakUrl, getKeycloakConfig } from '~/server/utils/keycloak'

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const body = await readBody(event)
  const { username, password } = body

  if (!username || !password) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Username and password are required'
    })
  }

  // Get Keycloak configuration (base URL and path prefix)
  const keycloakConfig = getKeycloakConfig(config)
  
  // Keycloak realm - use master realm (same as MngKeeper's EnsureAdminTokenAsync)
  const keycloakRealm = process.env.KEYCLOAK_REALM || 'master'
  
  // Build Keycloak token URL with configurable path prefix
  const tokenEndpoint = `realms/${keycloakRealm}/protocol/openid-connect/token`
  const tokenUrl = buildKeycloakUrl(keycloakConfig.baseUrl, keycloakConfig.pathPrefix, tokenEndpoint)
  
  console.log('[Login] Keycloak Base URL:', keycloakConfig.baseUrl)
  console.log('[Login] Keycloak Path Prefix:', keycloakConfig.pathPrefix || '(empty - direct access)')
  console.log('[Login] Keycloak Realm:', keycloakRealm)
  console.log('[Login] Full token URL:', tokenUrl)

  try {
    // Get token from Keycloak realm
    const tokenResponse = await $fetch<any>(
      tokenUrl,
      {
        method: 'POST',
        body: new URLSearchParams({
          username: username,
          password: password,
          grant_type: 'password',
          client_id: 'admin-cli'
        }),
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded'
        },
        // SSL bypass for development
        agent: keycloakConfig.baseUrl.startsWith('https')
          ? new https.Agent({ rejectUnauthorized: false })
          : undefined
      }
    )

    if (!tokenResponse.access_token) {
      throw createError({
        statusCode: 401,
        statusMessage: 'Invalid credentials'
      })
    }

    // Decode token to get user info (optional, for display purposes)
    let userInfo: any = { username }
    try {
      const tokenParts = tokenResponse.access_token.split('.')
      if (tokenParts.length >= 2) {
        const payload = tokenParts[1]
        const padding = 4 - (payload.length % 4)
        const paddedPayload = padding !== 4 ? payload + '='.repeat(padding) : payload
        const decoded = Buffer.from(paddedPayload.replace(/-/g, '+').replace(/_/g, '/'), 'base64').toString('utf-8')
        const tokenData = JSON.parse(decoded)
        userInfo = {
          username: tokenData.preferred_username || username,
          email: tokenData.email,
          roles: tokenData.realm_access?.roles || []
        }
      }
    } catch (error) {
      // If token decode fails, use basic user info
      console.warn('Failed to decode token:', error)
    }

    return {
      success: true,
      accessToken: tokenResponse.access_token,
      refreshToken: tokenResponse.refresh_token || null,
      expiresIn: tokenResponse.expires_in || 300,
      refreshExpiresIn: tokenResponse.refresh_expires_in || 1800,
      tokenType: tokenResponse.token_type || 'Bearer',
      user: userInfo
    }
  } catch (error: any) {
    if (error.statusCode === 401 || error.statusCode === 403) {
      throw createError({
        statusCode: 401,
        statusMessage: 'Invalid username or password'
      })
    }

    throw createError({
      statusCode: error.statusCode || 500,
      statusMessage: error.message || 'Login failed'
    })
  }
})

