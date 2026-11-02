using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ProtoBuf;
using Sandbox;
using Sandbox.Engine.Networking;
using Sandbox.Game;
using Sandbox.Game.World;
using VRage;
using VRage.Game;
using VRage.Serialization;
using Game = Sandbox.Engine.Platform.Game;

namespace SensEssentials.Patches
{
    public static class GameTagsPatch
    {
        private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone", AnyInstance)!;

        /// <summary>
        /// The vanilla method that publishes the server's settings to the server browser.
        /// </summary>
        public static MethodInfo Target =>
            typeof(MyCachedServerItem).GetMethod("SendSettingsToSteam", AnyStatic | AnyInstance)
            ?? throw new MissingMethodException(nameof(MyCachedServerItem), "SendSettingsToSteam");

        /// <summary>
        /// Runs before <see cref="Target"/> and replaces it, returning false, when the trimmed settings were sent.
        /// </summary>
        public static MethodInfo Prefix =>
            typeof(GameTagsPatch).GetMethod(nameof(SendSettingsPrefix), AnyStatic)!;

        private static bool SendSettingsPrefix()
        {
            try
            {
                return SendTrimmedSettings();
            }
            catch (Exception e)
            {
                // Fall back to the vanilla method so the server still shows up in the browser.
                Log.Error(e, "Failed to send trimmed server settings");
                return true;
            }
        }

        private static bool SendTrimmedSettings()
        {
            if (!Game.IsDedicated || MyGameService.GameServer == null || MySession.Static?.Settings == null) return true;
            byte[] array;
            using (var memoryStream = new MemoryStream())
            {
                var settings = (MyObjectBuilder_SessionSettings)MemberwiseCloneMethod.Invoke(MySession.Static.Settings, null)!;
                settings.BlockTypeLimits = new SerializableDictionary<string, short>();
                
                var myServerData = new MyCachedServerItem.MyServerData
                {
                    Settings = settings,
                    ExperimentalMode = MySession.Static.IsSettingsExperimental(),
                    // to determinate "it's not a vanilla"
                    Mods = new List<WorkshopId> {new WorkshopId(1406994352, "Steam")},
                    Description = MySandboxGame.ConfigDedicated?.ServerDescription
                };
                
                Serializer.Serialize(memoryStream, myServerData);
                array = MyCompression.Compress(memoryStream.ToArray());
            }

            MyGameService.GameServer.SetKeyValue("sc", array.Length.ToString());

            for (var i = 0; i < Math.Ceiling(array.Length / 93.0); i++)
            {
                byte[] part;
                
                var partLength = array.Length - 93 * i;
                
                if (partLength >= 93)
                {
                    part = new byte[93];
                    Array.Copy(array, i * 93, part, 0, 93);
                }
                else
                {
                    part = new byte[partLength];
                    Array.Copy(array, i * 93, part, 0, partLength);
                }

                MyGameService.GameServer.SetKeyValue($"sc{i}", Convert.ToBase64String(part));
            }


            return false;
        }
    }
}