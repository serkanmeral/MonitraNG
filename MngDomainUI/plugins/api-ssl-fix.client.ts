// Client-side plugin to handle SSL certificate issues in development
// Note: Browser cannot bypass SSL validation, so we need to use server-side API routes
// This plugin is a placeholder for future server-side API route implementation

export default defineNuxtPlugin(() => {
  // Browser-side SSL validation cannot be bypassed
  // For development, consider:
  // 1. Using server-side API routes (recommended)
  // 2. Adding the certificate to browser's trusted certificates
  // 3. Using HTTP instead of HTTPS in development
})

