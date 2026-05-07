using Microsoft.Extensions.DependencyInjection;

namespace ManicDigger.Extensions;

public static class BuilderScreensExtensions
{
    public static IServiceCollection AddScreens(this IServiceCollection services)
    {
        services.AddScoped<IMainScreen, MainScreen>();
        services.AddScoped<IScreenGame, ScreenGame>();
        services.AddScoped<ISingleplayerScreen, SingleplayerScreen>();
        services.AddScoped<IScreenMultiplayer, MultiplayerScreen>();
        services.AddSingleton<IScreenFactory, ScreenFactory>();
        // ScreenManager satisfies both contracts from the same singleton instance.
        services.AddSingleton<ScreenManager>();
        services.AddSingleton<IScreenManager>(sp => sp.GetRequiredService<ScreenManager>());
        services.AddSingleton<INavigator>(sp => sp.GetRequiredService<ScreenManager>());
        
        return services;
    }
}
