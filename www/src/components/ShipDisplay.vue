<template>
    <div
        :class="(isMulti ? ((isFirst ? ' pl-7rem' : ' pl-3rem') + (isLast ? ' pr-7rem' : ' pr-3rem')) : 'overflow-auto pl-7rem pr-7rem' ) + ' text-gray-300 text-center' + ( (shipCount ?? 0) > 3 ? ' min-w-30vw' : '') "
    >
        <!-- Header information about the mission -->
        <span :class="'text-duration-' + missionInfo.durationType">
            {{ missionInfo.shipString }}
        </span><br />
        <span
            v-if="missionInfo.level && missionInfo.level > 0"
            class="text-star text-goldenstar"
        >
            {{ "★".repeat(missionInfo.level) }}
        </span>
        <br v-if="missionInfo.level && missionInfo.level > 0" />
        <span>Launched: {{ formatDate(viewMissionData.launchDT) }}</span> <br />
        <span>Returned: {{ formatDate(viewMissionData.returnDT) }}</span> <br />
        <span>Duration: {{ viewMissionData.durationStr }}</span> <br />
        <span class="flex flex-row items-center justify-center">
        <span :class="((missionInfo.isDubCap || missionInfo.isBuggedCap) ? 'mr-0_5rem' : '')">Capacity: {{ missionInfo.capacity }} </span>
            <span v-if="missionInfo.isBuggedCap" class="max-w-32 flex py-1 bugged-cap-span items-center justify-center flex-1">
                <img alt="Skull Emoji" :src="'images/skull.png'" class="w-6 mr-0_5rem">
                <span
                    class="text-xs font-bold"
                    @mouseenter="(e) => showTooltip('bugged', e)"
                    @mouseleave="hideTooltip"
                >
                    0.6x Capacity
                </span>
            </span>
            <span v-if="!missionInfo.isBuggedCap && missionInfo.isDubCap" class="max-w-28 flex py-1 double-cap-span items-center justify-center flex-1">
                <img alt="Artifact Crate" :src="'images/icon_afx_chest_2.png'" class="w-6 mr-0_5rem">
                <span
                    class="text-xs font-bold"
                    @mouseenter="(e) => showTooltip('dubcap', e)"
                    @mouseleave="hideTooltip"
                >
                    {{viewMissionData.capacityModifier}}x Capacity
                </span>
            </span>
        </span>
        <div v-if="missionInfo.target != '' && missionInfo.target.toUpperCase() != 'UNKNOWN'">
            <div class="items-center justify-center flex">
                <span>Sensor Target: </span>
                <div class="ml-1 text-center text-xs rounded-full w-max px-1.5 py-0.5 text-gray-400 bg-darkerer font-semibold">
                    {{ properCase(missionInfo.target.replaceAll("_", " ")) }}
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
            ledger-type="mission" :data="viewMissionData"
            :show-expected-drops="showExpectedDrops"
        ></drop-display-container>

        <!-- Shamelessly stolen straight from MK2's source code, with mobile note removed -->
        <div v-if="!isMulti" class="mt-2 text-xs text-gray-300 text-center">
            Hover mouse over an item to show details.<br />
            Click to open the relevant <a target="_blank" v-external-link href="https://wasmegg-carpet.netlify.app/artifact-explorer/" class="ledger-underline">
            artifact explorer
            </a> page.
        </div>
    </div>
    <Teleport to="body">
        <Transition name="tooltip-fade">
            <div
                v-if="activeTooltip === 'bugged'"
                class="tooltip-floating"
                :style="tooltipStyle"
            >
                This ship was launched during <br>the
                <span class="text-buggedcap">0.6x Capacity "Event"</span>
                and <i>may have</i> returned<br />
                with fewer artifacts than normal.
            </div>
        </Transition>
        <Transition name="tooltip-fade">
            <div
                v-if="activeTooltip === 'dubcap'"
                class="tooltip-floating"
                :style="tooltipStyle"
            >
                This ship was launched during a<br />
                <span class="text-dubcap">{{ viewMissionData.capacityModifier }}x Capacity Event</span>
                and returned with<br />
                more artifacts than normal.
            </div>
        </Transition>
    </Teleport>
</template>

<script lang="ts">
    import { defineComponent, PropType } from 'vue';
    import DropDisplayContainer, { LedgerData } from './DropDisplayContainer.vue';

    interface MissionInfo {
        durationType: number;
        shipString: string;
        level: number;
        isDubCap: boolean;
        isBuggedCap: boolean;
        capacity: number;
        target: string;
    }

    interface ViewMissionData extends LedgerData {
        missionInfo: MissionInfo;
        launchDT: Date;
        returnDT: Date;
        durationStr: string;
        capacityModifier: number | string;
        prevMission: unknown;
        nextMission: unknown;
    }

    export default defineComponent({
        components: {
            DropDisplayContainer,
        },
        emits: [
            'view',
        ],
        props: {
            viewMissionData: {
                type: Object as PropType<ViewMissionData>,
                required: true,
            },
            isMulti: Boolean,
            showExpectedDrops: Boolean,
            shipCount: Number,
            isFirst: Boolean,
            isLast: Boolean,
        },
        data() {
            return {
                activeTooltip: '',
                tooltipX: 0,
                tooltipY: 0,
            };
        },
        computed: {
            missionInfo(): MissionInfo {
                return this.viewMissionData.missionInfo;
            },
            tooltipStyle(): Record<string, string> {
                return {
                    left: this.tooltipX + 'px',
                    top: this.tooltipY + 'px',
                };
            },
        },
        methods: {
            properCase(string: string) {
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
            showTooltip(which: string, e: MouseEvent) {
                const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
                this.activeTooltip = which;
                this.tooltipX = rect.left + rect.width / 2;
                this.tooltipY = rect.top;
            },
            hideTooltip() {
                this.activeTooltip = '';
            },
            formatDate(date: Date){
                const year = date.getFullYear();
                const month = String(date.getMonth() + 1).padStart(2, '0'); // Months are zero based
                const day = String(date.getDate()).padStart(2, '0');
                const hours = String(date.getHours()).padStart(2, '0');
                const minutes = String(date.getMinutes()).padStart(2, '0');
                const seconds = String(date.getSeconds()).padStart(2, '0');
                return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
            },
        },
    });

</script>
