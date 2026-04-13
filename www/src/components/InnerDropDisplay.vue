<template>
    <div :class="getRepeatClass()">
        <div :class="getInnerRepeatClass()" v-for="item in itemArray" :key="item.id + '_' + item.level + '_' + item.rarity">
            <a v-external-link :class="getAClass(item)"
                target="_blank" :href="afExplorerName(item, getSpecPathOffset(item))">
                <img class="h-full w-full" :alt="item.gameName" :src="specPath(item, getSpecPathOffset(item))"/>
                <div v-if="item.count > 1" :class="'ledger-af-count ' + numToDigClass(item.count)">
                    {{ item.count }}
                </div>
                <span class="text-sm tooltiptext-custom speech-bubble">
                    {{ item.gameName }} (T{{item.level + Number(getSpecPathOffset(item))}})
                    <span v-if="item.specType == 'Artifact'" :class="'text-rarity-' + item.rarity">
                        {{ ['Common', 'Rare', 'Epic', 'Legendary'][item.rarity] }}
                    </span>
                    × {{ item.count }}
                    <br v-if="item.specType == 'Artifact' || item.specType == 'Stone'"/>
                    <span v-if="item.specType == 'Artifact' || item.specType == 'Stone'">
                        {{ boundEffectString(item.effectString)[0] }}
                        <span class="text-green-500">
                            {{ boundEffectString(item.effectString)[1] }}
                        </span>
                        {{ boundEffectString(item.effectString)[2] }}
                    </span>

                    <hr v-if="ledgerType == 'lifetime' && lifetimeShowPerShip" class="mt-0_5rem mb-0_5rem w-full">
                    <span v-if="ledgerType == 'lifetime' && lifetimeShowPerShip">
                        (<span class="text-green-500">{{ (item.count / (missionCount ?? 1)).toFixed(5) }}</span> per ship -
                        <span class="text-green-500">1</span>:<span class="text-green-500">{{ (1 / (item.count / (missionCount ?? 1))).toFixed(2) }}</span>)
                    </span>

                    <hr v-if="ledgerType == 'lifetime' && showExpectedDrops" class="mt-0_5rem mb-0_5rem w-full">
                    <span v-if="ledgerType == 'lifetime' && showExpectedDrops">
                        <span v-if="getDropCalcs(item.id, item.level, item.rarity) == null">
                            <span class="text-red-700">Not enough data to determine drop rate.</span>
                        </span>
                        <span v-else class="text-gray-400">
                            <span class="text-green-500">{{ getDropCalcs(item.id, item.level, item.rarity)![0] }}</span> expected drops
                        </span>
                    </span>

                    <hr v-if="ledgerType == 'mission' && showExpectedDrops" class="mt-0_5rem mb-0_5rem w-full">
                    <span v-if="ledgerType == 'mission' && showExpectedDrops">
                        <span v-if="getDropCalcs(item.id, item.level, item.rarity) == null">
                            <span class="text-red-700">Not enough data to determine drop rate.</span>
                        </span>
                        <span v-else class="text-gray-400">
                            <span class="text-green-500">{{ getDropCalcs(item.id, item.level, item.rarity)![0].toLocaleString() }}</span>
                            <span> seen out of </span>
                            <span class="text-green-500">{{ getDropCalcs(item.id, item.level, item.rarity)![1].toLocaleString() }}</span>
                            <span> drops</span> <br>
                            <span>(Average of <span class="text-green-500">{{ getExpectedPerShip(item.id, item.level, item.rarity) }}</span> expected in this ship)</span>
                        </span>
                    </span>
                </span>
            </a>
        </div>
    </div>
</template>

