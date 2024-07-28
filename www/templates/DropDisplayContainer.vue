<template>
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

<script>
    import DropDisplay from './DropDisplay.vue';

    export default {
        components: {
            DropDisplay,
        },
        methods: {
            getTotalCount(){
                const data = this.data;
                return [
                    ...data.artifacts,
                    ...data.stones,
                    ...data.ingredients,
                    ...data.stoneFragments,
                ].reduce((acc, item) => acc + item.count, 0);
            },
            getMissionCount() {
                if(this.ledgerType === 'lifetime') return this.data.missionCount;
                else return 1;
            }
        },
        props: {
            data: Object,
            ledgerType: String,
            lifetimeShowPerShip: Boolean,
            mennoMissionData: Object,
            showExpectedDrops: Boolean,
        },
    }

</script>