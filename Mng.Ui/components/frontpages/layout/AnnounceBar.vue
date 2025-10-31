<script setup lang="ts">
import { RouterLink } from 'vue-router';
import shape from '/images/front-pages/background/right-shape.png';
import { XIcon } from 'vue-tabler-icons';
import { ref } from 'vue';

// Reactive state to control visibility of the offer bar
const isVisible = ref(true);

// Function to hide the offer bar
const hideOfferBar = () => {
    isVisible.value = false;
};
</script>

<template>
    <div v-if="isVisible" class="bg-textSecondary py-2 offerbar d-sm-flex justify-center gap-4 align-center position-relative px-4 overflow-hidden">
        <div class="d-flex ga-sm-4 ga-2 align-center">
            <v-btn class="white-btn text-white" size="small" href=""  target="_blank">New</v-btn>
            <p class="text-13 font-weight-medium position-relative z-1 pr-md-0 pr-5">Frontend Pages Added</p>
        </div>
       
       
        <XIcon size="25" class="pa-1 z-1 cursor-pointer announce-close" @click="hideOfferBar" />
    </div>
</template>
