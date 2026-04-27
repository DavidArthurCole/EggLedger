<h1 align="center">
  <img width="384" src="assets/icon-1024.png" alt="EggLedger">
</h1>

<p align="center">
  <a href="https://github.com/DavidArthurCole/EggLedger/releases"><img src="assets/download.svg" alt="download"></a>
  <a href="https://wasmegg-carpet.netlify.app/"><img src="assets/more-tools.svg" alt="more tools"></a>
  <a href="https://github.com/DavidArthurCole/EggLedger/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/DavidArthurCole/EggLedger/ci.yml?branch=master" alt="Build Status"></a>
  <a href="https://discord.davidarthurcole.me"><img src="https://img.shields.io/badge/discord-join%20server-5865F2?logo=discord&logoColor=white" alt="Discord"></a>
</p>

**EggLedger** helps export your Egg, Inc. spaceship mission data, including loot from each mission, to .xlsx (Excel) and .csv formats for further analysis. It extends the [rockets tracker](https://wasmegg-carpet.netlify.app/rockets-tracker/), answering questions like "from which mission did I obtain this legendary artifact?" and "how many of this item dropped from my ships?" which can't be answered there due to technical or UI limitations.

[**Download now**](https://github.com/DavidArthurCole/EggLedger/releases).

## Features

- **Mission export** - fetch your full mission history and export it to .xlsx and .csv for analysis in any spreadsheet tool.
- **In-app mission browser** - filter and sort your mission history by ship, duration, mission type, artifact drops, and more without leaving the app.
- **Lifetime drop statistics** - view aggregate drop totals and rates across your entire history.
- **Reports** - build custom charts and breakdowns: bar charts, line charts, pie charts, and data grids with configurable grouping, filters, and time bucketing.
- **Multi-account support** - add multiple player accounts by EID and switch between them at any time; their data coexists in the same local database.
- **Parallel fetch workers** - configurable 1-10 parallel workers to control fetch speed vs. API load.
- **Firefox and Chromium-family support** - works with Chrome, Brave, Edge, Opera, Vivaldi, and Firefox. Switch browsers from Settings without reinstalling.
- **Screenshot Safety mode** - masks player EIDs wherever they appear on screen.
- **Optional Settings Sync** - back up and restore your settings across devices using Discord as an auth layer. See [Security and privacy](#security-and-privacy) for details.
- **In-app updates** - when a new release is available, an update banner appears in the About tab. One click downloads the new binary and relaunches the app in place - no browser or installer required.

## FAQ

**Windows Defender is blocking the download. What do I do?**

EggLedger is a pure Go binary with no C dependencies - it should not trigger Defender heuristics. If you are on an older version that does, mark your `Ledger` folder as excluded from scans: `Windows Security` -> `Virus & threat protection settings` -> `Manage settings` -> `Exclusions` -> `Add or remove exclusions`.

**Why is EggLedger asking me to install Chrome?**

EggLedger is built on top of `lorca`, an open-source library for building Go apps with a web UI. `lorca` uses a browser as its UI layer. EggLedger supports the following browsers:

- Google Chrome / Chromium
- Brave
- Opera
- Vivaldi
- Microsoft Edge
- Firefox

If EggLedger is launching in the wrong browser, or you would prefer a different one, go to **Settings** in-app and choose from the list of detected browsers.

**The in-app update isn't working on macOS. What do I do?**

macOS [Gatekeeper translocates](https://developer.apple.com/documentation/security/gatekeeper) apps that are run directly from a disk image or Downloads folder by executing them from a randomised quarantine path. EggLedger cannot update itself from that path. Move `EggLedger` to a permanent location (e.g. your Applications folder or any folder outside Downloads) before launching it, and the in-app updater will work normally.

**Can I run EggLedger on Mobile?**

Short answer, no. The app was designed for desktop use. There is no support for mobile devices.

**Why does it take so long to load my data?**

EggLedger pulls every mission you have ever sent into a local database, which can take a while if you have a large history. Each mission is a separate request to the Egg, Inc. API. You can increase the worker count in Settings to fetch faster, but be conservative - too many parallel requests may trigger API rate limits.

<img width="238" src="assets/ledger_moment.png" alt="Ledger Moment">

## Security and privacy

**When I use EggLedger, are my data shared with anyone?**

No. EggLedger communicates with the Egg, Inc. API directly - all your data stays local. No analytics are collected by the EggLedger developer. The only third-party requests are the occasional update check against GitHub and an optional download of community drop-rate data from a public endpoint; neither attaches personal data.

**What about the Settings Sync feature?**

Settings Sync is entirely optional - if you never connect Discord, nothing leaves your machine beyond the requests above. If you do use it, your settings are encrypted client-side before upload. The encryption key is derived locally and never transmitted; the sync server receives and stores only an opaque encrypted blob it cannot read. Discord is used solely to verify your identity - no settings data passes through Discord's servers.

**Are there risks to my account if I use EggLedger?**

I'm not aware of any negative effects, and [rockets tracker](https://wasmegg-carpet.netlify.app/rockets-tracker/) has been safely operating with the same techniques for a long time. EggLedger is not sanctioned by the Egg, Inc. developer - use it at your own risk.

*You can find answers to more frequently asked questions in the About tab when you install the app.*

## License

The MIT License. See COPYING.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for setup instructions, build steps, and PR conventions.
