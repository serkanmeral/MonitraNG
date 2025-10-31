<script setup lang="ts">
import LogoIcon from '/images/logos/logoicon.svg';
</script>

<template>
    <div class="bg-primary space-p-96 template position-relative overflow-hidden">
        <img src="/images/front-pages/background/left-widget.png" alt="widget" class="left-widget d-xl-block d-none" />
        <v-container class="max-width-1000">
            <div class="d-flex justify-center">
                <div class="round-56 d-flex justify-center align-center rounded-md bg-white elevation-4 lh-normal">
                    <img :src=LogoIcon alt="Logo" />
                </div>
            </div>
            <h3 class="display-1 font-weight-semibold text-center py-6">Focus on what truly matters—creating stunning, functional designs.</h3>
            <div class="max-w-600 mx-auto">
                <p class="text-18 text-center">
                    Designed for ease of use and customization, this template help you build professional dashboards faster.
                </p>
            </div>
            <div class="d-flex justify-center">
                <v-btn
                    class="mt-6 px-10 m-btn-full"
                    variant="outlined"
                    target="_blank"
                    href="/auth/register"
                    color="white"
                    size="large"
                    flat
                    >Register</v-btn
                >
            </div>
        </v-container>
        <img src="/images/front-pages/background/right-widget.png" alt="widget" class="right-widget d-xl-block d-none" />
    </div>
</template>
