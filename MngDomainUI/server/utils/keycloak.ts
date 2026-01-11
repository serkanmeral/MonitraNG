/**
 * Keycloak URL helper functions
 * Similar to MngKeeper's KeycloakService.BuildEndpointPath logic
 */

/**
 * Builds a Keycloak API endpoint URL with optional path prefix
 * @param baseUrl - Keycloak base URL (e.g., 'http://keycloak:8080' or 'http://localhost:8080')
 * @param pathPrefix - Optional path prefix (e.g., '/keycloak' or empty string for direct access)
 * @param endpoint - API endpoint path (e.g., 'realms/master/protocol/openid-connect/token')
 * @returns Full Keycloak API URL with path prefix if configured
 */
export function buildKeycloakUrl(
  baseUrl: string,
  pathPrefix: string | undefined | null,
  endpoint: string
): string {
  // Normalize path prefix
  let normalizedPrefix = pathPrefix || ''
  
  // Ensure prefix starts with / if not empty
  if (normalizedPrefix && !normalizedPrefix.startsWith('/')) {
    normalizedPrefix = '/' + normalizedPrefix
  }
  
  // Remove trailing slash from prefix
  if (normalizedPrefix.endsWith('/')) {
    normalizedPrefix = normalizedPrefix.slice(0, -1)
  }
  
  // Normalize endpoint (remove leading slash if exists)
  const normalizedEndpoint = endpoint.startsWith('/') ? endpoint.slice(1) : endpoint
  
  // Build final URL
  if (normalizedPrefix) {
    return `${baseUrl}${normalizedPrefix}/${normalizedEndpoint}`
  }
  
  return `${baseUrl}/${normalizedEndpoint}`
}

/**
 * Gets Keycloak configuration from runtime config or environment variables
 * Priority: process.env (runtime) > config (build-time)
 * @param config - Nuxt runtime config
 * @returns Keycloak configuration object
 */
export function getKeycloakConfig(config: any) {
  // Read from environment variables first (runtime), then fallback to config (build-time)
  // This ensures Docker environment variables are properly used
  const baseUrl = process.env.KEYCLOAK_BASE_URL 
    || config.keycloakBaseUrl 
    || 'http://localhost:8080'
  const pathPrefix = process.env.KEYCLOAK_PATH_PREFIX !== undefined
    ? process.env.KEYCLOAK_PATH_PREFIX
    : (config.keycloakPathPrefix || '')
  
  return {
    baseUrl,
    pathPrefix,
    adminUser: process.env.KEYCLOAK_ADMIN_USER 
      || config.keycloakAdminUser 
      || 'admin',
    adminPassword: process.env.KEYCLOAK_ADMIN_PASSWORD 
      || config.keycloakAdminPassword 
      || 'admin123'
  }
}
