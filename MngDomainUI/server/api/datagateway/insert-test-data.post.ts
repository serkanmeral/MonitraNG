// Server-side API route to insert test data (books test data)
// This mimics the insert-books-test-data.ps1 script functionality

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

  // Use gateway URL if available, otherwise use direct datagateway URL
  // Gateway URL should be like https://localhost:5040, and /data path is handled by gateway routing
  // Use server-side URLs (for container-to-container communication)
  // Read directly from process.env first (runtime), then fallback to config (build-time)
  const datagatewayUrl = process.env.SERVER_DATAGATEWAY_URL 
    || process.env.DATAGATEWAY_URL 
    || config.serverDataGatewayUrl 
    || config.public.datagatewayUrl 
    || 'https://localhost:5010'
  const keeperUrl = process.env.SERVER_KEEPER_URL 
    || process.env.KEEPER_URL 
    || config.serverKeeperUrl 
    || config.public.keeperUrl 
    || 'https://localhost:5001'
  
  // Debug log
  if (process.dev) {
    console.log('[Insert Test Data] DataGateway URL:', datagatewayUrl)
    console.log('[Insert Test Data] Domain:', domainName)
  }

  // Use provided token or get new token from MngKeeper
  let accessToken: string
  if (token) {
    // Use provided token
    accessToken = token
    if (process.dev) {
      console.log('[Insert Test Data] Using provided token')
    }
  } else {
    // Get token from MngKeeper
    const adminUsername = adminEmail || process.env.DEFAULT_ADMIN_EMAIL || `admin@${domainName}`
    const adminPwd = adminPassword || process.env.DEFAULT_ADMIN_PASSWORD || 'Admin123!'
    
    try {
      const tokenResponse = await $fetch(`${keeperUrl}/api/auth/token`, {
        method: 'POST',
        body: {
          username: adminUsername,
          password: adminPwd
        },
        // Bypass SSL certificate validation in development
        ...(process.dev && {
          // @ts-ignore - Nitro internal option
          httpsAgent: new https.Agent({ rejectUnauthorized: false })
        })
      }) as any

      accessToken = tokenResponse.accessToken
      if (!accessToken) {
        throw new Error('Token not found in response')
      }

      if (process.dev) {
        console.log('[Insert Test Data] Token obtained successfully')
      }
    } catch (error: any) {
      console.error('[Insert Test Data] Failed to get token:', error.message)
      throw createError({
        statusCode: 401,
        statusMessage: `Failed to authenticate: ${error.message || 'Invalid credentials'}`,
      })
    }
  }

  try {
    const results: any = {
      publishers: [],
      genres: [],
      books: [],
      errors: []
    }

    // Step 0: Fetch Users from MngKeeper for author field (persons type requires user ID)
    const userIds: string[] = []
    try {
      const usersResponse = await $fetch(`${keeperUrl}/api/user?pageSize=100`, {
        headers: {
          'Authorization': `Bearer ${accessToken}`
        },
        // Bypass SSL certificate validation in development
        ...(process.dev && {
          // @ts-ignore - Nitro internal option
          httpsAgent: new https.Agent({ rejectUnauthorized: false })
        })
      }) as any

      if (usersResponse.users && Array.isArray(usersResponse.users) && usersResponse.users.length > 0) {
        usersResponse.users.forEach((user: any) => {
          if (user.userId) {
            userIds.push(user.userId)
          }
        })
        if (process.dev) {
          console.log(`[Insert Test Data] Found ${userIds.length} users from MngKeeper`)
        }
      }
    } catch (error: any) {
      if (process.dev) {
        console.warn('[Insert Test Data] Could not fetch users from MngKeeper:', error.message)
        console.warn('[Insert Test Data] Books will be created without author field (may fail validation)')
      }
    }

    // Step 1: Insert Publishers
    const publishers = [
      { name: 'Penguin Random House', website: 'https://www.penguinrandomhouse.com', country: 'USA' },
      { name: 'HarperCollins', website: 'https://www.harpercollins.com', country: 'USA' },
      { name: 'Simon & Schuster', website: 'https://www.simonandschuster.com', country: 'USA' },
      { name: 'Macmillan Publishers', website: 'https://www.macmillan.com', country: 'UK' },
      { name: 'Hachette Livre', website: 'https://www.hachette.com', country: 'France' }
    ]

    const publisherIds: Record<string, string> = {}

    // First, try to get existing publishers
    try {
      const existingResponse = await $fetch(`${datagatewayUrl}/api/v1/data/tst_publishers?pageSize=100`, {
        headers: {
          'Authorization': `Bearer ${accessToken}`
        }
      }) as any
      const publisherList = existingResponse.items || existingResponse.data?.items || existingResponse
      if (Array.isArray(publisherList) && publisherList.length > 0) {
        publisherList.forEach((pub: any) => {
          const dataId = pub.__dataId || pub.dataId || pub.DataId
          if (dataId && pub.name) {
            publisherIds[pub.name] = dataId
          }
        })
      }
    } catch {
      // Ignore errors when fetching existing
    }

    // Insert publishers that don't exist
    for (const publisher of publishers) {
      if (publisherIds[publisher.name]) {
        results.publishers.push({ name: publisher.name, created: false, message: 'Already exists' })
        continue
      }

      try {
        const response = await $fetch(`${datagatewayUrl}/api/v1/data/tst_publishers`, {
          method: 'POST',
          body: publisher,
          headers: {
            'Authorization': `Bearer ${accessToken}`
          }
        }) as any

        const dataId = response.data?.__dataId || response.data?.dataId || response.__dataId
        if (dataId) {
          publisherIds[publisher.name] = dataId
          results.publishers.push({ name: publisher.name, created: true, id: dataId })
        }
      } catch (error: any) {
        if (error.statusCode === 409 || error.status === 409) {
          results.publishers.push({ name: publisher.name, created: false, message: 'Already exists' })
        } else {
          results.errors.push({ step: 'publishers', name: publisher.name, error: error.message })
        }
      }
    }

    // Step 2: Insert Genres
    const genres = [
      { name: 'Science Fiction', description: 'Futuristic and science-based fiction' },
      { name: 'Fantasy', description: 'Imaginative fiction with magical elements' },
      { name: 'Mystery', description: 'Detective and crime fiction' },
      { name: 'Romance', description: 'Love stories and romantic relationships' },
      { name: 'Thriller', description: 'Suspenseful and exciting stories' },
      { name: 'Historical Fiction', description: 'Fiction set in the past' },
      { name: 'Biography', description: 'True stories of people\'s lives' },
      { name: 'Self-Help', description: 'Personal development and improvement' }
    ]

    const genreIds: Record<string, string> = {}

    // Get existing genres
    try {
      const existingResponse = await $fetch(`${datagatewayUrl}/api/v1/data/tst_genres?pageSize=100`, {
        headers: {
          'Authorization': `Bearer ${accessToken}`
        }
      }) as any
      const genreList = existingResponse.items || existingResponse.data?.items || existingResponse
      if (Array.isArray(genreList) && genreList.length > 0) {
        genreList.forEach((genre: any) => {
          const dataId = genre.__dataId || genre.dataId || genre.DataId
          if (dataId && genre.name) {
            genreIds[genre.name] = dataId
          }
        })
      }
    } catch {
      // Ignore errors
    }

    // Insert genres that don't exist
    for (const genre of genres) {
      if (genreIds[genre.name]) {
        results.genres.push({ name: genre.name, created: false, message: 'Already exists' })
        continue
      }

      try {
        const response = await $fetch(`${datagatewayUrl}/api/v1/data/tst_genres`, {
          method: 'POST',
          body: genre,
          headers: {
            'Authorization': `Bearer ${accessToken}`
          }
        }) as any

        const dataId = response.data?.__dataId || response.data?.dataId || response.__dataId
        if (dataId) {
          genreIds[genre.name] = dataId
          results.genres.push({ name: genre.name, created: true, id: dataId })
        }
      } catch (error: any) {
        if (error.statusCode === 409 || error.status === 409) {
          results.genres.push({ name: genre.name, created: false, message: 'Already exists' })
        } else {
          results.errors.push({ step: 'genres', name: genre.name, error: error.message })
        }
      }
    }

    // Step 3: Insert Books (full schema compatible)
    // Note: author field is persons type and requires user ID, but we'll skip it for now
    // or make it optional. Other required fields: title, publisher
    const currentYear = new Date().getFullYear()
    const books = [
      {
        title: 'The Foundation',
        subtitle: 'A Science Fiction Masterpiece',
        publisherCode: 'PRH',
        name: 'Foundation Classic',
        publisher: publisherIds['Penguin Random House'] || Object.values(publisherIds)[0],
        genres: [
          genreIds['Science Fiction'] || Object.values(genreIds)[0],
          genreIds['Fantasy'] || Object.values(genreIds)[0]
        ].filter(Boolean),
        // author: null, // persons type - requires user ID, skipping for now
        coAuthors: [],
        reviewerGroups: [],
        pageCount: 320,
        publicationDate: `${currentYear - 5}-01-15T00:00:00Z`,
        language: 'en', // ISO 639-1 format (en, not English)
        price: 29.99,
        coverImage: {
          url: 'https://example.com/covers/foundation.jpg',
          alt: 'Foundation Book Cover',
          width: 400,
          height: 600
        }
      },
      {
        title: 'The Hobbit',
        subtitle: 'There and Back Again',
        publisherCode: 'HC',
        name: 'Hobbit Classic',
        publisher: publisherIds['HarperCollins'] || Object.values(publisherIds)[0],
        genres: [
          genreIds['Fantasy'] || Object.values(genreIds)[0]
        ].filter(Boolean),
        coAuthors: [],
        reviewerGroups: [],
        pageCount: 310,
        publicationDate: `${currentYear - 3}-06-20T00:00:00Z`,
        language: 'en',
        price: 24.99,
        coverImage: {
          url: 'https://example.com/covers/hobbit.jpg',
          alt: 'The Hobbit Book Cover',
          width: 400,
          height: 600
        }
      },
      {
        title: 'The Da Vinci Code',
        subtitle: 'A Thrilling Mystery',
        publisherCode: 'SS',
        name: 'Da Vinci Code',
        publisher: publisherIds['Simon & Schuster'] || Object.values(publisherIds)[0],
        genres: [
          genreIds['Mystery'] || Object.values(genreIds)[0],
          genreIds['Thriller'] || Object.values(genreIds)[0]
        ].filter(Boolean),
        coAuthors: [],
        reviewerGroups: [],
        pageCount: 454,
        publicationDate: `${currentYear - 2}-03-10T00:00:00Z`,
        language: 'en',
        price: 19.99
      },
      {
        title: 'Pride and Prejudice',
        subtitle: 'A Timeless Romance',
        publisherCode: 'MP',
        name: 'Pride and Prejudice',
        publisher: publisherIds['Macmillan Publishers'] || Object.values(publisherIds)[0],
        genres: [
          genreIds['Romance'] || Object.values(genreIds)[0],
          genreIds['Historical Fiction'] || Object.values(genreIds)[0]
        ].filter(Boolean),
        coAuthors: [],
        reviewerGroups: [],
        pageCount: 432,
        publicationDate: `${currentYear - 1}-09-05T00:00:00Z`,
        language: 'en',
        price: 15.99
      },
      {
        title: 'The Art of War',
        subtitle: 'Ancient Strategy for Modern Times',
        publisherCode: 'HL',
        name: 'Art of War',
        publisher: publisherIds['Hachette Livre'] || Object.values(publisherIds)[0],
        genres: [
          genreIds['Biography'] || Object.values(genreIds)[0],
          genreIds['Self-Help'] || Object.values(genreIds)[0]
        ].filter(Boolean),
        coAuthors: [],
        reviewerGroups: [],
        pageCount: 128,
        publicationDate: `${currentYear - 4}-11-12T00:00:00Z`,
        language: 'en',
        price: 12.99,
        coverImage: {
          url: 'https://example.com/covers/art-of-war.jpg',
          alt: 'The Art of War Book Cover',
          width: 350,
          height: 525
        }
      },
      {
        title: '1984',
        subtitle: 'A Dystopian Classic',
        publisherCode: 'PRH',
        name: '1984 Classic',
        publisher: publisherIds['Penguin Random House'] || Object.values(publisherIds)[0],
        genres: [
          genreIds['Science Fiction'] || Object.values(genreIds)[0],
          genreIds['Thriller'] || Object.values(genreIds)[0]
        ].filter(Boolean),
        coAuthors: [],
        reviewerGroups: [],
        pageCount: 328,
        publicationDate: `${currentYear - 6}-08-15T00:00:00Z`,
        language: 'en',
        price: 18.99
      },
      {
        title: 'To Kill a Mockingbird',
        subtitle: 'A Coming-of-Age Story',
        publisherCode: 'HC',
        name: 'Mockingbird',
        publisher: publisherIds['HarperCollins'] || Object.values(publisherIds)[0],
        genres: [
          genreIds['Historical Fiction'] || Object.values(genreIds)[0],
          genreIds['Mystery'] || Object.values(genreIds)[0]
        ].filter(Boolean),
        coAuthors: [],
        reviewerGroups: [],
        pageCount: 376,
        publicationDate: `${currentYear - 7}-07-11T00:00:00Z`,
        language: 'en',
        price: 16.99,
        coverImage: {
          url: 'https://example.com/covers/mockingbird.jpg',
          alt: 'To Kill a Mockingbird Cover',
          width: 400,
          height: 600
        }
      },
      {
        title: 'The Great Gatsby',
        subtitle: 'The American Dream',
        publisherCode: 'SS',
        name: 'Great Gatsby',
        publisher: publisherIds['Simon & Schuster'] || Object.values(publisherIds)[0],
        genres: [
          genreIds['Romance'] || Object.values(genreIds)[0],
          genreIds['Historical Fiction'] || Object.values(genreIds)[0]
        ].filter(Boolean),
        coAuthors: [],
        reviewerGroups: [],
        pageCount: 180,
        publicationDate: `${currentYear - 8}-04-10T00:00:00Z`,
        language: 'en',
        price: 14.99
      }
    ]

    // Insert books
    for (const book of books) {
      try {
        // Clean book data - remove null/undefined values and empty arrays for optional fields
        const cleanBook: any = {}
        for (const [key, value] of Object.entries(book)) {
          if (value !== null && value !== undefined) {
            // Skip empty arrays for optional array fields (coAuthors, reviewerGroups)
            if (Array.isArray(value) && value.length === 0 && 
                (key === 'coAuthors' || key === 'reviewerGroups')) {
              continue
            }
            cleanBook[key] = value
          }
        }

        // Add author field (required, persons type needs user ID)
        // Use random user ID if available, otherwise skip (may fail validation)
        if (userIds.length > 0) {
          const randomUserId = userIds[Math.floor(Math.random() * userIds.length)]
          cleanBook.author = randomUserId
        } else {
          // Author is required but no users available
          // Try to create without author - may fail validation
          if (process.dev) {
            console.warn(`[Insert Test Data] No users available for book "${book.title}", creating without author`)
          }
        }

        const response = await $fetch(`${datagatewayUrl}/api/v1/data/tst_books`, {
          method: 'POST',
          body: cleanBook,
          headers: {
            'Authorization': `Bearer ${accessToken}`
          }
        }) as any

        const dataId = response.data?.__dataId || response.data?.dataId || response.__dataId
        results.books.push({ title: book.title, created: true, id: dataId })
      } catch (error: any) {
        if (error.statusCode === 409 || error.status === 409) {
          results.books.push({ title: book.title, created: false, message: 'Already exists' })
        } else {
          const errorMessage = error.data?.message || error.message || 'Unknown error'
          results.errors.push({ step: 'books', title: book.title, error: errorMessage })
          if (process.dev) {
            console.error(`[Insert Test Data] Failed to create book "${book.title}":`, errorMessage)
            if (error.data) {
              console.error('[Insert Test Data] Error details:', JSON.stringify(error.data, null, 2))
            }
          }
        }
      }
    }

    return {
      success: results.errors.length === 0,
      results,
      message: 'Test data insertion completed',
      summary: {
        publishers: results.publishers.filter((p: any) => p.created).length,
        genres: results.genres.filter((g: any) => g.created).length,
        books: results.books.filter((b: any) => b.created).length
      }
    }
  } catch (error: any) {
    throw createError({
      statusCode: error.statusCode || 500,
      statusMessage: error.message || 'Failed to insert test data',
    })
  }
})

