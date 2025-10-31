<script setup lang="ts">

import ClientReviews from '@/components/frontpages/Homepage/ClientReviews.vue';
import Componies from '@/components/frontpages/Homepage/Componies.vue';
import ContactBar from '@/components/frontpages/Homepage/ContactBar.vue';
import FAQ from '@/components/frontpages/Homepage/FAQ.vue';
import FeatureTabs from '@/components/frontpages/Homepage/FeatureTabs.vue';
import Highlights from '@/components/frontpages/Homepage/Highlights.vue';
import Mainbanner from '@/components/frontpages/Homepage/Mainbanner.vue';
import OurClients from '@/components/frontpages/Homepage/OurClients.vue';
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
        <Mainbanner />
        <OurClients />
        <FeatureTabs />
        <OurTeam/>
        <ContactBar/>
        <ClientReviews/>
        <Componies/>
        <Highlights/>
        <Packages/>
        <SecurePayment/>
        <FAQ/>
        <PurchaseTemplate/>
    </DefaultLayout>
</template>
