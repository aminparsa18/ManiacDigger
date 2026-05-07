using MessagePipe;
using Microsoft.Extensions.DependencyInjection;

namespace ManicDigger.Extensions;

public static class BuilderClientServicesExtensions
{
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
        services.AddSingleton<GameWindowNative>();

        services.AddSingleton<IGameExitService, GameExitService>();
        services.AddSingleton<IGameService, GameService>();
        services.AddSingleton<ICameraService, CameraService>();
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<IPreferences, Preferences>();
        services.AddSingleton<IOpenGlService, OpenGlService>();
        services.AddSingleton<IFrustumCulling, FrustumCulling>();
        services.AddSingleton<IMeshBatcher, MeshBatcher>();
        services.AddSingleton<IMeshDrawer, MeshDrawer>();
        services.AddSingleton<ITerrainChunkTesselator, TerrainChunkTesselator>();
        services.AddSingleton<IBlockChangeNotifier, BlockChangeNotifier>();

        services.AddSingleton<ILightManager>(sp =>  
            new LightManager(sp.GetRequiredService<IVoxelMap>(), sp.GetRequiredService<IBlockRegistry>(),
            new Lazy<ILightingWorkQueue>(() => sp.GetRequiredService<ILightingWorkQueue>()),
            sp.GetRequiredService<ISubscriber<BlockChangedEvent>>()));

        services.AddSingleton<IGame, Game>();

        return services;
    }
}