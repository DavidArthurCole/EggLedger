<template>
    <div 
        v-if="topLevelBool"
        :class="(isMulti ? ((isFirst ? ' pl-7rem' : ' pl-3rem') + (isLast ? ' pr-7rem' : ' pr-3rem')) : 'overflow-auto pl-7rem pr-7rem' ) + ' text-gray-300 text-center' + ( shipCount > 3 ? ' min-w-30vw' : '') "
    >
        <!-- Header information about the mission -->
        <span :class="durToTextClass(viewMissionData.shipInfo.durationType)">
            {{ viewMissionData.shipInfo.shipString }}
        </span><br />
        <span 
            v-if="viewMissionData.shipInfo.level && viewMissionData.shipInfo.level > 0"
            class="text-star text-goldenstar" 
        >
            {{ "★".repeat(viewMissionData.shipInfo.level) }}
        </span>
        <br v-if="viewMissionData.shipInfo.level && viewMissionData.shipInfo.level > 0" />
        <span>Launched: {{ viewMissionData.launchStr }}</span> <br />
        <span>Returned: {{ viewMissionData.returnStr }}</span> <br />
        <span>Duration: {{ viewMissionData.durationStr }}</span> <br />
        <span class="flex flex-row items-center justify-center">
            <span :class="(viewMissionData.shipInfo.isDubCap ? 'mr-05rem' : '')">Capacity: {{ viewMissionData.shipInfo.capacity }} </span>
                <span v-if="viewMissionData.shipInfo.isDubCap" 
                    class="max-w-28 flex py-1 double-cap-span items-center justify-center flex-1"
                >
                    <img 
                        alt="Artifact Crate" 
                        src="/images/icon_afx_chest_2.png" 
                        class="w-6 mr-05rem"
                    >
                    <span class="tooltip-custom text-xs font-bold">
                        {{viewMissionData.capacityModifier}}x Capacity
                        <span class="font-normal text-sm text-gray-400 tooltiptext-custom speech-bubble">
                        This ship was launched during a<br />
                        <span class="text-dubcap">
                            {{viewMissionData.capacityModifier}}x Capacity Event
                        </span>
                        and returned with<br />
                        more artifacts than normal.
                    </span>
                </span>
            </span>
            <br />
        </span>
        <div v-if="viewMissionData.shipInfo.target != '' && viewMissionData.shipInfo.target.toUpperCase() != 'UNKNOWN'">
            <div class="items-center justify-center flex">
                <span>Sensor Target: </span>
                <div class="ml-1 text-center text-xs rounded-full w-max px-1.5 py-0.5 text-gray-400 bg-darkerer font-semibold">
                    {{ properCase(viewMissionData.shipInfo.target.replaceAll("_", " ")) }}
                </div>
            </div>
            <br/>
        </div>

        <!-- Previous mission -->
        <button 
            v-if="!isMulti"
            v-bind:disabled="viewMissionData.prevMission == null"
            v-on:click="$emit('view', viewMissionData.prevMission)"
            title="Previous mission"
            class="disabled:hover:cursor-not-allowed absolute left-0 top-1/2 transform -translate-y-1/2 pl-2 rounded-md text-gray-400 focus:outline-none z-10 disabled:text-gray-500 hover:text-gray-500"
        >
            <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7">
                </path>
            </svg>
        </button>
        <!-- Next mission -->
        <button 
            v-if="!isMulti"
            v-bind:disabled="viewMissionData.nextMission == null"
            v-on:click="$emit('view', viewMissionData.nextMission)"
            title="Next mission"
            class="disabled:hover:cursor-not-allowed absolute right-0 top-1/2 transform -translate-y-1/2 pr-2 rounded-md text-gray-400 focus:outline-none z-10 disabled:text-gray-500 hover:text-gray-500"
        >
            <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7">
                </path>
            </svg>
        </button>

        <drop-display-container
            :use-gifs-for-rarity="useGifsForRarity" ledger-type="mission"
            :af-rarity-class="afRarityClass" :af-rarity-text="afRarityText"
            :data="viewMissionData" 
            :menno-mission-data="mennoMissionData" :show-expected-drops="showExpectedDrops"
        ></drop-display-container>
        
        <!-- Shamelessly stolen straight from MK2's source code, with mobile note removed -->
        <div v-if="!isMulti" class="mt-2 text-xs text-gray-300 text-center">
            Hover mouse over an item to show details.<br />
            Click to open the relevant <a target="_blank" v-external-link href="https://wasmegg-carpet.netlify.app/artifact-explorer/" class="ledger-underline">
            artifact explorer
            </a> page.
        </div>
    </div>
</template>

<script>
    import DropDisplayContainer from './DropDisplayContainer.vue';

    export default {
        components: {
            DropDisplayContainer,
        },
        emits: [
            'view',
        ],
        props: {
            topLevelBool: Boolean,
            viewMissionData: Object,
            isMulti: Boolean,
            useGifsForRarity: Boolean,
            afRarityClass: Function,
            afRarityText: Function,
            mennoMissionData: Object,
            showExpectedDrops: Boolean,
            shipCount: Number,
            isFirst: Boolean,
            isLast: Boolean,
        },
        methods: {
            durToTextClass(dur){
                switch(dur){
                case 0: return "text-short";
                case 1: return "text-standard";
                case 2: return "text-extended";
                case 3: return "text-tutorial";
                default: return "";
              }
            },
            properCase(string) {
              string = string.toLowerCase();
              // Capitalize the first letter of each word, unless it is 'of' or 'the'
              const words = string.split(" ");
              for (let i = 0; i < words.length; i++) {
                if (words[i] !== "of" && words[i] !== "the") {
                  words[i] = words[i].charAt(0).toUpperCase() + words[i].slice(1);
                }
              }
              const finalString = words.join(" ");
              return finalString.charAt(0).toUpperCase() + finalString.slice(1);
            },
        },
    }

</script>