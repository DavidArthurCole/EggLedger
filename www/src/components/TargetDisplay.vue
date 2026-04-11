<template>
    <img v-if="targetImageResult !== null"
         :src="targetImageResult[0]"
         :alt="targetImageResult[1]"
         class="target-ico" />
  </template>

  <script lang="ts">
  import { defineComponent } from 'vue';

  export default defineComponent({
    props: {
      target: String,
    },
    methods: {
      targetImage(target: string | undefined): [string, string] | null {
        if (target == null || target === "" || target === "UNKNOWN") {
          return null; // Return null if target is empty or UNKNOWN
        }
        let t = target.toUpperCase()
            .replaceAll("ORNATE_GUSSET", "GUSSET")
            .replaceAll("VIAL_MARTIAN_DUST", "VIAL_OF_MARTIAN_DUST");
        let tier = 4;
        if (t.includes('_FRAGMENT')) {
            t = t.replace('_FRAGMENT', '');
            tier = 1;
        }
        if (t === 'GOLD_METEORITE' || t === 'TAU_CETI_GEODE' || t === 'SOLAR_TITANIUM') {
            tier = 3;
        }
        const path = `images/artifacts/${t}/${t}_${tier}.png`;
        return [path, t];
      },
    },
    computed: {
      targetImageResult(): [string, string] | null {
        return this.targetImage(this.target);
      },
    },
  });
  </script>
