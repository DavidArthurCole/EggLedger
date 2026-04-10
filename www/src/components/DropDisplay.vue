<template>
    <div v-if="itemArray.length > 0">
        <div :class="getLabelClassList()">
            {{ getLabelDisplayValue() }} ({{ itemArray.reduce((acc, item) => acc + item.count, 0).toString() }})
        </div>
        <inner-drop-display
            :item-array="itemArray" :menno-data="mennoData"
            :ledger-type="ledgerType" :total-drops-count="totalDropsCount" :mission-count="missionCount"
            :lifetime-show-per-ship="lifetimeShowPerShip" :show-expected-drops="showExpectedDrops"
            :options="options"
        ></inner-drop-display>
        <br />
    </div>
</template>

<script lang="ts">
    import { defineComponent, PropType } from 'vue';
    import InnerDropDisplay, { InnerDropItem, InnerMennoData, InnerDisplayOptions } from './InnerDropDisplay.vue';

    export default defineComponent({
        components: {
            InnerDropDisplay,
        },
        props: {
            itemArray: {
                type: Array as PropType<InnerDropItem[]>,
                default: () => [],
            },
            type: String,
            ledgerType: String,
            lifetimeShowPerShip: Boolean,
            showExpectedDrops: Boolean,
            totalDropsCount: Number,
            missionCount: Number,
            mennoData: {
                type: Object as PropType<InnerMennoData>,
                default: null,
            },
            options: {
                type: Object as PropType<InnerDisplayOptions>,
                default: null,
            },
        },
        methods: {
            getLabelDisplayValue(): string {
                switch(this.type){
                    case 'artifact': return 'Artifacts';
                    case 'stone': return 'Eggfinity Stones';
                    case 'ingredient': return 'Ingredients';
                    case 'stone_fragment': return 'Stone Fragments';
                    default: return 'Unknown';
                }
            },
            getLabelClassList(): string {
                let colorClass = '';
                switch(this.type){
                    case 'artifact': colorClass = 'bg-blue-900'; break;
                    case 'stone': colorClass = 'bg-fuchsia-900'; break;
                    case 'ingredient': colorClass = 'text-gray-400 bg-darkerer'; break;
                    case 'stone_fragment': colorClass = 'text-gray-400 bg-darkerer'; break;
                    default: colorClass = 'bg-gray-900'; break;
                }
                return 'mission-view-div ' + colorClass;
            },
        },
    });
</script>
