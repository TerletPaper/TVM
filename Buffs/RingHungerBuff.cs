using Terraria;
using Terraria.ModLoader;

namespace VoreMod.Buffs
{
    public class RingHungerBuff : ModBuff {
        public override void SetDefaults() {
            canBeCleared = false;
            Main.persistentBuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
            Main.debuff[Type] = false;
            DisplayName.SetDefault("Ring of Hunger");
            Description.SetDefault("Rapidly digest everything in your stomach to heal");
        }
        public override bool Autoload(ref string name, ref string texture)
        {
            texture = nameof(VoreMod) + "/Buffs/CharmAcidBuff";
            return base.Autoload(ref name, ref texture);
        }
        public override void Update(Player player, ref int buffIndex) {
            if(player.statLife>=player.statLifeMax2)player.DelBuff(buffIndex);
        }
    }
}
