public interface IAssetManager
{
    List<Asset> Assets { get; set; }
    float AssetsLoadProgress { get; set; }

    void LoadAssets();
}