export default defineEventHandler((event) => {
  return {
    status: 'healthy',
    timestamp: new Date().toISOString()
  }
})

