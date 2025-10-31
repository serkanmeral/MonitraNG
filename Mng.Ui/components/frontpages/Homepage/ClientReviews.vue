<script setup lang="ts">
import { ref } from 'vue';
import { Carousel, Slide, Navigation, Pagination } from 'vue3-carousel';

import { ChevronLeftIcon, ChevronRightIcon } from 'vue-tabler-icons';
import { userReviewFrront } from '@/_mockApis/front-pages/PagesData';

// Counter tracking
const currentSlide = ref(0); // Start from the first slide
const totalSlides = userReviewFrront.length; // Total number of slides

// Method to update the current slide on manual change
const goToNextSlide = () => {
    if (currentSlide.value < totalSlides - 1) {
        currentSlide.value++;
    } else {
        currentSlide.value = 0; // Loop back to the first slide
    }
};

const goToPrevSlide = () => {
    if (currentSlide.value > 0) {
        currentSlide.value--;
    } else {
        currentSlide.value = totalSlides - 1; // Loop back to the last slide
    }
};
</script>
<template>
    <div class="pt-96 pb-lg-14 pb-10 bg-surface testimonials">
        <v-container class="max-width-1218 position-relative" >
            <v-row>
                <v-col cols="12" lg="5">
                    <h2 class="display-1 textPrimary font-weight-semibold">Words from customers.</h2>
                    <p class="text-textPrimary opacity-80 py-6 text-17 lh-32 pe-lg-4 font-weight-medium">Pellentesque varius semper odio non pretium.</p>
                    <div class="slide-counter d-xl-block d-none">{{ currentSlide + 1 }} / {{ totalSlides }}</div>
                </v-col>
                <v-col cols="12" lg="7">
                    <carousel :itemsToShow="1" :wrapAround="true" :transition="500" class="" :current-slide="currentSlide">
                        <slide v-for="(card, index) in userReviewFrront" :key="card.img">
                            <v-card elevation="0">
                                <div class="text-left">
                                    <p class="text-24 textPrimary clamped-text mb-6">{{ card.review }}</p>
                                    <div class="d-flex ga-4 align-center justify-space-between rtl-reviews pt-4">
                                        <div class="d-flex ga-4 align-center">
                                            <v-avatar size="56">
                                                <img :src="card.img" :alt="card.img" width="56" />
                                            </v-avatar>
                                            <div>
                                                <h6 class="text-h5 font-semibold textPrimary">{{ card.title }}</h6>
                                                <p class="text-15 text-textPrimary opacity-80">{{ card.subtitle }}</p>
                                            </div>
                                        </div>
                                        <div class="round-48 rounded-circle bg-primary d-flex justify-center align-center">
                                            <img src="/images/front-pages/background/quotes.svg" alt="reviews" width="20" />
                                        </div>
                                    </div>
                                </div>
                            </v-card>
                        </slide>

                        <template #addons>
                            <navigation class="navarrow">
                                <template #next>
                                    <span @click="goToNextSlide">
                                        <ChevronRightIcon class="textPrimary rtlnav" size="20" stroke-width="1.5" />
                                    </span>
                                </template>
                                <template #prev>
                                    <span @click="goToPrevSlide">
                                        <ChevronLeftIcon class="textPrimary rtlnav" size="20" stroke-width="1.5" />
                                    </span>
                                </template>
                            </navigation>

                            <Pagination class="mt-6 d-xl-none d-flex" />
                        </template>
                        
                    </carousel>
                </v-col>
            </v-row>
        </v-container>
    </div>
</template>
