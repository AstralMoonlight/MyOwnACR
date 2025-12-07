# MyOwnACR Pro

**MyOwnACR** is a Dalamud plugin for automated combat rotations and input orchestration in *Final Fantasy XIV: Dawntrail (7.x)*.

The core is designed as a **generic ACR framework** capable of hosting rotation logic for multiple jobs inside a single addon. Input sending, game state tracking and the external Web Dashboard are shared infrastructure. Individual jobs are implemented as pluggable logic modules on top of this core.

At the moment, the only fully implemented job is **Monk (MNK)**, which serves as the primary target for all design, testing and tuning. Future versions are intended to add support for additional jobs by reusing the same architecture.

## Key Features

### 🧩 Core ACR Framework

- Shared input engine (key simulation with human-like delays and queueing).
- Local HTTP/WebSocket server and Web Dashboard for control and real-time monitoring.
- Job configuration system (keybinds, survival logic, operational settings) designed to be extended to other jobs.
- Separation between core infrastructure and per-job logic to centralize all class behavior in a single addon.


### 🥋 Advanced Monk Logic
* **Dawntrail Ready:** Full support for the new Beast Chakra system, Nadi management, and Masterful Blitz evolutions (Elixir Burst, Rising Phoenix, Phantom Rush).
* **Smart Burst Alignment:**
    * **2-Minute Window:** Automatically aligns *Phantom Rush* or *Rising Phoenix* inside *Brotherhood* + *Riddle of Fire*.
    * **1-Minute Window:** Conservatively uses resources under *Riddle of Fire* to ensure full readiness for the next 2-minute burst.
* **Double Lunar Opener:** Implements the optimal opener logic to align Phantom Rush with raid buffs.
* **Dynamic Range Calculation:** Uses target hitbox size + skill range (3.5y) to ensure attacks only trigger when truly in range.

### 🛡️ Survival & Safety
* **Auto-Survival:** Automatically uses *Second Wind* and *Bloodbath* based on configurable HP thresholds.
* **Input Humanizer:** * Randomized delays between key presses.
    * "Anxious Press" simulation (occasional early presses to queue skills).
    * Variable click counts per action (1-4 clicks).

### 🖥️ Web Dashboard
* **Real-time Monitor:** View current combo state, target HP, and active buffs via a local WebSocket connection.
* **External Configuration:** Adjust settings (AoE toggle, True North, etc.) from a browser window without decluttering the game UI.
* **Visual Debugging:** See the plugin's "Next Intention" before it executes.

## Commands

* `/acr` - Toggles the rotation on or off (START/STOP).
* `/acrstatus` - Prints current active buffs and IDs to the chat log (useful for debugging).
* `/acrdebug` - Prints internal logic state (Nadis, Chakra count, PB charges, Next Action) to the chat.

## Getting Started

### Prerequisites
* **XIVLauncher** and **Dalamud** installed.
* **.NET 8.0 SDK** (for building).

### Installation & Building

1.  Clone this repository.
2.  Open `MyOwnACR.sln` in your IDE (Visual Studio 2022 or Rider).
3.  Build the solution (Release mode recommended).
4.  The output file will be at: `MyOwnACR/bin/x64/Release/MyOwnACR.dll`.

### Activating in-game

1.  Launch the game.
2.  Type `/xlsettings` to open Dalamud Settings.
3.  Go to **Experimental** -> **Dev Plugin Locations**.
4.  Add the full path to your `MyOwnACR.dll`.
5.  Type `/xlplugins`, go to **Dev Tools**, and enable **MyOwnACR Pro**.

### Using the Dashboard

1.  Ensure the plugin is loaded in-game.
2.  Open the provided `dashboard.html` file in any modern web browser.
3.  The badge should turn **ONLINE** (Green).
4.  You can now control the bot and view stats from the browser.

## Disclaimer

This plugin is for educational and testing purposes. Use at your own risk. The developers are not responsible for any actions taken against your account.