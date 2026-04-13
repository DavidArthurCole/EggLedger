<template>
  <div class="flex-1 w-full mx-auto px-4 overflow-y-auto">
    <div class="text-sm text-gray-400 space-y-2">
      <div class="flex items-center gap-3 p-3 rounded-lg bg-yellow-950/40 border border-yellow-700/40">
        <p class="flex-1 text-yellow-200/70 text-xs">EggLedger is free - if it's saved you time, consider buying me a coffee!</p>
        <a
          v-external-link
          href="https://buymeacoffee.com/davidarthurcole"
          target="_blank"
          class="flex-shrink-0 inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-yellow-400 hover:bg-yellow-300 text-gray-900 text-xs font-semibold transition-colors"
        >&#x2615; Buy me a coffee</a>
      </div>
      <p class="space-x-3">
        <a
          v-external-link
          href="https://github.com/DavidArthurCole/EggLedger/releases"
          target="_blank"
          class="file-link"
        >Check for updates</a>
        <a
          v-external-link
          href="https://wasmegg-carpet.netlify.app/"
          target="_blank"
          class="file-link"
        >More tools</a>
      </p>

      <hr />
      <div class="space-y-1">
        <h2 class="font-bold">Is mk2 back?</h2>
        <p>
          No. This tool is now being developed independently of mk2 - the community has taken over his orphaned tools since he went AWOL in 2022.
        </p>
        <p>
          <i>For EggLedger</i>, <i>specifically</i>, I (<a
            v-external-link
            href="https://github.com/DavidArthurCole"
            target="_blank"
            class="url-link"
          >DavidArthurCole</a>) am the active developer and maintainer.
        </p>
        <p>
          To report issues, ask for changes, and more generally, to reach me with any information, reach out on discord <span class="text-duration-0">@davidarthurcole</span>, or <a
            v-external-link
            href="https://github.com/DavidArthurCole/EggLedger/issues/new/choose"
            target="_blank"
            class="url-link"
          >open an Issue on GitHub</a>.
        </p>
      </div>
      <hr />

      <div class="space-y-1">
        <h2 class="font-bold">What does EggLedger do?</h2>
        <p>
          EggLedger helps export your spaceship mission data, including loot from each
          mission, to .xlsx (Excel) and .csv formats for further analysis.
        </p>
        <p>
          It is an extension to the
          <a
            v-external-link
            href="https://wasmegg-carpet.netlify.app/rockets-tracker/"
            target="_blank"
            class="url-link"
          >rockets tracker</a>, answering questions like "from which mission did I obtain this legendary
          artifact?" and "how many of this item dropped from my ships?" which can't be
          answered there due to technical or UI limitations.
        </p>
        <p>
          In addition, it mimics the functionality of rockets tracker to provide you access
          to view your mission data in a more user-friendly way, and lets you filter missions
          by various criteria.
        </p>
      </div>
      <hr />

      <div class="space-y-1">
        <h2 class="font-bold">How do I use EggLedger?</h2>
        <ul class="about-list-1">
          <li>Go to the Ledger tab</li>
          <li>Enter your Egg, Inc. account ID</li>
          <li>
            Press "Fetch"
            <ul class="about-list-2">
              <li>The first fetch session for an account could take a while, if you have lots of completed missions</li>
              <li>Subsequent sessions will only fetch unrecorded missions</li>
            </ul>
          </li>
        </ul>
        <span class="ledger-underline">Data for multiple accounts can coexist.</span>
      </div>
      <hr />

      <div class="space-y-1">
        <h2 class="font-bold">When I use EggLedger, are my data shared with anyone?</h2>
        <p>
          No. EggLedger communicates with the Egg, Inc. API directly, meaning all your data is
          kept 100% private. No data or analytics is collected by the EggLedger developer.
        </p>
        <p>
          The only third party request is the occasional update check against github.com; this is
          the biggest open source code hosting service, there is no personal data attached to
          the requests and no logs are available to me.
        </p>
        <p>
          Unless you tell me over another channel, there's no way I can determine if you're even using this tool, let alone
          acquiring collecting any info about your account.
        </p>
      </div>
      <hr />

      <div class="space-y-1">
        <h2 class="font-bold">Are there risks to my account if I use EggLedger?</h2>
        <p>
          I'm not aware of any negative effects, and
          <a
            v-external-link
            href="https://wasmegg-carpet.netlify.app/rockets-tracker/"
            target="_blank"
            class="url-link"
          >rockets tracker</a> has been safely operating with the same techniques for a very long time.
        </p>
        <p>
          Do realize that no community-made tools are sanctioned by the Egg, Inc. developer, so you use them at
          your own risk. I'm not responsible for any negative effects.
        </p>
      </div>
      <hr />

      <div class="space-y-1">
        <h2 class="font-bold">Where do I find my Egg, Inc. account ID?</h2>
        <ul class="about-list-1">
          <li class="mt-1">
            Open the game menu <img :src="'images/icon_menu.webp'" alt="Menu Icon" class="game-btn-ico" />
          </li>
          <li class="mt-1">
            Choose Settings <img :src="'images/icon_settings.webp'" alt="Settings Icon" class="game-btn-ico" />
          </li>
          <li class="mt-1 mb-1">
            Choose &nbsp; <span class="p-0.5 text-xs rounded-md bg-privacy_blue w-4 pl-3 pr-3"> Privacy &amp; Data </span>
          </li>
          <li class="mt1">
            Your EID is at the bottom
            <ul class="about-list-2">
              <li class="mt-1">
                <span class="ledger-underline">It should look like EI1234567890123456</span>. That's the
                letters "EI" followed by 16 digits.
              </li>
              <li class="mt-1 mb-1">
                Alternatively, if you choose Help
                <img
                  :src="'images/icon_help.webp'"
                  alt="Help Icon"
                  class="game-btn-ico"
                />
                in the main menu and click on &nbsp; <span class="p-0.5 text-xs rounded-md bg-data_loss_red w-4 pl-3 pr-3"> Data Loss Issue </span> &nbsp; the game will auto-compose an email
                with your ID in the subject
              </li>
            </ul>
          </li>
        </ul>
      </div>
      <hr />

      <div class="space-y-1">
        <h2 class="font-bold">Why are my latest missions missing?</h2>
        <p>
          The server backup is probably out of date. Pay attention to the message printed
          about the freshness of your backup whenever your backup is fetched.
        </p>
        <p>
          The game, while active, backs up your game state to Egg, Inc.'s server every couple
          of minutes if network condition allows, but these are mostly unpredictable.
          <span class="ledger-underline">Force closing and reopening the game, or switching to another farm, can fairly reliably trigger a new backup.</span>
        </p>
        <p>
          However, even after an app-initiated backup, it may take an unpredictable amount of
          time (usually no longer than a minute or two) for the game server to serve the
          updated backup through its API. EggLedger won't see the missions until then.
        </p>
      </div>
      <hr />

      <div class="space-y-1">
        <h2 class="font-bold">Where does EggLedger store data?</h2>
        <p>
          All configurations, data, and exported files are stored in the same directory as the EggLedger application.
        </p>
        <p>
          <span class="ledger-underline">Make sure you put EggLedger in a directory of its own, and don't touch the "internal" directory</span> (or you risk data corruption).
        </p>
        <p>
          If you want to uninstall EggLedger, just delete the directory, and it won't leave any trace on your system.
        </p>
      </div>
      <hr />

      <div class="space-y-1">
        <h2 class="font-bold">Where do I get @mk2's autograph?</h2>
        <p>
          <a
            v-external-link
            href="https://areyousure.netlify.app/"
            target="_blank"
            class="url-link"
          >Are you sure</a>
          you want @mk2's autograph?
        </p>
      </div>
      <hr />

      <div class="space-y-1">
        <h2 class="font-bold">Known issues</h2>
        <ul class="about-list-1">
          <li>On macOS, clicking on the app in dock can open a new browser window</li>
          <li>On Windows, resolution of the task bar app icon is garbage</li>
          <li>
            Some ships may fail to pull during a fetch
            <ul class="about-list-2">
              <li>An ocassional API timeout is normal, and another fetch should work to pull these missions</li>
              <li>If frequent timeouts occur, or the same ship fails to pull repeatedly, please reach out</li>
            </ul>
          </li>
          <li>
            Due to the immense number of elements generated, the "Mission Data" tab may be slow to load
          </li>
        </ul>
      </div>
      <hr />
    </div>

  </div>
</template>

<script setup lang="ts">
</script>
