<script setup lang="ts">
import Icon from "../Icon.vue";

interface Props {
  item: {
    icon?: any;
    iconType?: 'mdi' | 'tabler';
    iconName?: string;
    title?: string;
    header?: string;
    subCaption?: string;
    children?: any[];
  };
  level?: number;
}

const props = withDefaults(defineProps<Props>(), {
  level: 0,
});
</script>

<template>
  <!-- ---------------------------------------------- -->
  <!---Item Childern -->
  <!-- ---------------------------------------------- -->
  <v-list-group no-action>
    <!-- ---------------------------------------------- -->
    <!---Dropdown  -->
    <!-- ---------------------------------------------- -->
    <template v-slot:activator="{ props }">
      <v-list-item
        v-bind="props"
        :value="item.title"
        rounded
        class="mb-1"
       
      >
        <!---Icon  -->
        <template v-slot:prepend>
          <Icon 
            :item="item.icon" 
            :iconName="item.iconName || (typeof item.icon === 'string' ? item.icon : null)"
            :iconType="item.iconType || 'tabler'"
            :level="level" 
          />
        </template>
        <!---Title  -->
        <v-list-item-title
          class="mr-auto"
        >{{ item.title ? $t(item.title) : '' }}</v-list-item-title>
        <!---If Caption-->
        <v-list-item-subtitle
          v-if="item.subCaption"
          class="text-caption mt-n1 hide-menu"
        >
          {{ item.subCaption }}
        </v-list-item-subtitle>
      </v-list-item>
    </template>
    <!-- ---------------------------------------------- -->
    <!---Sub Item-->
    <!-- ---------------------------------------------- -->
    <template
      v-for="(subitem, i) in item.children"
      :key="i"
      v-if="item.children"
    >
      <!-- Nested Header: Eğer subitem bir header ise -->
      <template v-if="subitem.header">
        <LcFullVerticalSidebarNavGroup :item="subitem" />
        <!-- Nested header'ın children'larını recursive olarak render et -->
        <template v-if="subitem.children && subitem.children.length > 0">
          <LcFullVerticalSidebarNavCollapse 
            v-for="(grandchild, k) in subitem.children" 
            :key="`grandchild-${i}-${k}`"
            v-if="grandchild.children && grandchild.children.length > 0"
            :item="grandchild" 
            :level="level + 1" 
          />
          <LcFullVerticalSidebarNavItem 
            v-for="(grandchild, k) in subitem.children" 
            :key="`grandchild-item-${i}-${k}`"
            v-else
            :item="grandchild" 
            :level="level + 1" 
          />
        </template>
      </template>
      <!-- Normal Item veya Collapse: Eğer subitem header değilse -->
      <template v-else>
        <LcFullVerticalSidebarNavCollapse :item="subitem" v-if="subitem.children && subitem.children.length > 0" :level="level + 1" />
        <LcFullVerticalSidebarNavItem :item="subitem" :level="level + 1" v-else></LcFullVerticalSidebarNavItem>
      </template>
    </template>
  </v-list-group>

  <!-- ---------------------------------------------- -->
  <!---End Item Sub Header -->
  <!-- ---------------------------------------------- -->
</template>
