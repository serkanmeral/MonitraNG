<script setup lang="ts">
/**
 * Kök `error.vue` Nuxt’ta “sayfa” sayılmaz — definePageMeta burada geçersizdir
 * ve Vue uyarısı / ek hata üretir (konsoldaki definePageMeta uyarısı).
 */
defineProps<{
  error?: {
    statusCode?: number;
    statusMessage?: string;
    message?: string;
  };
}>();

function goHome() {
  clearError({ redirect: '/' });
}
</script>

<template>
  <div class="v-application d-flex bg-background">
    <div class="nopage">
      <div class="d-flex justify-center align-center text-center h-100">
        <div>
          <img src="/images/backgrounds/errorimg.svg" width="500" alt="" />
          <h1 class="text-h1 pt-3">Opps!!!</h1>
          <h4 class="text-h4 my-8">
            {{ error?.statusMessage || error?.message || 'This page you are looking for could not be found.' }}
          </h4>
          <button type="button" class="mb-4 text-primary text-decoration-underline bg-transparent border-0 cursor-pointer" @click="goHome">
            Go Back to Home
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
