using System;
using System.IO;
using System.Xml.Serialization;
using PluginSdk.Config;

namespace SensEssentials.MagnetarHost
{
    /// <summary>
    /// The shared config this server follows, pushed by the Sens Essentials Quasar plugin. Saved next to the config,
    /// so a restarted server keeps following it before Quasar reconnects.
    /// </summary>
    public class SharedConfigState
    {
        public string ConfigId = "";
        public string Name = "";
        public long Revision;

        /// <summary>The <see cref="MagnetarConfig"/> values JSON as received; local edits are undone back to it.</summary>
        public string ValuesJson = "";

        private static readonly XmlSerializer Serializer = new(typeof(SharedConfigState));

        public static SharedConfigState? Load(string path)
        {
            if (!File.Exists(path))
                return null;
            using var stream = File.OpenRead(path);
            return Serializer.Deserialize(stream) as SharedConfigState;
        }

        public void Save(string path)
        {
            var temp = path + ".tmp";
            using (var stream = File.Create(temp))
                Serializer.Serialize(stream, this);
            // Replace swaps the files in one step, so a crash never leaves the state missing.
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
    }

    /// <summary>
    /// Entry points for the Sens Essentials Quasar companion plugin. The companion is built by Quasar and cannot
    /// reference this assembly, so it finds this class by name and calls it through reflection. Only strings and
    /// numbers cross over. Calls must be made on the game thread.
    /// </summary>
    public static class SharedConfigBridge
    {
        internal static Plugin? Plugin;

        /// <summary>
        /// The followed shared config as { config id, name, revision, plugin version }. The id is empty when the
        /// server is not following one.
        /// </summary>
        public static string[] GetStatus()
        {
            var plugin = Plugin;
            if (plugin == null)
                throw new InvalidOperationException("Sens Essentials is not loaded.");
            return plugin.GetSharedStatus();
        }

        /// <summary>Applies a shared config. Returns null when applied, otherwise why it was refused.</summary>
        public static string? Apply(string configId, string name, long revision, string valuesJson)
        {
            var plugin = Plugin;
            return plugin == null ? "Sens Essentials is not loaded." : plugin.ApplySharedConfig(configId, name, revision, valuesJson);
        }

        /// <summary>The current settings as a PluginSdk JSON document, to start a shared config from.</summary>
        public static string GetValuesJson()
        {
            var plugin = Plugin;
            if (plugin == null)
                throw new InvalidOperationException("Sens Essentials is not loaded.");
            return ConfigStorage.SaveJson(plugin.PluginConfig);
        }

        /// <summary>Stops following the shared config. The current settings stay and can be edited again.</summary>
        public static void Release()
        {
            Plugin?.ReleaseSharedConfig();
        }
    }
}
