using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace VoreMod.Items {
	public class BlackHoleFragment : ModItem {
        public override string Texture => "Terraria/Item_"+ItemID.FragmentVortex;
        public override void SetDefaults() {
            item.CloneDefaults(ItemID.FragmentVortex);
            item.color = Color.Black;
		}
	}
}
