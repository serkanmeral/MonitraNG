<script setup lang="ts">
import AutomatedFormForm from '@/components/apps/automated-forms/AutomatedFormForm.vue';

const route = useRoute();
const formCode = route.params.formCode as string;
</script>

<template>
  <AutomatedFormForm :form-code="formCode" />
</template>
