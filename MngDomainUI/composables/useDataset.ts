// Composable for MngDataGateway dataset operations

export const useDataset = () => {
  const config = useRuntimeConfig()
  
  const getDataGatewayUrl = () => {
    if (config.public.gatewayUrl) {
      return `${config.public.gatewayUrl}/data`
    }
    // Default to DataGateway URL if available
    return config.public.datagatewayUrl || 'https://localhost:5010'
  }

  // Create dataset category
  const createCategory = async (categoryData: {
    categoryName: string
    categoryDescription: string
  }) => {
    const baseUrl = getDataGatewayUrl()
    return $fetch(`${baseUrl}/api/dataset-categories`, {
      method: 'POST',
      body: categoryData,
    })
  }

  // Create dataset
  const createDataset = async (datasetData: any) => {
    const baseUrl = getDataGatewayUrl()
    return $fetch(`${baseUrl}/api/datasets`, {
      method: 'POST',
      body: datasetData,
    })
  }

  // Insert data into dataset
  const insertData = async (datasetName: string, data: any) => {
    const baseUrl = getDataGatewayUrl()
    return $fetch(`${baseUrl}/api/data/${datasetName}`, {
      method: 'POST',
      body: data,
    })
  }

  // Get data from dataset
  const getData = async (datasetName: string, query?: string) => {
    const baseUrl = getDataGatewayUrl()
    const url = query ? `${baseUrl}/api/data/${datasetName}?${query}` : `${baseUrl}/api/data/${datasetName}`
    return $fetch(url)
  }

  // Create test datasets (books datasets)
  const createTestDatasets = async (
    domainName: string,
    credentials?: { adminEmail: string; adminPassword: string; token?: string }
  ) => {
    return $fetch('/api/datagateway/create-test-datasets', {
      method: 'POST',
      body: {
        domainName,
        ...(credentials && {
          adminEmail: credentials.adminEmail,
          adminPassword: credentials.adminPassword,
          token: credentials.token
        })
      },
    })
  }

  // Insert test data
  const insertTestData = async (
    domainName: string,
    credentials?: { adminEmail: string; adminPassword: string; token?: string }
  ) => {
    return $fetch('/api/datagateway/insert-test-data', {
      method: 'POST',
      body: {
        domainName,
        ...(credentials && {
          adminEmail: credentials.adminEmail,
          adminPassword: credentials.adminPassword,
          token: credentials.token
        })
      },
    })
  }

  // Create test users
  const createTestUsers = async (
    domainName: string,
    credentials?: { adminEmail: string; adminPassword: string; token?: string; userCount?: number; defaultPassword?: string }
  ) => {
    return $fetch('/api/datagateway/create-test-users', {
      method: 'POST',
      body: {
        domainName,
        ...(credentials && {
          adminEmail: credentials.adminEmail,
          adminPassword: credentials.adminPassword,
          token: credentials.token,
          userCount: credentials.userCount || 5,
          defaultPassword: credentials.defaultPassword || 'Test123!'
        })
      },
    })
  }

  // Create test groups
  const createTestGroups = async (
    domainName: string,
    credentials?: { adminEmail: string; adminPassword: string; token?: string }
  ) => {
    return $fetch('/api/datagateway/create-test-groups', {
      method: 'POST',
      body: {
        domainName,
        ...(credentials && {
          adminEmail: credentials.adminEmail,
          adminPassword: credentials.adminPassword,
          token: credentials.token
        })
      },
    })
  }

  return {
    createCategory,
    createDataset,
    insertData,
    getData,
    createTestDatasets,
    insertTestData,
    createTestUsers,
    createTestGroups,
  }
}

