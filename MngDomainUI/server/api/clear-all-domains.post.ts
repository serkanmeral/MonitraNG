import * as Minio from 'minio'
import https from 'https'
import { buildKeycloakUrl, getKeycloakConfig } from '~/server/utils/keycloak'

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  
  const results = {
    keycloak: {
      success: false,
      deletedCount: 0,
      error: null as string | null
    },
    minio: {
      success: false,
      deletedCount: 0,
      error: null as string | null
    }
  }

  // ============================================
  // 1. KEYCLOAK REALM'LERİNİ TEMİZLEME
  // ============================================
  try {
    // Get Keycloak configuration (base URL and path prefix)
    const keycloakConfig = getKeycloakConfig(config)
    
    console.log('[Clear All Domains] Keycloak Base URL:', keycloakConfig.baseUrl)
    console.log('[Clear All Domains] Keycloak Path Prefix:', keycloakConfig.pathPrefix || '(empty - direct access)')
    
    // SSL bypass for container-to-container HTTPS
    const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED
    try {
      if (keycloakConfig.baseUrl.startsWith('https')) {
        process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'
      }
      
      console.log('[Clear All Domains] Keycloak Admin User:', keycloakConfig.adminUser)
      
      // Build Keycloak admin token URL with configurable path prefix
      const tokenEndpoint = 'realms/master/protocol/openid-connect/token'
      const tokenUrl = buildKeycloakUrl(keycloakConfig.baseUrl, keycloakConfig.pathPrefix, tokenEndpoint)
      
      // Get Keycloak admin token
      const tokenResponse = await $fetch<any>(
        tokenUrl,
        {
          method: 'POST',
          body: new URLSearchParams({
            username: keycloakConfig.adminUser,
            password: keycloakConfig.adminPassword,
            grant_type: 'password',
            client_id: 'admin-cli'
          }),
          headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
          }
        }
      )

      const keycloakToken = tokenResponse.access_token

      // Build admin realms list URL
      const realmsListEndpoint = 'admin/realms'
      const realmsListUrl = buildKeycloakUrl(keycloakConfig.baseUrl, keycloakConfig.pathPrefix, realmsListEndpoint)
      
      // Get all realms
      const realms = await $fetch<any[]>(
        realmsListUrl,
        {
          method: 'GET',
          headers: {
            Authorization: `Bearer ${keycloakToken}`,
            'Content-Type': 'application/json'
          }
        }
      )

      // Delete all realms except 'master'
      let deletedCount = 0
      for (const realm of realms) {
        const realmName = realm.realm
        
        if (realmName === 'master') {
          continue // Skip master realm
        }

        try {
          // Build delete realm URL
          const deleteRealmEndpoint = `admin/realms/${realmName}`
          const deleteRealmUrl = buildKeycloakUrl(keycloakConfig.baseUrl, keycloakConfig.pathPrefix, deleteRealmEndpoint)
          
          await $fetch(
            deleteRealmUrl,
            {
              method: 'DELETE',
              headers: {
                Authorization: `Bearer ${keycloakToken}`,
                'Content-Type': 'application/json'
              }
            }
          )
          deletedCount++
        } catch (error: any) {
          console.error(`Failed to delete realm ${realmName}:`, error.message)
        }
      }

      results.keycloak.success = true
      results.keycloak.deletedCount = deletedCount
    } finally {
      if (originalRejectUnauthorized !== undefined) {
        process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized
      } else {
        delete process.env.NODE_TLS_REJECT_UNAUTHORIZED
      }
    }
  } catch (error: any) {
    results.keycloak.error = error.message || 'Unknown error'
    console.error('Keycloak cleanup failed:', error)
  }

  // ============================================
  // 2. MINIO BUCKET'LARINI TEMİZLEME
  // ============================================
  try {
    // Get MinIO config from environment or runtime config
    const minioEndpoint = process.env.MINIO_ENDPOINT || config.minioEndpoint || 'minio:9000'
    const minioUseSSL = process.env.MINIO_USE_SSL === 'true' || config.minioUseSSL || false
    const minioAccessKey = process.env.MINIO_ACCESS_KEY || config.minioAccessKey || 'admin'
    const minioSecretKey = process.env.MINIO_SECRET_KEY || config.minioSecretKey || 'admin123'
    
    console.log('[Clear All Domains] MinIO Endpoint:', minioEndpoint)
    console.log('[Clear All Domains] MinIO UseSSL:', minioUseSSL)
    
    // Parse endpoint (host:port)
    const [host, portStr] = minioEndpoint.split(':')
    const port = portStr ? parseInt(portStr, 10) : (minioUseSSL ? 443 : 9000)

    // Create MinIO client
    const minioClient = new Minio.Client({
      endPoint: host,
      port: port,
      useSSL: minioUseSSL,
      accessKey: minioAccessKey,
      secretKey: minioSecretKey,
      // Disable SSL verification for self-signed certificates
      ...(minioUseSSL && {
        // @ts-ignore - MinIO client options
        rejectUnauthorized: false
      })
    })

    // List all buckets
    const buckets = await minioClient.listBuckets()
    console.log('[Clear All Domains] Found buckets:', buckets.length)

    // Delete all buckets
    let deletedCount = 0
    for (const bucket of buckets) {
      try {
        // Remove all objects in bucket first
        const objectsStream = minioClient.listObjects(bucket.name, '', true)
        
        const objectsToDelete: { name: string }[] = []
        for await (const obj of objectsStream) {
          objectsToDelete.push({ name: obj.name || '' })
        }

        // Delete objects if any
        if (objectsToDelete.length > 0) {
          await minioClient.removeObjects(bucket.name, objectsToDelete.map(o => o.name))
        }

        // Remove bucket
        await minioClient.removeBucket(bucket.name)
        deletedCount++
      } catch (error: any) {
        console.error(`Failed to delete bucket ${bucket.name}:`, error.message)
      }
    }

    results.minio.success = true
    results.minio.deletedCount = deletedCount
  } catch (error: any) {
    results.minio.error = error.message || error.toString() || 'Unknown error'
    console.error('MinIO cleanup failed:', error)
    console.error('MinIO error details:', {
      endpoint: process.env.MINIO_ENDPOINT || config.minioEndpoint,
      useSSL: process.env.MINIO_USE_SSL || config.minioUseSSL,
      accessKey: process.env.MINIO_ACCESS_KEY || config.minioAccessKey ? '***' : 'not set'
    })
  }

  // Return results
  const totalDeleted = results.keycloak.deletedCount + results.minio.deletedCount
  const allSuccess = results.keycloak.success && results.minio.success

  return {
    success: allSuccess,
    message: `Cleanup completed. ${results.keycloak.deletedCount} Keycloak realm(s) and ${results.minio.deletedCount} MinIO bucket(s) deleted.`,
    results: {
      keycloak: {
        success: results.keycloak.success,
        deletedCount: results.keycloak.deletedCount,
        error: results.keycloak.error
      },
      minio: {
        success: results.minio.success,
        deletedCount: results.minio.deletedCount,
        error: results.minio.error
      },
      totalDeleted
    }
  }
})

