import * as Minio from 'minio'
import https from 'https'

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
    // Get Keycloak admin token
    const tokenResponse = await $fetch<any>(
      `${config.keycloakBaseUrl}/realms/master/protocol/openid-connect/token`,
      {
        method: 'POST',
        body: new URLSearchParams({
          username: config.keycloakAdminUser,
          password: config.keycloakAdminPassword,
          grant_type: 'password',
          client_id: 'admin-cli'
        }),
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded'
        },
        // SSL bypass for development
        agent: config.keycloakBaseUrl.startsWith('https')
          ? new https.Agent({ rejectUnauthorized: false })
          : undefined
      }
    )

    const keycloakToken = tokenResponse.access_token

    // Get all realms
    const realms = await $fetch<any[]>(
      `${config.keycloakBaseUrl}/admin/realms`,
      {
        method: 'GET',
        headers: {
          Authorization: `Bearer ${keycloakToken}`,
          'Content-Type': 'application/json'
        },
        // SSL bypass for development
        agent: config.keycloakBaseUrl.startsWith('https')
          ? new https.Agent({ rejectUnauthorized: false })
          : undefined
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
        await $fetch(
          `${config.keycloakBaseUrl}/admin/realms/${realmName}`,
          {
            method: 'DELETE',
            headers: {
              Authorization: `Bearer ${keycloakToken}`,
              'Content-Type': 'application/json'
            },
            // SSL bypass for development
            agent: config.keycloakBaseUrl.startsWith('https')
              ? new https.Agent({ rejectUnauthorized: false })
              : undefined
          }
        )
        deletedCount++
      } catch (error: any) {
        console.error(`Failed to delete realm ${realmName}:`, error.message)
      }
    }

    results.keycloak.success = true
    results.keycloak.deletedCount = deletedCount
  } catch (error: any) {
    results.keycloak.error = error.message || 'Unknown error'
    console.error('Keycloak cleanup failed:', error)
  }

  // ============================================
  // 2. MINIO BUCKET'LARINI TEMİZLEME
  // ============================================
  try {
    // Parse endpoint (host:port)
    const [host, portStr] = config.minioEndpoint.split(':')
    const port = portStr ? parseInt(portStr, 10) : (config.minioUseSSL ? 443 : 9000)

    // Create MinIO client
    const minioClient = new Minio.Client({
      endPoint: host,
      port: port,
      useSSL: config.minioUseSSL,
      accessKey: config.minioAccessKey,
      secretKey: config.minioSecretKey
    })

    // List all buckets
    const buckets = await minioClient.listBuckets()

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
    results.minio.error = error.message || 'Unknown error'
    console.error('MinIO cleanup failed:', error)
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

