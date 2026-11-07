# Sens Essentials 1.0.0
Compared with Essentials 1.5.1 (https://github.com/Bishbash777/Essentials, last commit fb62249).

## General
* Renamed from Essentials, with a new plugin GUID. Essentials.cfg and Essentials.KnownSteamIds.txt are copied to their
  SensEssentials names on first start; the originals are left in place.
* Also builds for Magnetar (Legacy and Interim) from the same code: Shared, TorchPlugin and ServerPlugin. On Magnetar,
  info commands are shown with `!info <name>`, since Magnetar has no chat hook. See the README for all differences.
* Quasar plugin with shared configs: create Sens Essentials configs in Quasar with a Torch-like editor and choose which
  servers follow each one. Saving sends the config to every server following it; offline servers get it when they
  connect. Local edits on a server that follows a shared config are undone.
* Based on Torch Essentials (https://github.com/TorchAPI/Essentials). New code is under the Unlicense; code derived from
  Torch Essentials is not relicensed and stays under its authors' terms. See License and Credits in the README.

## New
* Settings tab rebuilt: tabs, warnings on items that need attention, Undo after removing anything, hints and an
  Instructions tab. Essentials used Torch's generated property list.
* Cleanup Presets: named cleanups with targets and shared conditions, run with !admin runcleanup or as an auto command
  step. Each target can wait before the next one.
* Block Presets: named block actions, run with !admin runpreset or as an auto command step.
* !blocks on, off and remove accept cleanup conditions to limit which grids are affected. New !blocks count.
* Command Permissions: allow or deny single commands for single players, without changing their rank.
* Public balances setting: turn it off so only Admins can see other players' balances with !economy top and !economy
  check. On by default.
* Sim speed triggers have their own tab and run only when sim speed stays past the ratio for the whole Duration, with a
  Cooldown and optional Hysteresis. Essentials checked again once after TriggerCount seconds, with no cooldown.
* !entities poweroff and poweron: optional includeConnectorDocked.
* !voxels reset gps accepts "x y z", "x:y:z" or a pasted GPS string.
* !admin runauto, cancelauto and !vote find names regardless of upper or lower case.
* !cleanup conditions lists the cleanup conditions on both builds. Magnetar reserves `help`, so !cleanup help there is
  the generated command list; Torch still accepts it.

## Merged from Senxs Utilities
* Discord Logging tab: posts log entries to Discord webhooks (was Log2Discord). Every matching handler gets an entry,
  pings notify, long errors are attached as a file, and the plugin's own warnings are never posted back to Discord.
  Webhook URLs must be on discord.com or discordapp.com.
* Admin Tools tab: Suit Refill for admins, everyone (lobby mode), or one player for a while with
  !admin refill <player> [minutes]. Timed refills expire.
* !sutils refillSuit still works for now and points to !admin refill.
* Settings are imported once from Senxs_UtilitiesConfig.cfg. Remove the Senxs Utilities plugin afterwards.
* The Local Commands pipe server and Ejector Limiter were not carried over.

## Changed behaviour
* Cleanup and block conditions judge a ship together with everything attached by rotors, hinges and pistons. Name and
  grid type are checked on the largest grid, block and PCU counts are totals, "has" conditions match when any part
  has it and "no" conditions only when no part has it. Essentials grouped grids by conveyor/connector links and
  required every grid of a group to match on its own.
* Owners are worked out from the blocks: the owner is whoever owns the most blocks across the ship, owners whose
  identity was deleted count as nobody, and NPCs are identities without a Steam ID. ownedby <player> matches when the
  player owns any block of the ship. ownedby npc is no longer case sensitive and ownedby pirates works however many
  members the pirate faction has.
* playerdistancelessthan/greaterthan only count players who are alive in the world. Dead players and players on the
  respawn screen were measured from 0,0,0.
* poweredgriddistancegreaterthan ignores the ship's own subgrids and looks at other grids that have power. A grid with its
  power switch on but no working power does not count. Fix from Torch Essentials 74851de by Bishbash777.
* !identity clean, purge and clear return players to a brand-new state: grids they mostly own are deleted (their blocks
  on other players' ships go to that ship's owner), their offline character is removed, and they leave their faction
  and lose their GPS markers, reputation and credits. clean and clear keep the identity, purge removes it. Online
  players are skipped, and NPCs are skipped unless includeNpcs is true. Essentials' clean only handed over blocks on
  shared grids, and every command included NPCs.
* Auto commands: grid and player count triggers do not start again while the previous run is still going; a step's
  delay counts from when the step ran; changing a trigger or interval restarts the schedule; cancelling also
  interrupts a running delay; checks run on the game thread.
* !tp, !tpto and !tphere move a ship together with its rotor, hinge and piston parts and keep pilots seated. Ships
  docked by connectors are only moved with "true" added. A player name means their ship while they are seated.
* !entities poweroff and poweron switch every block that produces electricity (including modded ones) on the whole
  ship. Essentials only switched reactors, batteries and solar panels on the named grid.
* !entities delete ejects pilots first. !entities kill names who used it.
* !grids list replaces its previous GPS markers instead of adding new ones every time.
* !econ is now !economy, and !economy pay updates both players' balances on screen right away.
* !blocks on, off and remove take the selector as an argument (!blocks on type Reactor) instead of separate commands.
  Types work with or without "MyObjectBuilder_".
* Info commands match regardless of case and surrounding spaces. Whitelisted URLs open directly.
* Grid exports are kept in ExportedGrids inside the plugin's storage folder (Torch: the instance folder) instead of the
  server's working folder. Move old exports there.
* Every command is now either open to all players or needs Admin. Anything that needed SpaceMaster or Moderator needs
  Admin. Torch: !info list can be used by every player.
* !kick, !ban, !entities kill, !identity clear and Torch's !admin mute work on your own rank and below, never above.
* !cleanup delete needs a condition. !cleanup delete all deletes every grid without a pilot.
* !identity clean and purge need at least 1 day.
* hastype, hassubtype and their no forms check names against the blocks in the world, and take comma separated lists.
  A name that does not exist stops the command instead of matching everything. !blocks does the same. The -fast forms
  are now the same as the normal ones.
* ownedby and hasownertype both take npc, nobody, player and pirates in any case. ownedby checks whether any block
  belongs to that owner, so ownedby npc now matches a player's ship with one NPC block. hasownertype checks the
  majority owner. ownedby nobody still means no block has an owner, and shows a warning. An ownedby name that matches
  nobody stops the command.
* !entities delete refuses blocks and voxel maps, and points to !blocks remove and !voxels.
* !entities kill reaches offline players who are seated, and says when there is no character to kill.
* OnStart auto commands run once when the server starts, not when one is added or renamed on a running server.
* Grid and player count triggers check the count every Interval, as the setting says. It was every second.
* name conditions stop a pattern that runs for more than a second. Distances must be positive numbers, and unknown
  hasgridtype and hasownertype values stop the command.
* !blocks general now covers every power producer, every weapon and every production block. !blocks count includes armor
  and other simple blocks.
* New Magnetar command: !admin leavesharedconfig.
* Time fields are HH:MM:SS or D:HH:MM:SS, so 24:00:00 is 24 hours, not 24 days. Anything else is rejected and logged.

## Removed
* The client world download patch. The game now removes other players' GPS and death positions itself, so the patch
  only still removed their research data, which does not matter.
* Cut game tags. The game now fits its server browser data within Steam's limit by itself, and keeps as many mods and block
  limits as fit. The plugin no longer patches the game at all.
* Homes (!home) and the ranks and permissions system (!pr, default rank, override vanilla permissions). Command
  Permissions covers per-player command access.
* The default toolbar for new players (!admin set toolbar).

## Fixed
* Errors in game-thread callbacks, event handlers and session changes are logged instead of crashing the server.
* !grids export and import accepted names that could point outside ExportedGrids.
* !econ give, take and set accepted negative amounts. Balances can no longer pass the largest possible value, which
  wrapped them around to a large negative number: !economy give caps them and says so, !economy pay refuses.
* !admin setrank accepted numbers as ranks and could change an Owner's rank.
* The backpack limit counted all players together, so a new bag could delete another player's bag and its items. It
  is now per player and includes bags saved with the world. A limit of -1 no longer deletes bags.
* !w lost part of the message on Magnetar when the player name was quoted.
* Regex with a backslash in a preset changed meaning on Magnetar, so a cleanup could match more than intended.
* !grids static large counted grids it could not convert, and now stops them first.
* !grids export said saved when it failed, and !grids import said nothing when it failed.
* !admin give said items were given to players with no character. !economy take reported the full amount when the
  account had less. !economy reset uses the world's starting balance.
* !grids setowner accepted any number as an owner.
* !sandbox clean left the characters of removed identities in the world, and could give blocks to a deleted owner.
* Faction cleanup kicks members before removing a faction, as !faction remove does.
* !identity commands no longer rescan every block for each identity.
* Deleting an unchanged asteroid now also removes it for connected players. Magnetar says when players must reconnect
  to see a voxel reset.
* The Torch config is saved through a temporary file, so a crash cannot leave half a file, and a failed save no longer
  stops the server. The last edit is saved at shutdown.
* Removing a running auto command on Torch now stops it. Auto commands stop when the session ends.
* Auto commands that start each other can no longer multiply without limit: each runs at most 8 times at once.
* Discord logging crashed on its first post on Magnetar Interim, which stopped the plugin's updates.
* Discord logging no longer makes every logger build Trace entries while it is off.
* Each Discord webhook has its own queue, so a slow one no longer holds up the others. A deleted webhook is skipped for 10
  minutes instead of being tried for every entry, and queued entries get a moment to go out at shutdown. Anything cut to
  fit a Discord message is attached in full.
* On Magnetar, servers sharing a Discord channel post under their own server name.
* Quasar: two admins can no longer overwrite each other unseen. An option added to the Magnetar plugin keeps its value
  when an older Quasar plugin sends a config. Undo always returns a row to the list it came from. The shared config
  file can only be read by Quasar's user on Linux.
* Replies and failed lines from auto command and preset steps were lost on Torch. They are now logged on both hosts.
* An auto command with a repeat Interval of zero ran every second. It no longer runs, and shows why.
* A scheduled command set to one day stopped at midnight if it was still running, and carried on a week later.
* A config that cannot be read is kept as a .bad copy, instead of being overwritten with defaults at the next save.
* Quasar: choosing a config for a server asks for confirmation, and a message confirms what each server did.
* !admin give checks that the item exists and that the quantity is above 0.
* hassubtype-fast with an unknown subtype matched every block without a subtype. hastype-fast did not accept names
  without "MyObjectBuilder_".
* poweredgriddistancegreaterthan and haspilot had their help text set as their inverse command name.
* !economy pay no longer fails when run from the console or an auto command step. It says a player must run it.
* Timers and cooldowns no longer jump when the clocks change. Scheduled auto commands still follow local time.
* Backpacks are checked once a second instead of every game tick.
* The MOTD stops waiting when the player leaves, and waits for their character on the game thread instead of reading game
  state from a background thread. An altered version of Torch Essentials 3b4bfa6 by TheCrunchy.
* !blocks remove did not update grid physics and could try to remove the same block twice.
* !cleanup delete floatingobjects changed the entity list while going through it.
* !faction remove changed the member list while kicking members.
* Removing a faction left its bank account and balance in the world save.
* !grids stopall and !grids list could crash on grids without physics or when used from the console. !motd from the
  console crashed.
* !entities poweroff and poweron crashed on entities that are not grids.
* !entities refresh showed the time since the last use instead of the time left. Voxel cleanup used an asteroid
  generator from before a world reload.

# Essentials 1.5.1
* Fixes
    - Fixed crash if the MOTD field is empty

# Essentials 1.5
* Features
    - Added MOTD
    - Reworked cleanup commands