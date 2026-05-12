using Microsoft.Extensions.DependencyInjection;

namespace MeinKraft.Extensions;

public static class BuilderServerServicesExtensions
{
    public static IServiceCollection AddServerServices(this IServiceCollection services)
    {
        services.AddSingleton<Server>();
        services.AddSingleton<GameTimer>();

        services.AddSingleton<IServerMapStorage, ServerMapStorage>();
        services.AddSingleton<IServerConfig, ServerConfig>();
        services.AddSingleton<IPlayerStatusService, PlayerStatusService>();
        services.AddSingleton<IClientRegistry, ClientRegistry>();
        services.AddSingleton<IServerPacketService, ServerPacketService>();

        services.AddSingleton<ServerSystemLoadFirst>();
        services.AddSingleton<ServerSystemLoadConfig>();
        services.AddSingleton<ServerSystemHeartbeat>();
        services.AddSingleton<ServerSystemHttpServer>();
        services.AddSingleton<ServerSystemUnloadUnusedChunks>();
        services.AddSingleton<ServerSystemNotifyMap>();
        services.AddSingleton<ServerSystemNotifyPing>();
        services.AddSingleton<ServerSystemChunksSimulation>();
        services.AddSingleton<ServerSystemBanList>();
        services.AddSingleton<ServerSystemModLoader>();
        services.AddSingleton<ServerSystemLoadServerClient>();
        services.AddSingleton<ServerSystemNotifyEntities>();
        services.AddSingleton<ServerSystemLoadLast>();

        services.AddSingleton<ServerSystemBootstraper>();

        return services;
    }
}
