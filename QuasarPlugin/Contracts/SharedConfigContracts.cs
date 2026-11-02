namespace SensEssentials.QuasarContracts
{
    // Companion channel contract between the Sens Essentials Quasar plugin and its Magnetar companion.
    // Both projects compile this file, so the operation names and payloads always match.
    // It must build for netstandard2.0 (companion) and net10.0 (Quasar plugin).

    public static class SharedConfigOperations
    {
        /// <summary>Id of the Quasar plugin and of its companion on the servers.</summary>
        public const string CompanionId = "sensessentials.quasar";

        /// <summary><see cref="EmptyPayload"/> in, <see cref="SharedConfigStatus"/> out.</summary>
        public const string Status = "sharedconfig.status";

        /// <summary><see cref="ApplySharedConfigRequest"/> in, <see cref="SharedConfigStatus"/> out.</summary>
        public const string Apply = "sharedconfig.apply";

        /// <summary><see cref="EmptyPayload"/> in, <see cref="SharedConfigStatus"/> out.</summary>
        public const string Release = "sharedconfig.release";

        /// <summary><see cref="EmptyPayload"/> in, <see cref="ServerConfigExport"/> out: the server's current settings.</summary>
        public const string Export = "sharedconfig.export";
    }

    public class ServerConfigExport
    {
        /// <summary>The Sens Essentials Magnetar config as a PluginSdk JSON document.</summary>
        public string ValuesJson { get; set; } = "";
    }

    public class EmptyPayload
    {
    }

    public class SharedConfigStatus
    {
        /// <summary>False when the Sens Essentials Magnetar plugin is not loaded on the server.</summary>
        public bool Installed { get; set; }

        /// <summary>Empty when the server is not following a shared config.</summary>
        public string ConfigId { get; set; } = "";

        public string Name { get; set; } = "";

        public long Revision { get; set; }

        /// <summary>The Sens Essentials version on the server.</summary>
        public string Version { get; set; } = "";
    }

    public class ApplySharedConfigRequest
    {
        public string ConfigId { get; set; } = "";

        public string Name { get; set; } = "";

        public long Revision { get; set; }

        /// <summary>The Sens Essentials Magnetar config as a PluginSdk values JSON document.</summary>
        public string ValuesJson { get; set; } = "";
    }
}
