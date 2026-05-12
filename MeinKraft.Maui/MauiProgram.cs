using MeinKraft.Extensions;
using MeinKraft.Maui.Services;
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
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("PressStart2P-Regular.ttf", "PressStart2PRegular");
            });
        builder.Services.AddSharedServices();
        builder.Services.AddClientServices();
        builder.Services.AddServerServices();

        builder.Services.AddClientMods();
        builder.Services.AddServerMods();

        builder.Services.AddSingleton<MauiGameWindowService>();
        builder.Services.AddSingleton<IGameWindowService>(sp =>
            sp.GetRequiredService<MauiGameWindowService>());

        builder.Services.AddWorkerInfrastructure();

        return builder.Build();
    }
}
