<template>
    <div
        v-if="useContainers"
        class="rounded-md px-2 pt-1 pb-0 mb-2 ml-2rem mr-2rem"
        style="background: rgba(120, 128, 138, 0.12)"
    >
        <drop-display
            :item-array="data.artifacts" type="artifact" :menno-data="data.mennoData"
            :ledger-type="ledgerType" :total-drops-count="getTotalCount()" :mission-count="getMissionCount()"
            :lifetime-show-per-ship="lifetimeShowPerShip" :show-expected-drops="showExpectedDrops"
        ></drop-display>
        <drop-display
            :item-array="data.stones" type="stone" :menno-data="data.mennoData"
            :ledger-type="ledgerType" :total-drops-count="getTotalCount()" :mission-count="getMissionCount()"
            :lifetime-show-per-ship="lifetimeShowPerShip" :show-expected-drops="showExpectedDrops"
        ></drop-display>
        <drop-display
            :item-array="data.ingredients" type="ingredient" :menno-data="data.mennoData"
            :ledger-type="ledgerType" :total-drops-count="getTotalCount()" :mission-count="getMissionCount()"
            :lifetime-show-per-ship="lifetimeShowPerShip" :show-expected-drops="showExpectedDrops"
        ></drop-display>
        <drop-display
            :item-array="data.stoneFragments" type="stone_fragment" :menno-data="data.mennoData"
            :ledger-type="ledgerType" :total-drops-count="getTotalCount()" :mission-count="getMissionCount()"
            :lifetime-show-per-ship="lifetimeShowPerShip" :show-expected-drops="showExpectedDrops"
        ></drop-display>
    </div>
    <template v-else>
        <!-- Artifacts -->
        <drop-display
            :item-array="data.artifacts" type="artifact" :menno-data="data.mennoData"
            :ledger-type="ledgerType" :total-drops-count="getTotalCount()" :mission-count="getMissionCount()"
            :lifetime-show-per-ship="lifetimeShowPerShip" :show-expected-drops="showExpectedDrops"
        ></drop-display>
        <!-- Stones -->
        <drop-display
            :item-array="data.stones" type="stone" :menno-data="data.mennoData"
            :ledger-type="ledgerType" :total-drops-count="getTotalCount()" :mission-count="getMissionCount()"
            :lifetime-show-per-ship="lifetimeShowPerShip" :show-expected-drops="showExpectedDrops"
        ></drop-display>
        <!-- Ingredients -->
        <drop-display
            :item-array="data.ingredients" type="ingredient" :menno-data="data.mennoData"
            :ledger-type="ledgerType" :total-drops-count="getTotalCount()" :mission-count="getMissionCount()"
            :lifetime-show-per-ship="lifetimeShowPerShip" :show-expected-drops="showExpectedDrops"
        ></drop-display>
        <!-- Stone Fragments -->
        <drop-display
            :item-array="data.stoneFragments" type="stone_fragment" :menno-data="data.mennoData"
            :ledger-type="ledgerType" :total-drops-count="getTotalCount()" :mission-count="getMissionCount()"
            :lifetime-show-per-ship="lifetimeShowPerShip" :show-expected-drops="showExpectedDrops"
        ></drop-display>
    </template>
</template>

<script lang="ts">
    import { defineComponent, PropType } from 'vue';
    import DropDisplay from './DropDisplay.vue';
    import { InnerDropItem, InnerMennoData } from './InnerDropDisplay.vue';

    export interface LedgerData {
        artifacts: InnerDropItem[];
        stones: InnerDropItem[];
        ingredients: InnerDropItem[];
        stoneFragments: InnerDropItem[];
        mennoData: InnerMennoData;
        missionCount?: number;
    }

    export default defineComponent({
        components: {
            DropDisplay,
        },
        methods: {
            getTotalCount(): number {
                const data = this.data;
                return [
                    ...data.artifacts,
                    ...data.stones,
                    ...data.ingredients,
                    ...data.stoneFragments,
                ].reduce((acc, item) => acc + item.count, 0);
            },
            getMissionCount(): number {
                if(this.ledgerType === 'lifetime') return this.data.missionCount ?? 1;
                else return 1;
            },
        },
        props: {
            data: {
                type: Object as PropType<LedgerData>,
                required: true,
            },
            ledgerType: String,
            lifetimeShowPerShip: Boolean,
            mennoMissionData: {
                type: Object as PropType<Record<string, unknown>>,
                default: null,
            },
            showExpectedDrops: Boolean,
            useContainers: {
                type: Boolean,
                default: false,
            },
        },
    });

</script>
