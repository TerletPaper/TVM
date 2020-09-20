using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using Terraria.ID;

namespace VoreMod
{
    public class VoreMod : Mod
    {
        public static VoreMod instance;

        public VoreUI voreUI;

        GameTime lastTime;

        public override void Load()
        {
            instance = this;
            if (!Main.dedServ)
            {
                voreUI = new VoreUI();
                voreUI.Activate();
                voreUI.Show();
            }
        }

        public override void Unload()
        {
            instance = null;
            voreUI = null;
            VorePlayer.BellyLayer = null;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            lastTime = gameTime;
            if (voreUI != null) voreUI.UpdateUI(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (voreUI != null) voreUI.ApplyToInterfaceLayers(layers, lastTime);
        }
        public override void HandlePacket(BinaryReader reader, int whoAmI) {
            byte type = reader.ReadByte();
            if(Main.netMode == NetmodeID.Server) {
                switch(type) {
                    case 0:
                    //Logger.Debug($"netmode {Main.netMode}; {reader.ReadBoolean()}, {reader.ReadInt32()}, {reader.ReadBoolean()}: {reader.ReadInt32()}");
                    ModPacket packet = GetPacket();
                    packet.Write((byte)0);
                    packet.Write(reader.ReadBoolean());
                    packet.Write(reader.ReadInt32());
                    packet.Write(reader.ReadBoolean());
                    packet.Write(reader.ReadInt32());
                    packet.Send(ignoreClient:whoAmI);
                    //Main.NewText($"{(?"player":"npc")} {} swallowed {(?"player":"npc")}  {}, in netmode: {Main.netMode}");
                    break;
                    default:
			        Logger.WarnFormat("VoreMod: Unknown Message type: {0}", type);
			        break;
                }
            } else {
                switch(type) {
                    case 0:
                    bool predType = reader.ReadBoolean();
                    int predID = reader.ReadInt32();
                    bool preyType = reader.ReadBoolean();
                    int preyID = reader.ReadInt32();
                    VoreEntity pred = predType? Main.player[predID].GetEntity(): Main.npc[predID].GetEntity();
                    VoreEntity prey = preyType? Main.player[preyID].GetEntity(): Main.npc[preyID].GetEntity();
                    pred.AddPrey(prey);
                    //Main.NewText($"{(predType?"player":"npc")} {predID} swallowed {(preyType?"player":"npc")}  {preyID}, in netmode: {Main.netMode}");
                    break;
                    default:
			        Logger.WarnFormat("VoreMod: Unknown Message type: {0}", type);
			        break;
                }
            }
        }
    }
}
