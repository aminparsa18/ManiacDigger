public interface IDisplayService
{
    IReadOnlyList<DisplayResolutionCi> GetDisplayResolutions();
    DisplayResolutionCi GetDisplayResolutionDefault();
}