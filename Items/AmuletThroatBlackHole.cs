using System;
using Terraria.ID;
using Terraria.ModLoader;

namespace VoreMod.Items
{
	public class AmuletThroatBlackHole : AmuletThroatBase
	{
		public override ItemTier Tier => ItemTier.BlackHole;

        public override int Metal => 175;

		public override int Capacity
		{
			get
			{
				return 20;
			}
		}

        public override int EscapeLimit => 200;
        public override int EscapeBonus => 25;

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
