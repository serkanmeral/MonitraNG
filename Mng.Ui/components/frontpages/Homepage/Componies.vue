<script setup lang="ts">
import { ref } from 'vue';
import comp1 from '/images/front-pages/componies/intel.svg';
import comp2 from '/images/front-pages/componies/oracle.svg';
import comp3 from '/images/front-pages/componies/dell.svg';
import comp4 from '/images/front-pages/componies/samsung.svg';
import comp5 from '/images/front-pages/componies/infosys.svg';
import comp6 from '/images/front-pages/componies/capgemini.svg';
const companies = [
    {
        img: comp1
    },
    {
        img: comp2
    },
    {
        img: comp3
    },
    {
        img: comp4
    },
    {
        img: comp5
    },
    {
        img: comp6
    }
];
</script>
<template>
    <div class="bg-surface">
        <v-container class="max-width-1218 py-0">
            <div class="d-flex flex-wrap justify-md-space-between justify-center align-center gap-4 pt-lg-14 pt-7  border-t">
                <div v-for="item in companies" :key="item.img">
                    <img :src="item.img" alt="company" />
                </div>
            </div>
        </v-container>
    </div>
</template>
