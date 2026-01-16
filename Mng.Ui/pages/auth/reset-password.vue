<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useUserStore } from '@/stores/apps/user';
import { Form, Field } from 'vee-validate';
import * as yup from 'yup';

definePageMeta({
  layout: "blank",
});


const route = useRoute();
const router = useRouter();
const userStore = useUserStore();

const token = ref<string>('');
const loading = ref(false);
const validatingToken = ref(true);
const tokenValid = ref(false);
const tokenError = ref<string | null>(null);
const resetSuccess = ref(false);

// Password validation schema
const passwordSchema = yup.object({
  password: yup
    .string()
    .required('Şifre gereklidir')
    .min(8, 'Şifre en az 8 karakter olmalıdır')
    .matches(/[A-Z]/, 'Şifre en az bir büyük harf içermelidir')
    .matches(/[a-z]/, 'Şifre en az bir küçük harf içermelidir')
    .matches(/[0-9]/, 'Şifre en az bir rakam içermelidir')
    .matches(/[^A-Za-z0-9]/, 'Şifre en az bir özel karakter içermelidir'),
  confirmPassword: yup
    .string()
    .required('Şifre tekrarı gereklidir')
    .oneOf([yup.ref('password')], 'Şifreler eşleşmiyor'),
});

const password = ref('');
const confirmPassword = ref('');
const showPassword = ref(false);
const showConfirmPassword = ref(false);

// Validate token on mount
onMounted(async () => {
  const tokenParam = route.query.token as string;
  
  if (!tokenParam) {
    tokenError.value = 'Geçersiz veya eksik token.';
    validatingToken.value = false;
    return;
  }
  
  token.value = tokenParam;
  validatingToken.value = false;
  tokenValid.value = true; // Token validation will be done on submit
});

