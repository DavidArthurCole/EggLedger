<template>
    <div 
        :class="'top-click-detect popup-selector-main overlay-' + this.ledgerType + (this.isLifetime ? '-lifetime' : '')"
        v-on:click="e => clickTop(e)"
    >
        <div class="inner-click-detect popup-selector-inner">
            <button class="detect-trigger close-button" v-on:click="e => { e.preventDefault(); $emit('close')}">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                </svg>
            </button>
            <input
                :id="(ledgerType) + '-search' + (isLifetime ? '-lifetime' : '')"
                class="input-search" type="text" placeholder="Search..."
                v-on:focus="e => {e.preventDefault(); e.stopPropagation(); resetSelectedItem(); }"
                v-on:input="e => {e.preventDefault(); $emit('input', e.target.value)}"
                v-on:keydown="e => {if(e.keyCode === 38 || e.keyCode === 40) { e.preventDefault(); arrowKeyDown(e); }}"
            />
            <div class="popup-opt-container">
                <div v-for="item in itemList" v-bind:key="item" class="popup-opt" :id="item.value + '_div'"
                    v-on:click="e => {e.preventDefault(); $emit('select', item)}"
                    v-on:keydown="e => {e.preventDefault(); if(e.keyCode == '38' || e.keyCode == '40') {arrowKeyDown(e);} else if(e.keyCode == '13') {$emit('select', item); clearSearch();}}"
                    tabindex="0"
                >
                    <img 
                        class="mr-1rem max-w-7" v-if="item.imagePath != null && item.imagePath != ''"
                        :alt="item.text" :src="getImgPath(item)"
                        :style="(ledgerType === 'drop' && item.rarityGif ? 'background: url(' + item.rarityGif + ') center center no-repeat; background-size: cover;' : '')"
                    >
                    <span :class="(ledgerType === 'drop' ? (item.styleClass ?? '') : 'text-gray-400')">
                        {{ item.text }}
                    </span>
                </div>
                <div v-if="itemList == [] || itemList.length == 0">
                    <span class="text-gray-400">
                    No results found.
                    </span>
                </div>
            </div>
        </div>
    </div>
</template>

<script>
    export default {
        emits: ['close', 'input', 'select'],
        props: {
            itemList: Array,
            ledgerType: String,
            isLifetime: Boolean,
        },
        data() {
            return {
                selectedItem: null,
            };
        },
        methods: {
            arrowKeyDown(event){
                if(event.keyCode == '38'){
                    // up arrow
                    if(this.selectedItem === null) return;
                    if(this.selectedItem == this.itemList[0]) {
                        this.unselectItem(this.selectedItem);
                        this.focusSearchBar();
                    } else{
                        const index = this.itemList.indexOf(this.selectedItem);
                        this.unselectItem(this.selectedItem);
                        this.selectItem(this.itemList[index - 1]);
                    }
                }
                else if(event.keyCode == '40'){
                    if(this.selectedItem === null){
                        this.selectItem(this.itemList[0]);
                    }
                    else{
                        const index = this.itemList.indexOf(this.selectedItem);
                        if(index == this.itemList.length - 1) return;
                        this.unselectItem(this.selectedItem);
                        this.selectItem(this.itemList[index + 1]);
                    }
                }
            },
            clearSearch(){
                document.getElementById(this.ledgerType + '-search' + (this.isLifetime ? '-lifetime' : '')).value = '';
            },
            selectItem(item){
                document.getElementById(this.ledgerType + '-search' + (this.isLifetime ? '-lifetime' : ''))?.blur();
                this.selectedItem = item;
                document.getElementById(item.value + '_div').classList.add('selected-popup-opt');
                document.getElementById(item.value + '_div').focus();
            },
            unselectItem(item){
                this.selectedItem = null;
                document.getElementById(item.value + '_div').classList.remove('selected-popup-opt');
                document.getElementById(item.value + '_div').blur();
            },
            resetSelectedItem(){
                if(this.selectedItem === null) return;
                this.unselectItem(this.selectedItem);
            },
            focusSearchBar(){
                this.selectedItem = null;
                document.getElementById(this.ledgerType + '-search' + (this.isLifetime ? '-lifetime' : '')).focus();
            },
            getImgPath(item){
                if(this.ledgerType === 'drop') return `images/${item.imagePath}`;
                else if(this.ledgerType === 'target') return `images/targets/${item.imagePath}`;
            },
            clickTop(e){
                const topElement = e.target;
                const innerEl = topElement.querySelector('.inner-click-detect');
                if(innerEl === null) return;
                if(innerEl?.classList?.contains('hidden')) return;
                const divRect = innerEl.getBoundingClientRect();
                if(e.clientX < divRect.left || e.clientX > divRect.right || e.clientY < divRect.top || e.clientY > divRect.bottom)
                innerEl.querySelector('.detect-trigger').click();
            }
        }
    }
</script>