<script lang="ts">
    import { defineComponent, PropType } from 'vue';

    export interface InnerDropItem {
        id: number;
        specType: string;
        name: string;
        gameName: string;
        effectString: string;
        level: number;
        rarity: number;
        count: number;
    }

    export interface InnerMennoConfig {
        artifactConfiguration: {
            artifactType: { id: number };
            artifactLevel: number;
            artifactRarity: { id: number };
        };
        totalDrops: number;
    }

    export interface InnerMennoData {
        configs: InnerMennoConfig[];
        totalDropsCount: number;
    }

    export interface InnerDisplayOptions {
        noJustify?: boolean;
    }

    export default defineComponent({
        props: {
            itemArray: {
                type: Array as PropType<InnerDropItem[]>,
                default: () => [],
            },
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
            getRepeatClass(): string {
                if(this.ledgerType === 'lifetime') return this.options?.noJustify ? 'ledger-af-repeat-lifetime-alt' : 'ledger-af-repeat-lifetime';
                else return 'ledger-af-repeat';
            },
            getInnerRepeatClass(): string {
                if(this.ledgerType === 'lifetime') return 'af-view-rep-lifetime';
                else return 'mission-view-rep';
            },
            getSpecPathOffset(item: InnerDropItem): string {
                if(item.specType == 'Stone') return '2';
                else return '1';
            },
            getAClass(af: InnerDropItem): string {
                if(af.specType == 'Artifact') return 'ledger-af-link tooltip-custom bg-r-' + af.rarity;
                else return 'ledger-af-link tooltip-custom bg-r-0';
            },
            afExplorerName(drop: InnerDropItem, addend: string): string {
              return 'https://wasmegg-carpet.netlify.app/artifact-explorer/#/artifact/' +
                drop.name.replace('_FRAGMENT', '').toLowerCase().replaceAll("_", "-") + '-' + (drop.level + Number(addend));
            },
            specPath(spec: InnerDropItem, addend: string): string {
              const fixedName = spec.name.replaceAll("_FRAGMENT", "").replaceAll("ORNATE_GUSSET", "GUSSET").replaceAll("VIAL_MARTIAN_DUST", "VIAL_OF_MARTIAN_DUST");
              return "images/artifacts/" + fixedName + "/" + fixedName + "_" + (spec.level + Number(addend)) + ".png";
            },
            numToDigClass(num: number): string {
              const parsedNum = Number.parseInt(num.toString());
              switch (true) {
                case parsedNum > 999999: return "w-sevendig";
                case parsedNum > 99999: return "w-sixdig";
                case parsedNum > 9999: return "w-fivedig";
                case parsedNum > 999: return "w-fourdig";
                case parsedNum > 99: return "w-threedig";
                case parsedNum > 9: return "w-twodig";
                default: return "w-onedig";
              }
            },
            boundEffectString(str: string): [string, string, string] {
              if(str.startsWith('!!')){
                const match = /<([^>]+)>/g.exec(str);
                return (match ? [str.substring(2, match.index), match[1], ""] : ["?", "?", "?"]);
              }
              const match = /\[(.*?)\]/g.exec(str);
              return (match ? [str.substring(0, match.index), match[1], str.substring(match.index + match[0].length)] : ["?", "?", "?"]);
            },
            getDropCalcs(dropId: number, dropLevel: number, dropRarity: number): [number, number] | null {
                if(this.mennoData?.configs == null) return null;
                const mennoItem = this.mennoData.configs.find(item =>
                    item.artifactConfiguration.artifactType.id == dropId &&
                    item.artifactConfiguration.artifactLevel == dropLevel &&
                    item.artifactConfiguration.artifactRarity.id == dropRarity,
                );
                if(mennoItem == null || !this.ledgerType) return null;
                if(this.ledgerType == 'mission') return [mennoItem.totalDrops, this.mennoData.totalDropsCount];
                else if(this.ledgerType == 'lifetime') return [((mennoItem.totalDrops / this.mennoData.totalDropsCount) * (this.totalDropsCount ?? 0)), 0];
                else return null;
            },
            getExpectedPerShip(dropId: number, dropLevel: number, dropRarity: number): string | null {
                const ratios = this.getDropCalcs(dropId, dropLevel, dropRarity);
                if(ratios == null) return null;
                return ((ratios[0] / ratios[1]) * (this.totalDropsCount ?? 0)).toFixed(3);
            },
        },
    });
</script>
