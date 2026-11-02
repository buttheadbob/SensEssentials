using System;
using System.Reflection;
using Sandbox.Engine.Multiplayer;
using VRage.Game;
using VRage.Game.ObjectBuilders.Components;

namespace SensEssentials.Patches
{
    /// <summary>
    ///     This is a replacement for the vanilla logic that generates a world save to send to new clients.
    ///     The main focus of this replacement is to drastically reduce the amount of data sent to clients
    ///     (which removes some exploits), and to remove as many allocations as realistically possible,
    ///     in order to speed up the client join process, avoiding lag spikes on new connections.
    /// 
    ///     This code is **NOT** free to use, under the Apache license. You know who this message is for.
    ///
    ///    ----------------------------------------------------------------------------------------------------
    ///     Don't know who that message is for, but you cannot add private code to a public repo.  So it's all
    ///     public code as far as I'm concerned.
    /// </summary>
    public class SessionDownloadPatch
    {
        /// <summary>
        /// The vanilla method that strips the world before it is sent to a joining client.
        /// </summary>
        public static MethodInfo Target =>
            typeof(MyMultiplayerServerBase).GetMethod("CleanUpData", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(MyMultiplayerServerBase), "CleanUpData");

        /// <summary>
        /// Runs after <see cref="Target"/>. Its parameter names must match the target's, since both loaders bind by name.
        /// </summary>
        public static MethodInfo Suffix =>
            typeof(SessionDownloadPatch).GetMethod(nameof(CleanupClientWorld), BindingFlags.NonPublic | BindingFlags.Static)!;

        private static void CleanupClientWorld(MyObjectBuilder_World worldData, ulong playerId, long senderIdentity)
        {
            // Runs inside the join process; a throw here would stop the player from receiving the world.
            try
            {
                TrimWorld(worldData, senderIdentity);
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to trim the world sent to {playerId}");
            }
        }

        private static void TrimWorld(MyObjectBuilder_World worldData, long senderIdentity)
        {
            var checkpoint = worldData?.Checkpoint;
            if (checkpoint == null)
                return;

            /*
             * The entire client join code can be cleaned up massively to reduce server load. However, that needs to be in another plugin or keen
             * 
             * 
             * This is being ran directly after the original cleanup. Original removes:
             * 1. Station store items
             * 2. Player relations (keeps only theirs)
             * 3. Player Faction relations (keeps only theres)
             * 
             */

            //I know ALEs stuff removes this, but lets just add it in essentials too
            foreach (var identity in checkpoint.Identities ?? [])
            {
                //Clear all put sender identity last death position
                if (identity.IdentityId != senderIdentity)
                    identity.LastDeathPosition = null;
            }

            //I dont trust keen to do it
            var gps = checkpoint.Gps?.Dictionary;
            if (gps != null)
            {
                gps.TryGetValue(senderIdentity, out MyObjectBuilder_Gps? value);
                gps.Clear();
                if (value != null)
                    gps.Add(senderIdentity, value);
            }

            foreach (var sessionComponent in checkpoint.SessionComponents ?? [])
            {

                if (sessionComponent is MyObjectBuilder_SessionComponentResearch { Researches: not null } component)
                {
                    // Remove everyone elses research shit (quick and dirty)
                    for(int i = component.Researches.Count-1; i >= 0; i--)
                    {
                        if (component.Researches[i].IdentityId == senderIdentity)
                            continue;

                        component.Researches.RemoveAt(i);
                    }
                }
            }

            var allPlayers = checkpoint.AllPlayersData?.Dictionary;
            if (allPlayers == null)
                return;

            foreach (var player in allPlayers)
            {
                if (player.Value.IdentityId == senderIdentity)
                    continue;

                //Clear toolbar junk for other players. Seriously keen what the FUCK
                player.Value.Toolbar = null;
            }
        }
    }
}
