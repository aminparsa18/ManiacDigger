using Microsoft.Extensions.DependencyInjection;

namespace MeinKraft.Extensions;

public static class BuilderSharedServicesExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<IVoxelMap, VoxelMap>();
        services.AddSingleton<IDummyNetwork, DummyNetwork>();
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<IBlockRegistry, BlockRegistry>();
        services.AddSingleton<ICompression, CompressionGzip>();

        services.AddGameLogging(
                   minimumLevel: Serilog.Events.LogEventLevel.Debug,
                   enableConsole: true);

        services.AddSingleton<CrashReporter>();

        services.AddMessagePipe();

        return services;
    }
}