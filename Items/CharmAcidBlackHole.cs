using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using VoreMod.Buffs;

namespace VoreMod.Items
{
    public class CharmAcidBlackHole : CharmAcidBase<CharmAcidBlackHoleBuff>
    {
        public override ItemTier Tier => ItemTier.BlackHole;
        public override int Metal => ItemID.LeadBar;
		public override void AddRecipes()
		{
			ModRecipe expr_0B = new ModRecipe(base.mod);
			expr_0B.AddTile(TileID.LunarCraftingStation);
			expr_0B.AddIngredient(ModContent.ItemType<BlackHoleFragment>(), 15);
			expr_0B.SetResult(this, 1);
			expr_0B.AddRecipe();
		}
    }
}
