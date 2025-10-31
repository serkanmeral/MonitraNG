<script setup lang="ts">
import { ref } from "vue";
import { Form } from "vee-validate";

import {fetchDataWithoutToken } from '@/services/apiService'; // Servisinizin yolunu düzenleyin
import { decodeJwt } from 'jose';


const router = useRouter();
const checkbox = ref(false);
const valid = ref(false);
const show1 = ref(false);
const password = ref("");
const username = ref("");
const passwordRules = ref([
  (v: string) => !!v || "Password is required",
  (v: string) =>
    (v && v.length <= 10) || "Password must be less than 10 characters",
]);
const emailRules = ref([
  (v: string) => !!v || "Username is required",
  // (v: string) => /.+@.+\..+/.test(v) || "E-mail must be valid",
]);

async function validate() {

  try {

    const apiUrl = '/api/auth/login'
    let bodyObj = {
      username : username.value,
      password : password.value,
    }

    const error = ref(null);
    const tokenCookie = useCookie('access_token')

    fetchDataWithoutToken<{access_token:string}>(apiUrl, 'POST', JSON.stringify(bodyObj))
      .then(data => {
          
          try {
            const decoded = decodeJwt(data.token.access_token);

            tokenCookie.value = data.token.access_token
            localStorage.setItem('userInfo', JSON.stringify( decoded));
            router.push({ path: "/dashboards/modern" });
            
          } catch (error) {
            console.error('JWT Decode Error:', error);
            alert(error || 'Giriş başarısız')
          }

      })
      .catch(err => {
        error.value = err.message;
        alert(error.value || 'Giriş başarısız')
      });

    } catch (error) {

    }
  }

</script>

<template>

  <Form @submit="validate" v-slot="{ errors, isSubmitting }" class="mt-5">
    <v-label class="text-subtitle-1 font-weight-medium pb-2 text-lightText"
      >Username</v-label
    >
    <VTextField
      v-model="username"
      :rules="emailRules"
      class="mb-8"
      required
      hide-details="auto"
    ></VTextField>
    <v-label class="text-subtitle-1 font-weight-medium pb-2 text-lightText"
      >Password</v-label
    >
    <VTextField
      v-model="password"
      :rules="passwordRules"
      required
      hide-details="auto"
      type="password"
      class="pwdInput"
    ></VTextField>
    <div class="d-flex flex-wrap align-center my-3 ml-n2">
      <!-- <v-checkbox
        v-model="checkbox"
        :rules="[(v:any) => !!v || 'You must agree to continue!']"
        required
        hide-details
        color="primary"
      >
        <template v-slot:label class="">Remeber this Device</template>
      </v-checkbox> -->
    </div>
    <v-btn
      size="large"
      :loading="isSubmitting"
      color="primary"
      :disabled="!password"
      block
      type="submit"
      flat
      >Sign In</v-btn
    >
    <div v-if="errors.apiError" class="mt-2">
      <v-alert color="error">{{ errors.apiError }}</v-alert>
    </div>
  </Form>
</template>
