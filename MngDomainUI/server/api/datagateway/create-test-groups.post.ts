// Server-side API route to create test groups
// This mimics the group creation part of create-meral-domain.ps1 script

import https from 'https'

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const body = await readBody(event)
  const { domainName, token, adminEmail, adminPassword } = body

  if (!domainName) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Domain name is required',
    })
  }

  // Get keeper URL (server-side - for container-to-container communication)
  // Read directly from process.env first (runtime), then fallback to config (build-time)
  const keeperUrl = process.env.SERVER_KEEPER_URL 
    || process.env.KEEPER_URL 
    || config.serverKeeperUrl 
    || config.public.keeperUrl 
    || 'https://localhost:5001'

  // Use provided token or get new token from MngKeeper
  let accessToken: string
  if (token) {
    accessToken = token
  } else {
    const adminUsername = adminEmail || process.env.DEFAULT_ADMIN_EMAIL || `admin@${domainName}`
    const adminPwd = adminPassword || process.env.DEFAULT_ADMIN_PASSWORD || 'Admin123!'
    
    try {
      const tokenResponse = await $fetch(`${keeperUrl}/api/auth/token`, {
        method: 'POST',
        body: {
          username: adminUsername,
          password: adminPwd
        },
        ...(process.dev && {
          // @ts-ignore
          httpsAgent: new https.Agent({ rejectUnauthorized: false })
        })
      }) as any

      accessToken = tokenResponse.accessToken
      if (!accessToken) {
        throw new Error('Token not found in response')
      }
    } catch (error: any) {
      throw createError({
        statusCode: 401,
        statusMessage: `Failed to authenticate: ${error.message || 'Invalid credentials'}`,
      })
    }
  }

  try {
    const results: any = {
      groups: [],
      errors: []
    }

    // Test groups to create
    const testGroups = [
      {
        name: 'developers',
        description: 'Development Team'
      },
      {
        name: 'testers',
        description: 'Testing Team'
      },
      {
        name: 'viewers',
        description: 'View Only Access'
      }
    ]

    // Create groups
    for (const group of testGroups) {
      try {
        const response = await $fetch(`${keeperUrl}/api/group`, {
          method: 'POST',
          body: group,
          headers: {
            'Authorization': `Bearer ${accessToken}`
          },
          ...(process.dev && {
            // @ts-ignore
            httpsAgent: new https.Agent({ rejectUnauthorized: false })
          })
        }) as any

        const groupId = response.groupId || response.group?.groupId
        results.groups.push({ name: group.name, created: true, id: groupId })
      } catch (error: any) {
        if (error.statusCode === 409 || error.status === 409) {
          results.groups.push({ name: group.name, created: false, message: 'Already exists' })
        } else {
          const errorMessage = error.data?.message || error.message || 'Unknown error'
          results.errors.push({ name: group.name, error: errorMessage })
          if (process.dev) {
            console.error(`[Create Test Groups] Failed to create group "${group.name}":`, errorMessage)
          }
        }
      }
    }

    return {
      success: results.errors.length === 0,
      results,
      message: 'Test groups creation completed',
      summary: {
        groups: results.groups.filter((g: any) => g.created).length
      }
    }
  } catch (error: any) {
    throw createError({
      statusCode: error.statusCode || 500,
      statusMessage: error.message || 'Failed to create test groups',
    })
  }
})

