<script setup lang="ts">
import { RouterLink } from 'vue-router';
import { ChevronRightIcon } from 'vue-tabler-icons';

const props = defineProps({
    caption: String,
    title: String
});
</script>
<template>
    <div class="overflow-hidden bg-background pb-sm-14 pb-7 pt-sm-8">
        <v-container class="max-width-1218">
            <div class="d-flex flex-wrap justify-sm-space-between justify-center align-center">
                <h2 class="text-44 font-weight-semibold textPrimary text-align-start">{{ title }}</h2>
                <div className="d-flex  align-center ga-4 text-uppercase text-14 font-weight-semibold mt-md-0 mt-4">
                    <span className="text-muted">MaterialPro</span>
                    <ChevronRightIcon size="15" />
                    <RouterLink to="" className="text-primary text-decoration-none"> {{ caption }} </RouterLink>
                </div>
            </div>
        </v-container>
    </div>
</template>
