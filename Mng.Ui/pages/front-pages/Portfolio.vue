<script setup lang="ts">
import TextBannerCard from '@/components/frontpages/Shared/TextBannerCard.vue';

import GalleryBox from '@/components/frontpages/Portfolio/GalleryBox.vue';
import PurchaseTemplate from '@/components/frontpages/Homepage/PurchaseTemplate.vue';
import DefaultLayout from './Layout.vue'
/*for Blank Layout*/
definePageMeta({
  layout: "blank",
});
</script>

<template>
    <DefaultLayout>
        <TextBannerCard title="Explore Our Latest Works" caption="Portfolio" />
        <GalleryBox/>
        <PurchaseTemplate />
    </DefaultLayout>
</template>
