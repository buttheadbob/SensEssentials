using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Quasar.Plugin.Abstractions;
using Quasar.Plugin.Abstractions.Extensions;
using Quasar.Plugin.Abstractions.Navigation;
using Quasar.Plugin.Abstractions.Security;
using SensEssentials.Quasar.Services;
using SensEssentials.QuasarContracts;

namespace SensEssentials.Quasar;

/// <summary>
/// Sens Essentials for Quasar: shared configs that Quasar keeps applied on the servers using them.
/// </summary>
// ReSharper disable once UnusedType.Global
public sealed class SensEssentialsQuasarPlugin : IQuasarPlugin
{
    public const string Route = "/sens-essentials";

    // Material "tune"
    private const string Icon = "<path d=\"M0 0h24v24H0z\" fill=\"none\"/><path d=\"M3 17v2h6v-2H3zM3 5v2h10V5H3zm10 16v-2h8v-2h-8v-2h-2v6h2zM7 9v2H3v2h4v2h2V9H7zm14 4v-2H11v2h10zm-6-4h2V7h4V5h-4V3h-2v6z\"/>";

    public string Id => SharedConfigOperations.CompanionId;

    public string DisplayName => "Sens Essentials";

    public void ConfigureServices(IServiceCollection services, QuasarPluginContext context)
    {
        services.AddSingleton(new SharedConfigStore(Path.Combine(context.InstallDirectory, "SensEssentials")));
        services.AddSingleton<SharedConfigSync>();
        services.AddHostedService(provider => provider.GetRequiredService<SharedConfigSync>());
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints, QuasarPluginContext context)
    {
    }

    public IEnumerable<Assembly> GetRazorAssemblies()
    {
        yield return typeof(SensEssentialsQuasarPlugin).Assembly;
    }

    public IEnumerable<QuasarNavItem> GetNavItems()
    {
        // Configs can hold Discord webhook URLs, so like Quasar's own plugin settings they need CanEditConfigs.
        yield return new QuasarNavItem("Sens Essentials", Route, Icon, QuasarNavZones.Main, 800, QuasarPolicyNames.CanEditConfigs);
    }

    public IEnumerable<QuasarExtensionContribution> GetExtensions()
    {
        yield break;
    }
}
