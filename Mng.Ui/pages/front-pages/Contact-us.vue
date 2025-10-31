<script setup lang="ts">
import ContactForm from '@/components/frontpages/ContactUs/ContactForm.vue';
import ContactMap from '@/components/frontpages/ContactUs/ContactMap.vue';
import PurchaseTemplate from '@/components/frontpages/Homepage/PurchaseTemplate.vue';
import TextBannerCard from '@/components/frontpages/Shared/TextBannerCard.vue';
import DefaultLayout from './Layout.vue'
/*for Blank Layout*/
definePageMeta({
  layout: "blank",
});
</script>

<template>
    <DefaultLayout>
        <TextBannerCard title="We’d love to hear from you" caption="Contact Us" />
        <ContactMap/>
        <ContactForm/>
        <PurchaseTemplate />
    </DefaultLayout>
</template>
