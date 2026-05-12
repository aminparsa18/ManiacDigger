using MeinKraft.Extensions;
using MeinKraft.Maui.Services;
using Plugin.Maui.Audio;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace MeinKraft.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .AddAudio()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("PressStart2P-Regular.ttf", "PressStart2PRegular");
            });
        builder.Services.AddSharedServices();
        builder.Services.AddClientServices();
        builder.Services.AddServerServices();

        builder.Services.AddClientMods();
        builder.Services.AddServerMods();

        builder.Services.AddSingleton<IAssetManager, AssetManager>();
        builder.Services.AddSingleton<IAudioService, AudioService>();

        builder.Services.AddSingleton<MauiGameWindowService>();
        builder.Services.AddSingleton<IGameWindowService>(sp =>
            sp.GetRequiredService<MauiGameWindowService>());

        builder.Services.AddWorkerInfrastructure();

        return builder.Build();
    }
}
