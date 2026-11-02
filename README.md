# Sens Essentials

Must-have utilities and administration tools for Space Engineers dedicated servers: auto commands, cleanup and block
presets, MOTD, voting, economy, teleporting, Discord logging, suit refill and more. Built for two plugin loaders from
one code base:

| Build | Loader | Project | Patching | Config |
|---|---|---|---|---|
| Torch | Torch for the Dedicated Server | `TorchPlugin` (.NET Framework 4.8) | Torch's PatchManager | WPF tab + `SensEssentials.cfg` |
| Magnetar | Magnetar (Legacy .NET 4.8, Interim .NET 10), managed by Quasar | `ServerPlugin` (`net48;net10.0`) | Harmony | Quasar renders it from the PluginSdk attributes |

The optional Quasar plugin (`QuasarPlugin/`) adds shared configs for admins with several Magnetar servers: one config,
edited in a Torch-like editor inside Quasar, that Quasar keeps applied on every server following it. See
[Shared configs in Quasar](#shared-configs-in-quasar).

## Layout
```
Shared/          engine neutral code, compiled into both plugins (game assemblies only)
  Core/            host interface (IEssentialsHost), logging, session lifecycle, command context
  Config/          the config model (auto commands, presets, info commands, permissions, Discord handlers)
  Commands/        the body of every command, one *Actions class per group
  Conditions/      cleanup conditions          Features/   auto commands, MOTD, backpacks, suit refill, ...
  Discord/         Discord webhook logging     Patches/    patch bodies, applied by each host
TorchPlugin/     Torch host: plugin class, WPF tab, [Category] command wrappers, PatchManager, NLog to Discord
ServerPlugin/    Magnetar host: IPlugin, Quasar config, [CommandRoot] command wrappers, Harmony, PluginSdk logs to Discord
  SharedConfig.cs  follows a shared config from the Quasar plugin, undoes local edits while it does
QuasarPlugin/    the Quasar plugin
  SensEssentials.Quasar/           Quasar UI plugin (net10.0, Blazor): shared config store, sync service, editor pages
  SensEssentials.QuasarCompanion/  Magnetar plugin Quasar builds and adds to managed servers (netstandard2.0)
  Contracts/                       companion request/response types, compiled into both
Versioning/      version.ps1 and the AssemblyVersion.cs it writes, compiled into every plugin
SensEssentials.xml         MagnetarHub descriptor (set the Commit when publishing)
SensEssentials.Quasar.xml  QuasarHub descriptor (set the Commit when publishing)
quasar-plugin.json         Quasar plugin package manifest, must stay in the repository root
Build/           SensEssentials.zip for Torch, written by the Torch build
```
Rules for `Shared/`: no Torch, NLog, Harmony or PluginSdk references, and talk to the loader only through
`IEssentialsHost`. Magnetar compiles the `ServerPlugin`, `Shared` and `Versioning` folders itself, for .NET Framework 4.8 and .NET 10,
so nothing Windows or WPF only belongs in them. New behaviour belongs in `Shared`; the host projects stay thin
wrappers.

`Shared/Config/ConfigEnums.cs` holds the config enums and must stay free of game references: the Quasar plugin
compiles it too. `ServerPlugin/MagnetarConfig.cs` and `QuasarPlugin/SensEssentials.Quasar/Model/SharedConfigValues.cs`
describe the same values JSON, so an option added or renamed in one must be changed in the other.

## Building
1. Run `Setup (run before opening solution).bat` once. It creates the `GameBinaries` / `TorchBinaries` junctions (Torch
   build) and `Directory.Build.props` from the template (Magnetar build: `Dedicated64`, `Magnetar`, `MagnetarBin`).
2. Run `build.bat` (`build.bat torch`, `build.bat magnetar`, `build.bat quasar`, optional `debug` / `nodeploy`), or open
   `SensEssentials.sln`.
   Do not use plain `dotnet build` on the solution, the Torch project needs the .NET Framework MSBuild.
   `dotnet build ServerPlugin -c Release` works on its own.
   - Torch: `Build\SensEssentials.zip` (dll + `manifest.xml`) after every build.
   - Magnetar: `ServerPlugin\bin\Release\net48` and `net10.0`. The build also copies the dll to
     `%AppData%\Magnetar\<Legacy|Interim>\Local` when that folder exists (`-p:DeployToMagnetar=false` disables it).
     `PluginSdk.dll` comes from the Magnetar install, or from a build of the Magnetar repository's `PluginSdk`
     (set `MagnetarBin` in `Directory.Build.props`).
   - Quasar plugin: `QuasarPlugin\*\bin`. Local builds compile against a quasar checkout next to this repository
     (`..\quasar`) and are skipped without one. Installed from QuasarHub, Quasar builds both projects itself.
3. Versions: `build.bat` runs `Versioning\version.ps1` first, which adds one to the build number (fourth part) in
   `Versioning\AssemblyVersion.cs` (compiled into every build) and copies the version into `TorchPlugin\manifest.xml`
   and `quasar-plugin.json`.
   For a new release, set the first three parts in `AssemblyVersion.cs` by hand (for example `1.1.0.0`).
   Building from Visual Studio does not run the script, so the version only goes up with `build.bat`.

## Differences between the two builds
| Feature | Torch | Magnetar |
|---|---|---|
| Command Permissions | Yes | No: Magnetar has no hook that runs before a command |
| Info commands | Typed as their own chat word, for example `!discord` | `!info <name>`, since Magnetar has no chat hook |
| `!admin mute`, `unmute`, `list mute` | Yes | No: they need Torch's chat manager |
| Dialog pop-ups | Torch's client mod | Magnetar's mission screens (MagnetarMod in the world), otherwise sent as chat |
| Voxel resets reach clients | Right away, through Torch's client mod | After the player reconnects |
| Discord Logging sees | Everything in NLog: game, Torch and plugins | Magnetar plugin logs only |
| Auto command steps | Any Torch command | Commands registered with Magnetar |
| Config | WPF tab with problem hints, Undo and Instructions | Quasar's generated form; instructions are the guide below |
| Senxs Utilities import | Yes | Not needed |

Root level commands such as `!say`, `!tp`, `!kick`, `!vote` and `!yes` are typed the same on both. World maintenance
commands are too: `!identity clean`, `!faction remove`, `!sandbox clean`.

## Using Sens Essentials
Torch: settings are in the plugin's tab, which also has an Instructions tab. Magnetar: settings are in Quasar, under
Server plugins, Sens Essentials; changes apply as soon as Quasar saves them. The tabs have the same names on both.

Most features work the same way: create a preset in the settings, then run it with a chat or console command, or
make it a step of an Auto Command so it runs on its own. Time fields use `HH:MM:SS`.

### Quick reference
| Command | What it does |
|---|---|
| `!admin runpreset <name>` | Run a Block Preset |
| `!admin runcleanup <name>` | Run a Cleanup Preset |
| `!admin runauto <name>` / `!admin cancelauto <name>` | Run or cancel an Auto Command |
| `!admin listauto` / `!admin listrunningauto` | List Auto Commands, or the ones running now |
| `!identity clean <days> [includeNpcs]` | Reset players inactive for that many days to a brand-new state (the identity is kept) |
| `!identity purge <days> [includeNpcs]` | Remove players inactive for that many days and everything they own |
| `!identity clear <player>` | Reset one player to a brand-new state |
| `!admin refill <player> [minutes]` | Keep one player's suit topped up for a while (default 30, 0 stops it) |

"Brand-new" means: grids they mostly own are deleted (their blocks on other players' ships go to that ship's owner),
their offline character is removed, and they leave their faction and lose their GPS markers, reputation and credits.
NPCs are skipped unless `includeNpcs` is true. Online players are never touched by the day-based commands.

### General
- **Stop ships on start**: stops every moving entity when the server starts, against physics trouble while loading.
- **Show position / GPS marker**: `!grids list` shows each grid's position and can add a GPS marker for it.
- **Backpack limit**: backpacks (dropped inventories) each player may have. -1 for no limit. Empty backpacks are
  deleted after 30 seconds.
- **Cut game tags**: removes mods and block limits from the server browser info. Turn it on when players get
  "error downloading session settings". Needs a restart.

### MOTD
- **MOTD**: shown to every player when they spawn. `%player%` is replaced with their name. `!motd` shows it again.
- **New user MOTD**: shown instead of the MOTD to players joining for the first time.
- **MOTD URL**: opened in the player's Steam overlay instead of showing the text, optionally for new players only.

### Auto Commands
Run any command automatically. Give the command a name, choose a trigger, and add steps. Each step is a command
including the `!` (for example `!save`), followed by how long to wait before the next step.

| Trigger | Runs |
|---|---|
| Disabled | Never on its own; `!admin runauto <name>` still works |
| Timed | Runs the steps, then waits Interval before running them again |
| Scheduled | Once a day at Interval (time of day, server time), on the chosen day or every day |
| OnStart | Once, shortly after the server has started |
| GridCount / PlayerCount | Every Interval, compares the number of grids (or online players) with the count, and runs when it matches. A new run does not start while the previous one is still going |
| Vote | `!vote <name>` starts a vote and `!yes` votes for it. If the share of online players voting yes reaches the ratio before the vote ends, the steps run. `!vote list` shows the commands players can vote on |
| SimSpeed | When sim speed crosses the ratio (see below) |

On Magnetar, steps can only run commands registered with Magnetar.

Example, restart warning: trigger Timed, interval `04:00:00`, steps `!say Server restarting in 5 minutes` (wait
`00:04:00`), `!say Server restarting in 1 minute` (wait `00:01:00`), then the restart command.

#### Sim speed
When sim speed crosses the ratio in the chosen direction, a timer starts. Only when sim speed stays on that side for the
whole duration do the steps run. After that, they cannot run again until the cooldown has passed. Short lag spikes
therefore never trigger anything.

- **Compare**: GreaterThan runs when sim speed rises above the ratio, LessThan when it drops below.
- **Ratio**: 0 (0%) to 1 (100%). 0.4 with LessThan means "below 40%".
- **Duration**: how long sim speed must stay past the ratio, for example `00:00:10`.
- **Cooldown**: the least time between two runs, for example `00:10:00`.
- **Hysteresis** (optional): after a run, sim speed must first recover past the recovery ratio (default 0.7) before the
  command can run again. A recovery during the cooldown counts. Use it only when sim speed keeps hovering around the
  ratio and Duration and Cooldown are not enough.

Example, lag cleanup: LessThan 0.4, duration `00:00:10`, cooldown `00:10:00`, hysteresis on at 0.7, one step
`!admin runcleanup "Dead Grids"`. A short dip does not trigger it, it runs at most every 10 minutes, and after a run
sim speed must reach 70% before it can run again.

### Info Commands
Custom commands that answer players. On Torch players type the command itself (`!rules`), on Magnetar `!info rules`;
`!info list` lists them. Each one can answer with a chat line, a dialog and a link opened in the Steam overlay; fill in
any combination, for example a chat line saying "see the dialog" together with a longer dialog.

### Block Presets
Turn blocks on or off, or remove them, across the server.
1. Add a preset, give it a name and choose the action: TurnOn, TurnOff or Remove.
2. Add items. Each one targets blocks by type or subtype. Types work with or without the `MyObjectBuilder_` prefix
   (`Reactor` and `MyObjectBuilder_Reactor` both work). Each item can wait before the next one.
3. Optionally add conditions to limit which grids are affected (for example no power, or owned by nobody). They are the
   same conditions as `!cleanup`, listed by `!cleanup help`.

Run it with `!admin runpreset "Disable Drills"`. A preset with an item without a value, or a condition missing its
parameter, is refused and the reason is shown.

### Cleanup Presets
Delete grids by name, owner, power, distance from players and many other conditions. `!cleanup help` lists them all.
- **Targets** say what you are looking for, for example grids named `Container MK` or grids owned by nobody. Each target
  runs as its own `!cleanup delete`.
- **Conditions** are added to every target, for example no power, or far from players.
- **Apply to all** runs once with only the conditions, for broad rules such as "owned by nobody and far from players".
- **DeleteFloatingObjects** as the action removes loose floating items; it needs no targets or conditions.

Ships with rotors, hinges and pistons are checked and deleted together with everything attached: the name and grid type
are checked on the largest grid, block and PCU counts are totals, the owner is whoever owns the most blocks (owned by
nobody means no block is owned), "has" conditions match when any part has it and "no" conditions only when no part has
it.

Safety: if a condition is unknown, is missing its parameter or has a parameter that cannot be read, nothing is deleted
and the reason is shown. Player distance only counts players alive in the world.

Name conditions take a pattern (regex): `Container MK` matches any name containing it, `^Static Grid [0-9]+$`,
`^Large Grid [0-9]+$` and `^Small Grid [0-9]+$` match the game's default names.

Run it with `!admin runcleanup "Dead Grids"`, or as an Auto Command step to clean up on a schedule.

### Discord Logging
Posts log entries to Discord channels, so you hear about errors without watching the console. Each handler sends to
one channel and picks entries by level and logger name; an entry goes to every handler that wants it. On Magnetar,
only Magnetar plugin logs are seen.
1. In Discord, open the channel's settings, Integrations, Webhooks, New Webhook, Copy Webhook URL.
2. Add a handler, paste the URL and tick Enabled (new handlers in Quasar start disabled).
3. Choose the levels to send. Warn to Fatal is a good start; Info and below are very busy. In Quasar, new handlers
   start with both levels at Trace, so set them.

- **Logger filters**: "only these loggers" and "never these loggers", comma separated; end a name with `*` to match by
  prefix.
- **Pings**: the role and user IDs listed are pinged with every entry. Use a separate handler for pings, for example
  Error and Fatal with pings and Warn without.
- **Rate limit**: each webhook posts at most this many entries a second. The rest are dropped and the channel is told
  how many.
- **Long errors**: stack traces longer than the limit, and errors with inner exceptions, are attached as `log.txt`.

### Admin Tools
Suit Refill tops up suits every 10 seconds: oxygen, hydrogen, energy, health, food and radiation, each of which can be
turned off. It refills admins and owners, every online player (lobby mode, for hub worlds), or one player for a while
with `!admin refill <player> [minutes]`. The player can be a name while online, or a Steam ID. Timed refills end when
the server restarts.

### Command Permissions (Torch only)
Give single players commands their in-game rank does not allow, or block commands they could otherwise use. The Torch
Instructions tab explains the rules.

## Shared configs in Quasar
For admins running several Magnetar servers. Install the Sens Essentials Quasar plugin from QuasarHub (Settings, UI
plugins) and restart Quasar. A "Sens Essentials" page appears in the menu for users who may edit configs.

- **Shared configs**: create a config, or start one from a server you already set up ("New from server"). The editor
  has the Torch tabs: General, MOTD, Auto Commands, Sim Speed, Info Commands, Block Presets, Cleanup Presets, Discord
  Logging, Admin Tools and Instructions, with ⚠ warnings and Undo. Nothing reaches the servers until you click Save.
- **Servers**: on the Servers tab (or "All servers"), choose the config each server follows. Saving sends the config to
  every server following it within seconds. Servers that are offline get it as soon as they connect; until then they
  keep the last config they received, also across restarts.
- **Local edits**: while a server follows a shared config, changes made to Sens Essentials on that server's Plugins page
  are undone and logged; the server's General tab names the config. Choose "Configured on its own" to edit the server
  by itself again. It keeps its settings. Deleting a config does the same for its servers.
- **Requirements**: the servers run the Sens Essentials Magnetar plugin. Quasar adds the plugin's small companion
  (`sensessentials.quasar`) to managed servers when they are prepared, so restart servers that were running when the
  Quasar plugin was enabled. Cluster nodes are not managed; a cluster's plugins are configured through the cluster.
- Configs are stored in `{Quasar install}/SensEssentials/shared-configs.json`. They can hold Discord webhook URLs, so
  the page needs the same permission as Quasar's plugin settings (CanEditConfigs).

## Moving from Essentials or Senxs Utilities (Torch)
- The first start copies `Essentials.cfg` and `Essentials.KnownSteamIds.txt` to their `SensEssentials` names. The
  originals are left in place.
- Settings from `Senxs_UtilitiesConfig.cfg` (Discord handlers, suit refill) are imported once. Remove the Senxs
  Utilities plugin afterwards, otherwise every log entry is posted to Discord twice.
- Do not run the original Essentials plugin next to this one: they register the same commands.
- Grid exports (`!grids export`) are now kept in `ExportedGrids` inside the Torch instance folder. Move exports made
  with the original Essentials there to import them.

## License
Sens Essentials is free and unencumbered software released into the public domain under the
[Unlicense](LICENSE).
