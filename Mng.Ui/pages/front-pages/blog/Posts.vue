<script setup lang="ts">
import BlogPagesPosts from '@/components/frontpages/Blog/BlogPagesPosts.vue';
import PurchaseTemplate from '@/components/frontpages/Homepage/PurchaseTemplate.vue';
import TextBannerCard from '@/components/frontpages/Shared/TextBannerCard.vue';
import DefaultLayout from '../Layout.vue'
/*for Blank Layout*/
definePageMeta({
  layout: "blank",
});
</script>

<template>
    <DefaultLayout>
        <TextBannerCard title="Latest blog & news" caption="Blog Page" />
        <div class="bg-background">
            <BlogPagesPosts />
        </div>
        <PurchaseTemplate />
    </DefaultLayout>
</template>
