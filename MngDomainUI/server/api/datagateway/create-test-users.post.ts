// Server-side API route to create test users
// This mimics the user creation part of create-meral-domain.ps1 script

import https from 'https'

export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const body = await readBody(event)
  const { domainName, token, adminEmail, adminPassword, userCount = 5, defaultPassword = 'Test123!' } = body

  if (!domainName) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Domain name is required',
    })
  }

  const requestedUserCount = Math.max(1, Math.min(100, parseInt(userCount) || 5))
  const userPassword = defaultPassword || 'Test123!'

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
      // SSL bypass for container-to-container HTTPS
      const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED
      try {
        if (keeperUrl.startsWith('https')) {
          process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'
        }
        const tokenResponse = await $fetch(`${keeperUrl}/api/auth/token`, {
          method: 'POST',
          body: {
            username: adminUsername,
            password: adminPwd
          }
        }) as any

        accessToken = tokenResponse.accessToken
        if (!accessToken) {
          throw new Error('Token not found in response')
        }
      } finally {
        if (originalRejectUnauthorized !== undefined) {
          process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized
        } else {
          delete process.env.NODE_TLS_REJECT_UNAUTHORIZED
        }
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
      users: [],
      errors: []
    }

    // Get all groups to map group names to IDs
    const groupNameToId: Record<string, string> = {}
    try {
      // SSL bypass for container-to-container HTTPS
      const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED
      try {
        if (keeperUrl.startsWith('https')) {
          process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'
        }
        const groupsResponse = await $fetch(`${keeperUrl}/api/group?page=1&pageSize=100`, {
          headers: {
            'Authorization': `Bearer ${accessToken}`
          }
        }) as any

        if (groupsResponse.groups && Array.isArray(groupsResponse.groups)) {
          groupsResponse.groups.forEach((group: any) => {
            if (group.groupId && group.name) {
              groupNameToId[group.name] = group.groupId
            }
          })
        }
      } finally {
        if (originalRejectUnauthorized !== undefined) {
          process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized
        } else {
          delete process.env.NODE_TLS_REJECT_UNAUTHORIZED
        }
      }
    } catch (error: any) {
      if (process.dev) {
        console.warn('[Create Test Users] Could not fetch groups:', error.message)
      }
    }

    // Get available groups (excluding admin)
    const availableGroupNames: string[] = []
    Object.keys(groupNameToId).forEach((groupName) => {
      if (groupName.toLowerCase() !== 'admin' && groupName.toLowerCase() !== 'admins') {
        availableGroupNames.push(groupName)
      }
    })

    // If no groups available, default to 'users'
    if (availableGroupNames.length === 0) {
      availableGroupNames.push('users')
    }

    // Titles and departments for random assignment
    const titles = ['Developer', 'QA Engineer', 'Designer', 'Analyst', 'Manager', 'Consultant', 'Senior Developer', 'Junior Developer']
    const departments = ['IT', 'Development', 'QA', 'Design', 'Management', 'Sales', 'Quality Assurance']

    // Generate test users
    const testUsers: any[] = []

    // Always add serkan.meral as first user
    const serkanGroup = availableGroupNames.length > 0 
      ? [availableGroupNames[Math.floor(Math.random() * availableGroupNames.length)]] 
      : ['users']
    
    testUsers.push({
      username: 'serkan.meral',
      email: 'serkan.meral@outlook.com',
      password: 'Serkan123!',
      firstName: 'Serkan',
      lastName: 'Meral',
      title: 'Senior Developer',
      department: 'IT Department',
      gender: 1,
      phoneNumber: '+905551234567',
      groupNames: serkanGroup,
      isActive: true
    })

    // Generate remaining users
    for (let i = 1; i <= requestedUserCount; i++) {
      const randomTitle = titles[Math.floor(Math.random() * titles.length)]
      const randomDepartment = departments[Math.floor(Math.random() * departments.length)]
      const randomGender = Math.floor(Math.random() * 3) // 0, 1, or 2
      const randomGroup = availableGroupNames[Math.floor(Math.random() * availableGroupNames.length)]
      
      testUsers.push({
        username: `test.user${i}`,
        email: `test.user${i}@${domainName}.com`,
        password: userPassword,
        firstName: 'Test',
        lastName: `User${i}`,
        title: randomTitle,
        department: randomDepartment,
        gender: randomGender,
        phoneNumber: `+90555${Math.floor(1000000 + Math.random() * 9000000)}`,
        groupNames: [randomGroup],
        isActive: true
      })
    }

    // Create users
    for (const user of testUsers) {
      try {
        // Convert group names to IDs
        const groupIds: string[] = []
        if (user.groupNames) {
          user.groupNames.forEach((groupName: string) => {
            if (groupNameToId[groupName]) {
              groupIds.push(groupNameToId[groupName])
            }
          })
        }

        const userToCreate: any = {
          username: user.username,
          email: user.email,
          password: user.password,
          firstName: user.firstName,
          lastName: user.lastName,
          groupIds: groupIds,
          isActive: user.isActive
        }

        // Add optional fields
        if (user.title) userToCreate.title = user.title
        if (user.department) userToCreate.department = user.department
        if (user.gender !== undefined) userToCreate.gender = user.gender
        if (user.phoneNumber) userToCreate.phoneNumber = user.phoneNumber

        // SSL bypass for container-to-container HTTPS
        const originalRejectUnauthorized = process.env.NODE_TLS_REJECT_UNAUTHORIZED
        try {
          if (keeperUrl.startsWith('https')) {
            process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'
          }
          const response = await $fetch(`${keeperUrl}/api/user`, {
            method: 'POST',
            body: userToCreate,
            headers: {
              'Authorization': `Bearer ${accessToken}`
            }
          }) as any

          const userId = response.userId || response.user?.userId
          results.users.push({ username: user.username, created: true, id: userId })
        } finally {
          if (originalRejectUnauthorized !== undefined) {
            process.env.NODE_TLS_REJECT_UNAUTHORIZED = originalRejectUnauthorized
          } else {
            delete process.env.NODE_TLS_REJECT_UNAUTHORIZED
          }
        }
      } catch (error: any) {
        if (error.statusCode === 409 || error.status === 409) {
          results.users.push({ username: user.username, created: false, message: 'Already exists' })
        } else {
          const errorMessage = error.data?.message || error.message || 'Unknown error'
          results.errors.push({ username: user.username, error: errorMessage })
          if (process.dev) {
            console.error(`[Create Test Users] Failed to create user "${user.username}":`, errorMessage)
          }
        }
      }
    }

    return {
      success: results.errors.length === 0,
      results,
      message: 'Test users creation completed',
      summary: {
        users: results.users.filter((u: any) => u.created).length
      }
    }
  } catch (error: any) {
    throw createError({
      statusCode: error.statusCode || 500,
      statusMessage: error.message || 'Failed to create test users',
    })
  }
})

