using Microsoft.Extensions.DependencyInjection;

namespace ManicDigger.Extensions;

public static class BuilderSharedServicesExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<IVoxelMap, VoxelMap>();
        services.AddSingleton<ISinglePlayerService, SinglePlayerService>();
        services.AddSingleton<IDummyNetwork, DummyNetwork>();
        services.AddSingleton<IAssetManager, AssetManager>();
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<IBlockRegistry, BlockRegistry>();
        services.AddSingleton<ICompression, CompressionGzip>();
        services.AddSingleton<IChunkDbCompressed, ChunkDbCompressed>();
        services.AddSingleton<IChunkDbRegion, ChunkDbRegion>();
        services.AddSingleton<ISaveGameService, SaveGameService>();

        services.AddGameLogging(
                   minimumLevel: Serilog.Events.LogEventLevel.Debug,
                   enableConsole: false);

        services.AddSingleton<CrashReporter>();

        return services;
    }
}