using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Magnetar.Protocol.Bridge;
using Magnetar.Protocol.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sandbox;
using SensEssentials.QuasarContracts;
using VRage.Plugins;

namespace SensEssentials.QuasarCompanion
{
    /// <summary>
    /// Answers the Sens Essentials Quasar plugin. Shared configs are handed to the Sens Essentials Magnetar plugin
    /// through its SharedConfigBridge, found by name because this project cannot reference it.
    /// </summary>
    // ReSharper disable once UnusedType.Global
    public sealed class SharedConfigCompanion : IPlugin, IQuasarCompanionRequestHandler
    {
        private const string BridgeTypeName = "SensEssentials.MagnetarHost.SharedConfigBridge";

        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private Type? _bridge;

        public string PluginId => SharedConfigOperations.CompanionId;

        public void Init(object gameInstance)
        {
        }

        public void Update()
        {
        }

        public void Dispose()
        {
        }

        public async Task<CompanionPluginResponse> HandleQuasarCompanionRequestAsync(CompanionPluginRequest request, CancellationToken cancellationToken)
        {
            var response = new CompanionPluginResponse { CorrelationId = request.CorrelationId, SchemaVersion = 1 };
            try
            {
                if (request.Operation == SharedConfigOperations.Export)
                {
                    var export = await OnGameThread(Export, cancellationToken).ConfigureAwait(false);
                    response.Success = true;
                    response.PayloadJson = JsonConvert.SerializeObject(export, Json);
                    return response;
                }

                SharedConfigStatus status;
                switch (request.Operation)
                {
                    case SharedConfigOperations.Status:
                        // Only reads one field, so it does not wait for the game thread and stall the agent during a world load.
                        status = GetStatus();
                        break;

                    case SharedConfigOperations.Apply:
                        var apply = JsonConvert.DeserializeObject<ApplySharedConfigRequest>(request.PayloadJson ?? "", Json)
                                    ?? throw new InvalidOperationException("The request has no shared config.");
                        status = await OnGameThread(() => Apply(apply), cancellationToken).ConfigureAwait(false);
                        break;

                    case SharedConfigOperations.Release:
                        status = await OnGameThread(Release, cancellationToken).ConfigureAwait(false);
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown operation '{request.Operation}'.");
                }

                response.Success = true;
                response.PayloadJson = JsonConvert.SerializeObject(status, Json);
            }
            catch (Exception e)
            {
                response.Success = false;
                response.Error = e is TargetInvocationException { InnerException: { } inner } ? inner.Message : e.Message;
            }
            return response;
        }

        private SharedConfigStatus GetStatus()
        {
            var bridge = FindBridge();
            if (bridge == null)
                return new SharedConfigStatus { Installed = false };

            var values = (string[])Invoke(bridge, "GetStatus")!;
            return new SharedConfigStatus
            {
                Installed = true,
                ConfigId = values[0],
                Name = values[1],
                Revision = long.Parse(values[2], CultureInfo.InvariantCulture),
                Version = values[3]
            };
        }

        private SharedConfigStatus Apply(ApplySharedConfigRequest request)
        {
            var bridge = FindBridge() ?? throw new InvalidOperationException("Sens Essentials is not loaded on this server.");
            var error = (string?)Invoke(bridge, "Apply", request.ConfigId, request.Name, request.Revision, request.ValuesJson);
            if (error != null)
                throw new InvalidOperationException(error);
            return GetStatus();
        }

        private ServerConfigExport Export()
        {
            var bridge = FindBridge() ?? throw new InvalidOperationException("Sens Essentials is not loaded on this server.");
            return new ServerConfigExport { ValuesJson = (string)Invoke(bridge, "GetValuesJson")! };
        }

        private SharedConfigStatus Release()
        {
            var bridge = FindBridge();
            if (bridge != null)
                Invoke(bridge, "Release");
            return GetStatus();
        }

        private Type? FindBridge()
        {
            if (_bridge != null)
                return _bridge;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(BridgeTypeName, throwOnError: false);
                if (type != null)
                    return _bridge = type;
            }
            return null;
        }

        private static object? Invoke(Type bridge, string method, params object[] args)
        {
            var info = bridge.GetMethod(method, BindingFlags.Public | BindingFlags.Static)
                       ?? throw new InvalidOperationException($"This Sens Essentials version has no {method}; update it.");
            return info.Invoke(null, args);
        }

        /// <summary>Config changes touch state the game reads, so they run on the game thread like the agent's.</summary>
        private static async Task<T> OnGameThread<T>(Func<T> work, CancellationToken cancellationToken)
        {
            var game = MySandboxGame.Static;
            if (game == null)
                return work();

            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled());
            game.Invoke(() =>
            {
                try
                {
                    completion.TrySetResult(work());
                }
                catch (Exception e)
                {
                    completion.TrySetException(e);
                }
            }, "SensEssentials.QuasarCompanion");
            return await completion.Task.ConfigureAwait(false);
        }
    }
}
