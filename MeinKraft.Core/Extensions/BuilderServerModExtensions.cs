using MeinKraft.Mods;
using MeinKraft.Mods.Fortress;
using Microsoft.Extensions.DependencyInjection;

namespace MeinKraft.Extensions;

public static class BuilderServerModExtensions
{
    public static IServiceCollection AddServerMods(this IServiceCollection services)
    {
        services.AddScoped<IMod, Core>();
        services.AddScoped<IMod, CoreBlocks>();
        services.AddScoped<IMod, AdvanceWorldGenerator>();
        services.AddScoped<IMod, BuildLog>();
        services.AddScoped<IMod, CoreCrafting>();
        services.AddScoped<IMod, CoreEvents>();
        services.AddScoped<IMod, Doors>();
        services.AddScoped<IMod, Food>();
        services.AddScoped<IMod, Ghost>();
        services.AddScoped<IMod, PlayerList>();
        services.AddScoped<IMod, RememberPosition>();
        services.AddScoped<IMod, Revert>();
        services.AddScoped<IMod, SandPhysics>();
        services.AddScoped<IMod, Tnt>();
        services.AddScoped<IMod, TreeGenerator>();
        services.AddScoped<IMod, VandalFinder>();
        services.AddScoped<IMod, VegetationGrowth>();

        services.AddSingleton<IServerModManager, ServerModManager>();

        return services;
    }
}