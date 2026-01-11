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

const passwordRules = ref([
  (v: string) => !!v || "Şifre gereklidir",
  (v: string) => (v && v.length >= 3) || "Şifre en az 3 karakter olmalıdır",
]);

const usernameRules = ref([
  (v: string) => !!v || "Kullanıcı adı gereklidir",
]);

// Development ortamında varsayılan değerleri ayarla
onMounted(() => {
  if (process.env.NODE_ENV === 'development') {
    username.value = 'serkan.meral';
    password.value = 'Serkan123!';
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
    
    // Redirect to welcome page
    router.push({ path: "/welcome" });
  } catch (error) {
    if (error instanceof Error) {
      errorMessage.value = error.message;
    } else {
      errorMessage.value = "Giriş başarısız. Lütfen tekrar deneyin.";
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
      >Kullanıcı Adı</v-label
    >
    <VTextField
      v-model="username"
      :rules="usernameRules"
      class="mb-4"
      required
      hide-details="auto"
      placeholder="Kullanıcı adı veya domain@kullaniciadi"
      variant="outlined"
      density="comfortable"
    ></VTextField>

    <!-- Password Field -->
    <v-label class="text-subtitle-1 font-weight-medium pb-2 text-lightText"
      >Şifre</v-label
    >
    <VTextField
      v-model="password"
      :rules="passwordRules"
      required
      hide-details="auto"
      type="password"
      variant="outlined"
      density="comfortable"
      placeholder="Şifrenizi giriniz"
    ></VTextField>

    <!-- Error Message -->
    <div v-if="errorMessage" class="mt-4">
      <v-alert type="error" variant="tonal" density="compact">
        {{ errorMessage }}
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
      Giriş Yap
    </v-btn>
  </Form>
</template>
