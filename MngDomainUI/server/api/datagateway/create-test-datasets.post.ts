// Server-side API route to create test datasets (books datasets)
// This mimics the setup-books-datasets.ps1 script functionality

import https from 'https'

export default defineEventHandler(async (event) => {
  // Debug log
  if (process.dev) {
    console.log('[Create Test Datasets] Route called')
  }
  
  const config = useRuntimeConfig()
  const body = await readBody(event)
  const { domainName, token, adminEmail, adminPassword } = body

  if (!domainName) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Domain name is required',
    })
  }

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
    console.log('[Create Test Datasets] DataGateway URL:', datagatewayUrl)
    console.log('[Create Test Datasets] Domain:', domainName)
  }

  // Use provided token or get new token from MngKeeper
  let accessToken: string
  if (token) {
    // Use provided token
    accessToken = token
    if (process.dev) {
      console.log('[Create Test Datasets] Using provided token')
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
        console.log('[Create Test Datasets] Token obtained successfully')
      }
    } catch (error: any) {
      console.error('[Create Test Datasets] Failed to get token:', error.message)
      throw createError({
        statusCode: 401,
        statusMessage: `Failed to authenticate: ${error.message || 'Invalid credentials'}`,
      })
    }
  }

  // Get token from MngKeeper (we'll need to get it from the request or use a service account)
  // For now, we'll assume token is passed in the request or use a default
  // TODO: Get token from MngKeeper API for the domain

  try {
    const results: any = {
      category: null,
      publishers: null,
      genres: null,
      books: null,
      errors: []
    }

    // Step 1: Create or get Book Categories category
    const categoryData = {
      categoryName: 'Book Categories',
      categoryDescription: 'Category for book-related datasets (publishers, genres, books)'
    }

    let categoryId: string | null = null

    try {
      // DataGateway API endpoint: /api/v1/dataset-categories (versioned)
      const categoryUrl = `${datagatewayUrl}/api/v1/dataset-categories`
      if (process.dev) {
        console.log('[Create Test Datasets] Category URL:', categoryUrl)
      }
      const categoryResponse = await $fetch(categoryUrl, {
        method: 'POST',
        body: categoryData,
        headers: {
          'Authorization': `Bearer ${accessToken}`
        }
      }) as any

      categoryId = categoryResponse.dataId || categoryResponse.__dataId || categoryResponse.DataId
      results.category = { created: true, id: categoryId }
    } catch (error: any) {
      if (error.statusCode === 409 || error.status === 409) {
        // Category already exists, try to find it
        const categories = await $fetch(`${datagatewayUrl}/api/v1/dataset-categories?pageNumber=1&pageSize=100`, {
          headers: {
            'Authorization': `Bearer ${accessToken}`
          }
        }) as any
        const categoriesList = categories.items || categories.Data || categories.data || categories
        const existingCategory = categoriesList.find((c: any) => 
          (c.categoryName === 'Book Categories') || (c.CategoryName === 'Book Categories')
        )
        if (existingCategory) {
          categoryId = existingCategory.dataId || existingCategory.DataId || existingCategory.__dataId
          results.category = { created: false, id: categoryId, message: 'Already exists' }
        } else {
          throw new Error('Category already exists but could not be found')
        }
      } else {
        throw error
      }
    }

    if (!categoryId) {
      throw createError({
        statusCode: 500,
        statusMessage: 'Failed to create or find Book Categories category',
      })
    }

    // Step 2: Create tst_publishers dataset
    const publishersSchema = {
      name: 'tst_publishers',
      description: 'Book publishers dataset (test)',
      category: categoryId,
      forceSchema: true,
      logging: 'none',
      publishMode: 'none',
      fields: [
        {
          fieldType: 'text',
          name: 'name',
          title: 'Publisher Name',
          mandatory: true,
          unique: true
        },
        {
          fieldType: 'text',
          name: 'website',
          title: 'Website',
          mandatory: false,
          unique: false
        },
        {
          fieldType: 'text',
          name: 'country',
          title: 'Country',
          mandatory: false,
          unique: false
        }
      ],
      indexList: [
        {
          name: 'idx_name',
          fields: { name: 1 },
          unique: true
        }
      ]
    }

    try {
      await $fetch(`${datagatewayUrl}/api/v1/datasets`, {
        method: 'POST',
        body: publishersSchema,
        headers: {
          'Authorization': `Bearer ${accessToken}`
        }
      })
      results.publishers = { created: true }
    } catch (error: any) {
      if (error.statusCode === 409 || error.status === 409) {
        results.publishers = { created: false, message: 'Already exists' }
      } else {
        results.errors.push({ step: 'publishers', error: error.message })
      }
    }

    // Step 3: Create tst_genres dataset
    const genresSchema = {
      name: 'tst_genres',
      description: 'Book genres dataset (test)',
      category: categoryId,
      forceSchema: true,
      logging: 'none',
      publishMode: 'basic',
      fields: [
        {
          fieldType: 'text',
          name: 'name',
          title: 'Genre Name',
          mandatory: true,
          unique: true
        },
        {
          fieldType: 'text',
          name: 'description',
          title: 'Description',
          mandatory: false,
          unique: false
        }
      ],
      indexList: [
        {
          name: 'idx_name',
          fields: { name: 1 },
          unique: true
        }
      ]
    }

    try {
      await $fetch(`${datagatewayUrl}/api/v1/datasets`, {
        method: 'POST',
        body: genresSchema,
        headers: {
          'Authorization': `Bearer ${accessToken}`
        }
      })
      results.genres = { created: true }
    } catch (error: any) {
      if (error.statusCode === 409 || error.status === 409) {
        results.genres = { created: false, message: 'Already exists' }
      } else {
        results.errors.push({ step: 'genres', error: error.message })
      }
    }

    // Step 4: Create tst_books dataset (full complex schema with 19 fields, validations, indexes, queries)
    const booksSchema = {
      name: 'tst_books',
      description: 'Books dataset with relations and person fields (test)',
      category: categoryId,
      forceSchema: true,
      logging: 'self',
      publishMode: 'full',
      fields: [
        {
          fieldType: 'incremental',
          name: 'isbn',
          title: 'ISBN',
          mandatory: true,
          unique: true,
          isArray: false,
          relationDataset: null,
          incrementalOptions: {
            format: 'ISBN-{year}-{0:D6}',
            startValue: 1,
            incrementStep: 1
          }
        },
        {
          fieldType: 'incremental',
          name: 'bookCode',
          title: 'Book Code',
          mandatory: true,
          unique: true,
          isArray: false,
          relationDataset: null,
          incrementalOptions: {
            format: 'BK-{yy}{month}-{0:D4}',
            startValue: 1,
            incrementStep: 1
          }
        },
        {
          fieldType: 'text',
          name: 'publisherCode',
          title: 'Publisher Code',
          mandatory: false,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null
        },
        {
          fieldType: 'incremental',
          name: 'internalBookNumber',
          title: 'Internal Book Number',
          mandatory: true,
          unique: true,
          isArray: false,
          relationDataset: null,
          incrementalOptions: {
            format: '{publisherCode}-{year}-{0:D5}',
            startValue: 1,
            incrementStep: 1
          }
        },
        {
          fieldType: 'incremental',
          name: 'sequenceNumber',
          title: 'Sequence Number',
          mandatory: true,
          unique: true,
          isArray: false,
          relationDataset: null,
          incrementalOptions: {
            format: '{domain}-BOOK-{0:D6}',
            startValue: 1000,
            incrementStep: 10
          }
        },
        {
          fieldType: 'text',
          name: 'name',
          title: 'Book Name',
          mandatory: false,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null
        },
        {
          fieldType: 'text',
          name: 'title',
          title: 'Book Title',
          mandatory: true,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null,
          validation: {
            minLength: 3,
            maxLength: 200,
            message: 'Title must be between 3 and 200 characters'
          }
        },
        {
          fieldType: 'text',
          name: 'subtitle',
          title: 'Subtitle',
          mandatory: false,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null
        },
        {
          fieldType: 'relation',
          name: 'publisher',
          title: 'Publisher',
          mandatory: true,
          unique: false,
          isArray: false,
          relationDataset: 'tst_publishers',
          incrementalOptions: null
        },
        {
          fieldType: 'relation',
          name: 'genres',
          title: 'Genres',
          mandatory: false,
          unique: false,
          isArray: true,
          relationDataset: 'tst_genres',
          incrementalOptions: null,
          validation: {
            minItems: 0,
            maxItems: 5,
            message: 'A book can have at most 5 genres'
          }
        },
        {
          fieldType: 'persons',
          name: 'author',
          title: 'Author',
          mandatory: true,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null
        },
        {
          fieldType: 'persons',
          name: 'coAuthors',
          title: 'Co-Authors',
          mandatory: false,
          unique: false,
          isArray: true,
          relationDataset: null,
          incrementalOptions: null
        },
        {
          fieldType: 'personGroups',
          name: 'reviewerGroups',
          title: 'Reviewer Groups',
          mandatory: false,
          unique: false,
          isArray: true,
          relationDataset: null,
          incrementalOptions: null
        },
        {
          fieldType: 'personGroups',
          name: 'editorialTeam',
          title: 'Editorial Team',
          mandatory: false,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null
        },
        {
          fieldType: 'number',
          name: 'pageCount',
          title: 'Page Count',
          mandatory: false,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null,
          validation: {
            min: 1,
            max: 10000,
            message: 'Page count must be between 1 and 10000'
          }
        },
        {
          fieldType: 'datetime',
          name: 'publicationDate',
          title: 'Publication Date',
          mandatory: false,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null,
          validation: {
            minDate: '1900-01-01T00:00:00Z',
            maxDate: '2100-12-31T23:59:59Z',
            message: 'Publication date must be between 1900 and 2100'
          }
        },
        {
          fieldType: 'text',
          name: 'language',
          title: 'Language',
          mandatory: false,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null,
          validation: {
            pattern: '^[a-z]{2}(-[A-Z]{2})?$',
            message: 'Language must be in ISO 639-1 format (e.g., \'en\', \'tr-TR\')'
          }
        },
        {
          fieldType: 'number',
          name: 'price',
          title: 'Price',
          mandatory: false,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null,
          validation: {
            min: 0,
            max: 100000,
            message: 'Price must be between 0 and 100000'
          }
        },
        {
          fieldType: 'object',
          name: 'coverImage',
          title: 'Cover Image',
          mandatory: false,
          unique: false,
          isArray: false,
          relationDataset: null,
          incrementalOptions: null
        }
      ],
      indexList: [
        {
          name: 'idx_isbn',
          fields: { isbn: 1 },
          unique: true
        },
        {
          name: 'idx_bookCode',
          fields: { bookCode: 1 },
          unique: true
        },
        {
          name: 'idx_internalBookNumber',
          fields: { internalBookNumber: 1 },
          unique: true
        },
        {
          name: 'idx_sequenceNumber',
          fields: { sequenceNumber: 1 },
          unique: true
        },
        {
          name: 'idx_name',
          fields: { name: 1 },
          unique: true
        },
        {
          name: 'idx_title',
          fields: { title: 1 },
          unique: false
        },
        {
          name: 'idx_title_bookCode',
          fields: { bookCode: 1, title: 1 },
          unique: false
        },
        {
          name: 'idx_publisher',
          fields: { publisher: 1 },
          unique: false
        },
        {
          name: 'idx_author',
          fields: { author: 1 },
          unique: false
        },
        {
          name: 'idx_publicationDate',
          fields: { publicationDate: -1 },
          unique: false
        }
      ],
      queries: [
        {
          name: 'books_by_publication_date_range',
          description: 'Get books published between two dates',
          pipeline: [
            {
              $match: {
                publicationDate: {
                  $gte: ':startDate',
                  $lte: ':endDate'
                }
              }
            },
            {
              $sort: {
                publicationDate: -1,
                title: 1
              }
            }
          ],
          parameters: [
            {
              name: 'startDate',
              type: 'datetime'
            },
            {
              name: 'endDate',
              type: 'datetime'
            }
          ]
        },
        {
          name: 'books_by_price_range',
          description: 'Get books within a price range',
          pipeline: [
            {
              $match: {
                price: {
                  $gte: ':minPrice',
                  $lte: ':maxPrice'
                }
              }
            },
            {
              $sort: {
                price: 1,
                title: 1
              }
            }
          ],
          parameters: [
            {
              name: 'minPrice',
              type: 'number',
              description: 'Minimum price',
              required: true
            },
            {
              name: 'maxPrice',
              type: 'number',
              description: 'Maximum price',
              required: true
            }
          ]
        },
        {
          name: 'books_by_min_pages',
          description: 'Get books with at least N pages',
          pipeline: [
            {
              $match: {
                pageCount: {
                  $gte: ':minPages'
                }
              }
            },
            {
              $sort: {
                pageCount: -1,
                title: 1
              }
            }
          ],
          parameters: [
            {
              name: 'minPages',
              type: 'number',
              description: 'Minimum number of pages',
              required: true
            }
          ]
        },
        {
          name: 'books_by_availability',
          description: 'Get books by availability status',
          pipeline: [
            {
              $match: {
                isAvailable: ':isAvailable'
              }
            },
            {
              $sort: {
                title: 1
              }
            }
          ],
          parameters: [
            {
              name: 'isAvailable',
              type: 'bool',
              description: 'Whether the book is available',
              required: true
            }
          ]
        },
        {
          name: 'books_by_published_status',
          description: 'Get published/unpublished books',
          pipeline: [
            {
              $match: {
                $and: [
                  {
                    isPublished: ':isPublished'
                  },
                  {
                    publicationDate: {
                      $exists: true
                    }
                  }
                ]
              }
            },
            {
              $sort: {
                publicationDate: -1
              }
            }
          ],
          parameters: [
            {
              name: 'isPublished',
              type: 'bool',
              description: 'Whether the book is published',
              required: true
            }
          ]
        },
        {
          name: 'books_by_author',
          description: 'Get books by author name (case-insensitive partial match)',
          pipeline: [
            {
              $match: {
                author: {
                  $regex: ':authorName',
                  $options: 'i'
                }
              }
            },
            {
              $sort: {
                title: 1
              }
            }
          ],
          parameters: [
            {
              name: 'authorName',
              type: 'text',
              description: 'Author name (partial match, case-insensitive)',
              required: true
            }
          ]
        },
        {
          name: 'books_by_category_and_title',
          description: 'Get books by category and title contains',
          pipeline: [
            {
              $match: {
                $and: [
                  {
                    category: ':category'
                  },
                  {
                    title: {
                      $regex: ':titleKeyword',
                      $options: 'i'
                    }
                  }
                ]
              }
            },
            {
              $sort: {
                title: 1
              }
            }
          ],
          parameters: [
            {
              name: 'category',
              type: 'text',
              description: 'Book category',
              required: true
            },
            {
              name: 'titleKeyword',
              type: 'text',
              description: 'Title keyword (partial match, case-insensitive)',
              required: true
            }
          ]
        },
        {
          name: 'books_by_price_date_and_status',
          description: 'Get books filtered by price, date range, and availability',
          pipeline: [
            {
              $match: {
                $and: [
                  {
                    price: {
                      $lte: ':maxPrice'
                    }
                  },
                  {
                    publicationDate: {
                      $gte: ':startDate',
                      $lte: ':endDate'
                    }
                  },
                  {
                    isAvailable: ':isAvailable'
                  }
                ]
              }
            },
            {
              $sort: {
                price: 1,
                publicationDate: -1
              }
            }
          ],
          parameters: [
            {
              name: 'maxPrice',
              type: 'number',
              description: 'Maximum price',
              required: true
            },
            {
              name: 'startDate',
              type: 'datetime',
              description: 'Start date (ISO 8601 format)',
              required: true
            },
            {
              name: 'endDate',
              type: 'datetime',
              description: 'End date (ISO 8601 format)',
              required: true
            },
            {
              name: 'isAvailable',
              type: 'bool',
              description: 'Whether the book is available',
              required: true
            }
          ]
        },
        {
          name: 'books_by_author_pages_and_published',
          description: 'Get books by author, minimum pages, and published status',
          pipeline: [
            {
              $match: {
                $and: [
                  {
                    author: {
                      $regex: ':authorName',
                      $options: 'i'
                    }
                  },
                  {
                    pageCount: {
                      $gte: ':minPages'
                    }
                  },
                  {
                    isPublished: ':isPublished'
                  }
                ]
              }
            },
            {
              $sort: {
                pageCount: -1,
                title: 1
              }
            }
          ],
          parameters: [
            {
              name: 'authorName',
              type: 'text',
              description: 'Author name (partial match)',
              required: true
            },
            {
              name: 'minPages',
              type: 'number',
              description: 'Minimum number of pages',
              required: true
            },
            {
              name: 'isPublished',
              type: 'bool',
              description: 'Whether the book is published',
              required: true
            }
          ]
        },
        {
          name: 'books_with_optional_filters',
          description: 'Get books with optional filters',
          pipeline: [
            {
              $match: {
                $and: [
                  {
                    price: {
                      $lte: ':maxPrice'
                    }
                  },
                  {
                    isAvailable: true
                  }
                ]
              }
            },
            {
              $sort: {
                title: 1
              }
            }
          ],
          parameters: [
            {
              name: 'maxPrice',
              type: 'number',
              description: 'Maximum price (optional)',
              required: false
            }
          ]
        }
      ],
      validations: [
        {
          name: 'price_page_ratio',
          description: 'Price per page should be reasonable (max 10 per page)',
          type: 'expression',
          expression: '(price == null || pageCount == null) || (price / pageCount <= 10)',
          when: 'both',
          order: 0
        },
        {
          name: 'price_positive_if_pages',
          description: 'If pageCount is provided, price must be positive',
          type: 'expression',
          expression: '(pageCount == null) || (price != null && price > 0)',
          when: 'both',
          order: 1
        }
      ]
    }

    try {
      await $fetch(`${datagatewayUrl}/api/v1/datasets`, {
        method: 'POST',
        body: booksSchema,
        headers: {
          'Authorization': `Bearer ${accessToken}`
        }
      })
      results.books = { created: true }
    } catch (error: any) {
      if (error.statusCode === 409 || error.status === 409) {
        results.books = { created: false, message: 'Already exists' }
      } else {
        results.errors.push({ step: 'books', error: error.message })
      }
    }

    return {
      success: results.errors.length === 0,
      results,
      message: 'Test datasets creation completed'
    }
  } catch (error: any) {
    throw createError({
      statusCode: error.statusCode || 500,
      statusMessage: error.message || 'Failed to create test datasets',
    })
  }
})

