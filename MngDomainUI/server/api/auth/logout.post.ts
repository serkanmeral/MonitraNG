export default defineEventHandler(async (event) => {
  // Logout is handled client-side by clearing the auth store
  // In a real implementation, you might want to revoke the token on Keycloak
  return {
    success: true,
    message: 'Logged out successfully'
  }
})

