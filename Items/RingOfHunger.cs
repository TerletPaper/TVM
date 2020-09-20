using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using VoreMod.Buffs;
using System.Linq;
using System.Collections.Generic;

namespace VoreMod.Items
{
    public class RingOfHunger : ModItem{

        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Ring of Hunger");
            Tooltip.SetDefault("Rapidly digest everything in your stomach to heal");
        }
        public override void SetDefaults() {
            item.CloneDefaults(ItemID.LesserHealingPotion);
            item.healLife = 1;
            item.UseSound = SoundID.Item28;
            item.autoReuse = false;
            item.buffType = ModContent.BuffType<RingHungerBuff>();
            item.buffTime = 300;
            item.rare = ItemRarityID.Orange;
            item.value = 15750;
            item.maxStack = 1;
            item.consumable = false;
        }
        public override bool ConsumeItem(Player player) {
            return false;
        }
        public override void GetHealLife(Player player, bool quickHeal, ref int healValue) {
            healValue = player.HasBuff(item.buffType) ? 0 : player.GetEntity().GetPreyLifeTotal();
        }
        public override bool CanUseItem(Player player) {
            return !player.HasBuff(item.buffType)&&player.GetEntity().GetPreyCount(false)>0;
        }
        public override bool UseItem(Player player) {
            player.AddBuff(item.buffType, item.buffTime);
            player.potionDelay/=6;
            player.buffTime[player.FindBuffIndex(BuffID.PotionSickness)]/=6;
            return true;
        }
        public override void AddRecipes()
        {
            ModRecipe recipe = new ModRecipe(mod);
            recipe.AddTile(TileID.WorkBenches);
            recipe.AddIngredient(ItemID.MeteoriteBar, 3);
            recipe.SetResult(this);
            recipe.AddRecipe();
        }
    }
}
