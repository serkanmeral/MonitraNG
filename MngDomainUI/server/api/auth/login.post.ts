import https from 'https'

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

  // Keycloak base URL - read from environment or runtime config
  // In Docker, use keycloak hostname; in development, use localhost
  // Note: Keycloak runs under /keycloak path in production (KC_HTTP_RELATIVE_PATH)
  // Environment variable should include /keycloak path: http://keycloak:8080/keycloak
  const keycloakBaseUrl = process.env.KEYCLOAK_BASE_URL || config.keycloakBaseUrl || 'http://localhost:8080'
  // Keycloak realm - use master realm (same as MngKeeper's EnsureAdminTokenAsync)
  const keycloakRealm = process.env.KEYCLOAK_REALM || 'master'
  
  console.log('[Login] Keycloak URL:', keycloakBaseUrl)
  console.log('[Login] Keycloak Realm:', keycloakRealm)

  try {
    // Get token from Keycloak realm
    const tokenResponse = await $fetch<any>(
      `${keycloakBaseUrl}/realms/${keycloakRealm}/protocol/openid-connect/token`,
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
        agent: keycloakBaseUrl.startsWith('https')
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

