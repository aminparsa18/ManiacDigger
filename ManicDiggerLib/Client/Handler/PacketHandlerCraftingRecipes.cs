public class PacketHandlerCraftingRecipes : ClientPacketHandler
{
    internal ModGuiCrafting mod;
    public override void Handle(Game game, Packet_Server packet)
    {
        mod.d_CraftingRecipes = packet.CraftingRecipes.CraftingRecipes;
        mod.d_CraftingRecipesCount = packet.CraftingRecipes.CraftingRecipesCount;
    }
}
