<template>
  <div class="container mx-auto p-8">
    <div class="text-center">
      <h1 class="text-3xl font-bold mb-4">Domain Management</h1>
      <p class="text-gray-600 mb-6">Welcome to Mng Domain Management UI</p>
      <UButton
        color="primary"
        size="lg"
        to="/domains"
      >
        Go to Domains
      </UButton>
    </div>
  </div>
</template>

<script setup lang="ts">
// Redirect to domains page
definePageMeta({
  middleware: 'auth'
})

// Redirect to domains on mount
onMounted(() => {
  navigateTo('/domains')
})
</script>

