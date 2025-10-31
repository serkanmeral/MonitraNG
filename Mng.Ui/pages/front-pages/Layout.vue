<script setup lang="ts">
import AnnounceBar from '@/components/frontpages/layout/AnnounceBar.vue';
import Footer from '@/components/frontpages/layout/Footer.vue';
import Header from '@/components/frontpages/layout/Header.vue';

</script>
<template>
    <div class="front-wraper bg-surface">
      <AnnounceBar/>  
      <Header />
      <main>
        <slot></slot> <!-- This slot will be replaced with page-specific content -->
      </main>
      <Footer />
    </div>
  </template>