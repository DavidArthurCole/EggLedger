<template>
    <div
        :class="(isMulti ? ((isFirst ? ' pl-7rem' : ' pl-3rem') + (isLast ? ' pr-7rem' : ' pr-3rem')) : '' ) + ' text-gray-300 text-center' + ( (shipCount ?? 0) > 3 ? ' min-w-30vw' : '') "
    >
        <!-- Ship info container: two cards side by side -->
        <div class="flex flex-row gap-2 mb-3 mx-auto items-center" style="width: fit-content;">
            <!-- Capacity badge slot - always reserves space so name card never shifts.
                 Content is hidden when no special capacity event applies. -->
            <div
                :class="missionInfo.isBuggedCap ? 'bugged-cap-span' : (missionInfo.isDubCap ? 'double-cap-span' : '')"
                :style="{ visibility: (missionInfo.isBuggedCap || missionInfo.isDubCap) ? 'visible' : 'hidden' }"
                class="flex items-center py-1 px-1.5 self-center"
            >
                <img
                    :alt="missionInfo.isBuggedCap ? 'Skull Emoji' : 'Artifact Crate'"
                    :src="missionInfo.isBuggedCap ? 'images/skull.png' : 'images/icon_afx_chest_2.png'"
                    class="w-5 mr-0_5rem"
                >
                <span
                    class="text-xs font-bold whitespace-nowrap"
                    @mouseenter="(e) => showTooltip(missionInfo.isBuggedCap ? 'bugged' : 'dubcap', e)"
                    @mouseleave="hideTooltip"
                >{{ missionInfo.isBuggedCap ? '0.6x Capacity' : (viewMissionData.capacityModifier + 'x Capacity') }}</span>
            </div>
            <!-- Left card: identity -->
            <div class="rounded-md px-4 py-2 flex flex-col items-center justify-center" style="background: rgba(120, 128, 138, 0.12);">
                <img
                    v-if="missionInfo.shipEnumString"
                    :src="'images/ships/' + missionInfo.shipEnumString + '.png'"
                    :alt="missionInfo.shipString"
                    class="w-12 h-12 object-contain mb-2"
                />
                <span :class="'text-duration-' + missionInfo.durationType">
                    {{ missionInfo.shipString }}
                </span>
                <span
                    v-if="missionInfo.level && missionInfo.level > 0"
                    class="text-star text-goldenstar"
                >
                    {{ "★".repeat(missionInfo.level) }}
                </span>
            </div>
            <!-- Right card: mission details -->
            <div class="rounded-md px-4 py-2 flex flex-col justify-center text-left text-sm" style="background: rgba(120, 128, 138, 0.12);">
                <div>Launched: {{ formatDate(viewMissionData.launchDT) }}</div>
                <div>Returned: {{ formatDate(viewMissionData.returnDT) }}</div>
                <div>Duration: {{ viewMissionData.durationStr }}</div>
                <div>Capacity: {{ missionInfo.capacity }}</div>
                <div v-if="missionInfo.target != '' && missionInfo.target.toUpperCase() != 'UNKNOWN'" class="flex items-center gap-1 mt-1">
                    <span>Sensor Target:</span>
                    <div class="text-xs rounded-full w-max px-1.5 py-0.5 text-gray-400 bg-darkerer font-semibold">
                        {{ properCase(missionInfo.target.replaceAll("_", " ")) }}
                    </div>
                </div>
            </div>
        </div>

        <drop-display-container
            ledger-type="mission" :data="viewMissionData"
            :show-expected-drops="showExpectedDrops"
            :use-containers="true"
            :ship-count="shipCount"
        ></drop-display-container>

        <!-- Shamelessly stolen straight from MK2's source code, with mobile note removed -->
        <div v-if="!isMulti && !hideFooterTip" class="mt-2 text-xs text-gray-300 text-center">
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
        shipEnumString: string;
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
            hideFooterTip: Boolean,
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
                const month = String(date.getMonth() + 1).padStart(2, '0');
                const day = String(date.getDate()).padStart(2, '0');
                const hours = String(date.getHours()).padStart(2, '0');
                const minutes = String(date.getMinutes()).padStart(2, '0');
                const seconds = String(date.getSeconds()).padStart(2, '0');
                return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
            },
        },
    });

</script>
