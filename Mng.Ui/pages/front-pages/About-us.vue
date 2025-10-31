<script setup lang="ts">
import Archivement from '@/components/frontpages/AboutUs/Archivement.vue';
import HeroText from '@/components/frontpages/AboutUs/HeroText.vue';
import SetupProcess from '@/components/frontpages/AboutUs/SetupProcess.vue';
import ClientReviews from '@/components/frontpages/Homepage/ClientReviews.vue';
import Componies from '@/components/frontpages/Homepage/Componies.vue';
import FAQ from '@/components/frontpages/Homepage/FAQ.vue';
import Highlights from '@/components/frontpages/Homepage/Highlights.vue';
import OurTeam from '@/components/frontpages/Homepage/OurTeam.vue';
import Packages from '@/components/frontpages/Homepage/Packages.vue';
import PurchaseTemplate from '@/components/frontpages/Homepage/PurchaseTemplate.vue';
import SecurePayment from '@/components/frontpages/Homepage/SecurePayment.vue';
import DefaultLayout from './Layout.vue'
/*for Blank Layout*/
definePageMeta({
  layout: "blank",
});
</script>

<template>
    <DefaultLayout>
        <HeroText />
        <SetupProcess />
        <Archivement />
        <OurTeam />
        <ClientReviews />
        <Componies />
        <Highlights />
        <Packages />
        <SecurePayment />
        <FAQ />
        <PurchaseTemplate />
    </DefaultLayout>
</template>
