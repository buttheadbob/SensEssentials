# Sens Essentials 1.0.0
* Renamed from Essentials, with a new plugin GUID. The old config and known Steam ID files are copied on first start.
* Now also builds for Magnetar (Legacy and Interim). The code is split into Shared, TorchPlugin and ServerPlugin; see
  the README for what differs between the two.
* On Magnetar, info commands are shown with `!info <name>`, since Magnetar has no chat hook. On Torch they stay their own chat word.
* Quasar plugin with shared configs: create Sens Essentials configs in Quasar with a Torch-like editor and choose which
  servers follow each one. Saving sends the config to every server following it; offline servers get it when they
  connect. Local edits on a server that follows a shared config are undone.
* Merged Senxs Utilities
    - Discord Logging tab: posts log entries to Discord webhooks (was Log2Discord). Every matching handler now gets an entry, pings actually notify, long errors are attached as a file, and the plugin's own warnings are never posted back to Discord.
    - Admin Tools tab: Suit Refill for admins, everyone (lobby mode), or one player for a while with !admin refill <player> [minutes]. Timed refills now expire.
    - !sutils refillSuit still works for now and points to !admin refill.
    - Settings are imported once from Senxs_UtilitiesConfig.cfg. Remove the Senxs Utilities plugin afterwards.
    - The Local Commands pipe server and Ejector Limiter were not carried over.
* Fixes
    - Errors in game thread callbacks, event handlers, session changes and patches are logged instead of crashing the server.
    - hassubtype-fast with an unknown subtype matched every block without a subtype.
    - hastype-fast now accepts names with or without "MyObjectBuilder_", like hastype.
    - !blocks remove updates grid physics.
    - !grids export and import only use files inside ExportedGrids, which is now in the plugin's storage folder
      (Torch: the instance folder) instead of the server's working folder. Move old exports there.
    - !admin give checks that the item exists; !economy give, take and set reject negative amounts.
    - Balances can no longer pass the largest possible value, which wrapped them around to a large negative number.
      !economy give caps them and says so; !economy pay refuses.
    - !admin setrank only accepts rank names and cannot change the rank of an Owner.
    - !identity purge needs Admin, like !identity clear (was SpaceMaster).
    - Discord webhook URLs must be on discord.com or discordapp.com, so logs are never sent anywhere else.
    - Torch: !info list can be used by every player, as on Magnetar.
* Released under the Unlicense.

# Essentials 1.5.1
* Fixes
    - Fixed crash if the MOTD field is empty

# Essentials 1.5
* Features
    - Added MOTD
    - Reworked cleanup commands