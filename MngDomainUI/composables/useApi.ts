type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH' | 'HEAD' | 'OPTIONS'

export const useApi = () => {
  const config = useRuntimeConfig()
  
  const getBaseUrl = () => {
    if (config.public.gatewayUrl) {
      return `${config.public.gatewayUrl}/keeper`
    }
    return config.public.keeperUrl
  }

  const apiCall = async <T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> => {
    const baseUrl = getBaseUrl()
    const url = `${baseUrl}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`
    
    try {
      // Use $fetch - SSL validation is bypassed by nitro plugin in development (server-side only)
      const method = (options.method?.toUpperCase() || 'GET') as HttpMethod
      
      // @ts-ignore - Type inference issue with $fetch generics
      const response = await $fetch(url, {
        method,
        body: options.body,
        headers: {
          'Content-Type': 'application/json',
          ...(options.headers as Record<string, string>),
        },
      }) as T

      return response
    } catch (error: any) {
      // Better error handling
      const errorMessage = error.message || error.data?.message || 'API call failed'
      throw new Error(errorMessage)
    }
  }

  return {
    getBaseUrl,
    apiCall,
  }
}

