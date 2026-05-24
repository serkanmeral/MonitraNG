<script setup lang="ts">
import { ref, onMounted } from "vue";
import { Form } from "vee-validate";
import { useAuthStore } from "@/stores/auth";

const router = useRouter();
const authStore = useAuthStore();

const password = ref("");
const username = ref("");
const domain = ref("");
const errorMessage = ref("");
const isLoading = ref(false);

// Note: Validation rules will use $t() in template, but we need to access i18n in script
// Since vue-i18n is in legacy mode, we'll use hardcoded fallback messages
// The actual translations will be shown via $t() in the template
const passwordRules = ref([
  (v: string) => !!v || "Şifre gereklidir", // Fallback - will be overridden by $t() in template
  (v: string) => (v && v.length >= 3) || "Şifre en az 3 karakter olmalıdır", // Fallback
]);

const usernameRules = ref([
  (v: string) => !!v || "Kullanıcı adı gereklidir", // Fallback
]);

// Development: Odak sunucu pilot girişi (domain@kullanıcı → auth store domain ayrıştırır)
onMounted(() => {
  if (process.env.NODE_ENV === 'development') {
    username.value = 'odak@odak_admin';
    password.value = 'Admin123!';
  }
});


async function validate() {
  errorMessage.value = "";
  isLoading.value = true;

    try {
    // Parse domain from username if format is "domain@username"
    let selectedDomain: string | undefined = undefined;
    let selectedUsername = username.value;

    if (username.value.includes("@")) {
      const parts = username.value.split("@");
      if (parts.length === 2) {
        selectedDomain = parts[0];
        selectedUsername = parts[1];
      }
    }

    await authStore.login(selectedUsername, password.value, selectedDomain);
    
    router.push({ path: "/" });
  } catch (error) {
    if (error instanceof Error) {
      errorMessage.value = error.message;
    } else {
      // Note: This will be shown in template, but we can't use $t() here in script setup
      // The template will handle the translation
      errorMessage.value = "Giriş başarısız. Lütfen tekrar deneyin."; // Fallback
    }
  } finally {
    isLoading.value = false;
  }
}

</script>

<template>
  <Form @submit="validate" v-slot="{ errors, isSubmitting }" class="mt-5">
    <!-- Username Field -->
    <v-label class="text-subtitle-1 font-weight-medium pb-2 text-lightText"
      >{{ $t('login.form.username') }}</v-label
    >
    <VTextField
      v-model="username"
      :rules="[
        (v: string) => !!v || $t('login.validation.usernameRequired'),
      ]"
      class="mb-4"
      required
      hide-details="auto"
      :placeholder="String($t('login.form.usernamePlaceholder')).replace(/\{'@'\}/g, '@')"
      variant="outlined"
      density="comfortable"
    ></VTextField>

    <!-- Password Field -->
    <v-label class="text-subtitle-1 font-weight-medium pb-2 text-lightText"
      >{{ $t('login.form.password') }}</v-label
    >
    <VTextField
      v-model="password"
      :rules="[
        (v: string) => !!v || $t('login.validation.passwordRequired'),
        (v: string) => (v && v.length >= 3) || $t('login.validation.passwordMinLength'),
      ]"
      required
      hide-details="auto"
      type="password"
      variant="outlined"
      density="comfortable"
      :placeholder="$t('login.form.passwordPlaceholder')"
    ></VTextField>

    <!-- Error Message -->
    <div v-if="errorMessage" class="mt-4">
      <v-alert type="error" variant="tonal" density="compact">
        {{ errorMessage || $t('login.form.error') }}
      </v-alert>
    </div>

    <!-- Submit Button -->
    <v-btn
      size="large"
      :loading="isLoading || isSubmitting"
      color="primary"
      :disabled="!username || !password"
      block
      type="submit"
      class="mt-6"
      flat
    >
      {{ $t('login.form.submit') }}
    </v-btn>
  </Form>
</template>