const onSubmit = async (values: any) => {
  if (!token.value) {
    tokenError.value = 'Geçersiz veya eksik token.';
    return;
  }
  
  loading.value = true;
  tokenError.value = null;
  resetSuccess.value = false;
  
  try {
    const result = await userStore.resetPassword(token.value, values.password);
    
    if (result.isSuccess) {
      resetSuccess.value = true;
      // Redirect to login after 3 seconds
      setTimeout(() => {
        router.push('/auth/login');
      }, 3000);
    } else {
      tokenError.value = result.error || 'Şifre sıfırlama başarısız oldu.';
    }
  } catch (error: any) {
    tokenError.value = error.message || 'Bir hata oluştu.';
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <div class="pa-3">
    <v-row class="h-100vh mh-100 auth">
      <v-col cols="12" lg="8" xl="8" xxl="9" class="d-lg-flex align-center justify-center authentication position-relative">
        <div class="auth-header pt-sm-6 pt-2 px-sm-6 px-3 pb-sm-6 pb-0">
          <div class="position-relative">
            <LcFullLogoAuthLogo/>
          </div>
        </div>
        <div class="">
          <img src="/images/backgrounds/login-bg.svg" height="450" class="position-relative d-none d-lg-flex" alt="login-background" />
        </div>
      </v-col>
      <v-col cols="12" lg="4" xl="4" xxl="3" class="d-flex align-center justify-center bg-surface">
        <div class="pa-sm-7 pa-4" style="width: 100%; max-width: 450px;">
          <h2 class="text--darken-2 text-h4 font-weight-semibold">Yeni Şifre Belirle</h2>
          <p class="text-subtitle-1 py-4 text-10">
            Yeni şifrenizi belirleyin. Şifreniz güvenli olmalıdır.
          </p>

          <!-- Token Error -->
          <v-alert
            v-if="tokenError"
            type="error"
            variant="tonal"
            class="mb-4"
            closable
            @click:close="tokenError = null"
          >
            {{ tokenError }}
          </v-alert>

          <!-- Success Message -->
          <v-alert
            v-if="resetSuccess"
            type="success"
            variant="tonal"
            class="mb-4"
          >
            Şifreniz başarıyla sıfırlandı. Giriş sayfasına yönlendiriliyorsunuz...
          </v-alert>

          <!-- Loading State -->
          <div v-if="validatingToken" class="text-center py-8">
            <v-progress-circular indeterminate color="primary" />
            <p class="text-subtitle-1 mt-4">Token doğrulanıyor...</p>
          </div>

          <!-- Reset Password Form -->
          <Form
            v-else-if="tokenValid && !resetSuccess"
            v-slot="{ handleSubmit }"
            :validation-schema="passwordSchema"
            @submit="onSubmit"
            class="mt-sm-13 mt-8"
          >
            <v-form @submit.prevent="handleSubmit(onSubmit)">
              <v-label class="text-subtitle-1 font-weight-medium pb-2 text-lightText">Yeni Şifre</v-label>
              <Field name="password" v-slot="{ field, errors: fieldErrors }">
                <VTextField
                  v-bind="field"
                  v-model="password"
                  :type="showPassword ? 'text' : 'password'"
                  :error-messages="fieldErrors"
                  required
                  placeholder="Yeni şifrenizi girin"
                >
                  <template v-slot:append-inner>
                    <v-icon
                      @click="showPassword = !showPassword"
                      :icon="showPassword ? 'mdi-eye-off' : 'mdi-eye'"
                      size="20"
                    />
                  </template>
                </VTextField>
              </Field>

              <v-label class="text-subtitle-1 font-weight-medium pb-2 text-lightText mt-4">Şifre Tekrar</v-label>
              <Field name="confirmPassword" v-slot="{ field, errors: fieldErrors }">
                <VTextField
                  v-bind="field"
                  v-model="confirmPassword"
                  :type="showConfirmPassword ? 'text' : 'password'"
                  :error-messages="fieldErrors"
                  required
                  placeholder="Şifrenizi tekrar girin"
                >
                  <template v-slot:append-inner>
                    <v-icon
                      @click="showConfirmPassword = !showConfirmPassword"
                      :icon="showConfirmPassword ? 'mdi-eye-off' : 'mdi-eye'"
                      size="20"
                    />
                  </template>
                </VTextField>
              </Field>

              <!-- Password Requirements -->
              <v-card variant="outlined" class="mt-4 pa-3">
                <v-card-title class="text-subtitle-2 font-weight-medium pa-0 mb-2">Şifre Gereksinimleri:</v-card-title>
                <v-list density="compact" class="pa-0">
                  <v-list-item class="pa-0">
                    <template v-slot:prepend>
                      <v-icon size="16" :color="password && password.length >= 8 ? 'success' : 'default'">
                        {{ password && password.length >= 8 ? 'mdi-check-circle' : 'mdi-circle-outline' }}
                      </v-icon>
                    </template>
                    <v-list-item-title class="text-caption">En az 8 karakter</v-list-item-title>
                  </v-list-item>
                  <v-list-item class="pa-0">
                    <template v-slot:prepend>
                      <v-icon size="16" :color="password && /[A-Z]/.test(password) ? 'success' : 'default'">
                        {{ password && /[A-Z]/.test(password) ? 'mdi-check-circle' : 'mdi-circle-outline' }}
                      </v-icon>
                    </template>
                    <v-list-item-title class="text-caption">En az bir büyük harf</v-list-item-title>
                  </v-list-item>
                  <v-list-item class="pa-0">
                    <template v-slot:prepend>
                      <v-icon size="16" :color="password && /[a-z]/.test(password) ? 'success' : 'default'">
                        {{ password && /[a-z]/.test(password) ? 'mdi-check-circle' : 'mdi-circle-outline' }}
                      </v-icon>
                    </template>
                    <v-list-item-title class="text-caption">En az bir küçük harf</v-list-item-title>
                  </v-list-item>
                  <v-list-item class="pa-0">
                    <template v-slot:prepend>
                      <v-icon size="16" :color="password && /[0-9]/.test(password) ? 'success' : 'default'">
                        {{ password && /[0-9]/.test(password) ? 'mdi-check-circle' : 'mdi-circle-outline' }}
                      </v-icon>
                    </template>
                    <v-list-item-title class="text-caption">En az bir rakam</v-list-item-title>
                  </v-list-item>
                  <v-list-item class="pa-0">
                    <template v-slot:prepend>
                      <v-icon size="16" :color="password && /[^A-Za-z0-9]/.test(password) ? 'success' : 'default'">
                        {{ password && /[^A-Za-z0-9]/.test(password) ? 'mdi-check-circle' : 'mdi-circle-outline' }}
                      </v-icon>
                    </template>
                    <v-list-item-title class="text-caption">En az bir özel karakter</v-list-item-title>
                  </v-list-item>
                </v-list>
              </v-card>

              <v-btn
                size="large"
                color="primary"
                type="submit"
                block
                class="mt-6"
                :loading="loading"
                :disabled="loading"
                flat
              >
                Şifreyi Sıfırla
              </v-btn>
            </v-form>
          </Form>

          <v-btn
            size="large"
            color="lightprimary"
            to="/auth/login"
            block
            class="mt-5 text-primary"
            flat
          >
            Giriş Sayfasına Dön
          </v-btn>
        </div>
      </v-col>
    </v-row>
  </div>
</template>
