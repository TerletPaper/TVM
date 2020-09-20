using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using VoreMod.Items;
using VoreMod.Buffs;

namespace VoreMod
{
    public abstract class VoreEntity
    {
        CharmEffects charms;

        public Vector2 swallowedLocation;

        bool digesting;
        int digestCounter;

        int digestValue;

        int regenCounter;

        int noiseCounter;
        int noiseRate;

        int struggleCounter;

        int graceCounter;

        float bellyRatio;

        VoreEntity pred;
        List<VoreEntity> preys = new List<VoreEntity>();

        public VoreEntity GetPredator() => pred;

        public IReadOnlyList<VoreEntity> GetAllPrey(bool includeChildren = false) => includeChildren ? new List<VoreEntity>(preys) : preys.Where(p => !p.IsChild()).ToList();

        public int GetPreyCount(bool includeChildren)
        {
            int count = 0;
            foreach (VoreEntity prey in preys)
            {
                if (includeChildren || !prey.IsChild()) count++;
            }
            return count;
        }
        public int GetPreyLifeTotal(bool includeChildren = false)
        {
            int life = 0;
            foreach (VoreEntity prey in preys)
            {
                if (includeChildren || !prey.IsChild()) life+=prey.GetLife();
            }
            return life;
        }

        public float GetDigestionRatio() => IsSwallowed() ? (float)digestValue / (float)pred.GetDigestionLimit(this) : 0f;

        public float GetLifeRatio() => (float)GetLife() / (float)GetLifeMax();

        public bool HasSwallowed(VoreEntity prey) => prey.IsSwallowedBy(this);

        public bool HasSwallowedAny() => preys.Count > 0;

        public bool IsSwallowed() => pred != null;

        public bool IsSwallowedBy(VoreEntity otherPred) => otherPred == pred;

        public bool CanSwallow(VoreEntity prey) => !IsSwallowed() && (GetPreyCount(false) < GetCapacity() || VoreConfig.Instance.DebugNoPreyCapacityLimit) && prey.CanBeSwallowed() && VoreConfig.Instance.CanSwallow.Match(this) && EligibleForVore();

        public bool CanBeSwallowed() => !IsSwallowed() && VoreConfig.Instance.CanBeSwallowed.Match(this) && EligibleForVore();

        public bool CanRegurgitate(VoreEntity prey) => !IsSwallowed() && prey.IsSwallowedBy(this) && prey.CanBeRegurgitated() && prey.GetLifeRatio()>0.1f && EligibleForVore();

        public bool CanRegurgitateAny() => HasSwallowedAny() && preys.Any(p => CanRegurgitate(p));

        public bool CanBeRegurgitated() => IsSwallowed() && EligibleForVore();

        public bool IsBeingDigested() => IsSwallowed() && digesting;

        public bool IsDigestingAny() => HasSwallowedAny() && preys.Any(p => p.IsBeingDigested());

        public bool CanDigest(VoreEntity prey) => prey.IsSwallowedBy(this) && prey.CanBeDigested() && VoreConfig.Instance.CanDigest.Match(this) && EligibleForVore();

        public bool CanDigestAny() => HasSwallowedAny() && preys.Any(p => CanDigest(p));

        public bool CanBeDigested() => IsSwallowed() && !IsBeingDigested() && VoreConfig.Instance.CanBeDigested.Match(this) && EligibleForVore();

        public bool CanDispose(VoreEntity prey) => prey.IsSwallowedBy(this) && prey.CanBeDisposed() && EligibleForVore();

        public bool CanDisposeAny() => HasSwallowedAny() && preys.Any(p => CanDispose(p));

        public bool CanBeDisposed() => IsSwallowed() && IsBeingDigested() && GetLife() <= 1 && GetDigestionRatio() >= 1f && EligibleForVore();

        public bool CanDamage(VoreEntity target) => !IsSwallowed() && graceCounter == 0 && target.CanBeDamaged();

        public bool CanBeDamaged() => !IsSwallowed() && graceCounter == 0;

        public bool CanStruggle() => IsSwallowed() && GetLife() > 1 && VoreConfig.Instance.CanStruggle.Match(this) && EligibleForVore();

        public bool CanRandomVore(VoreEntity target) => CanSwallow(target) && VoreConfig.Instance.CanRandomVore.Match(this) && EligibleForVore();

        public virtual bool ShouldDigest(VoreEntity prey) => CanDigest(prey) && charms.acid > 0 && (IsHostileTo(prey) || prey.charms.hunger != ItemTier.None);

        public virtual bool ShouldDispose(VoreEntity prey) => CanDispose(prey);

        public virtual bool ShouldStruggle() => CanStruggle() && IsHostileTo(GetPredator());

        public void Swallow(VoreEntity prey) {
            AddPrey(prey);
            //Main.NewText($"{this.GetID()} swallowed {prey.GetID()} in netmode: {Main.netMode}");
            if(Main.netMode == NetmodeID.MultiplayerClient && this.IsLocalPlayer() || (prey.IsLocalPlayer() && this is VoreEntityNPC)) {
                ModPacket packet = VoreMod.instance.GetPacket();
                packet.Write((byte)0);
                packet.Write(this is VoreEntityPlayer);
                packet.Write(this.GetID());
                packet.Write(prey is VoreEntityPlayer);
                packet.Write(prey.GetID());
                packet.Send();
            }
        }

        public void Regurgitate(VoreEntity prey)
        {
            if (IsSwallowed())
            {
                RemovePrey(prey);
                GetPredator().Swallow(prey);
            }
            else
            {
                RegurgitatePrey(prey);
            }
        }

        public void RegurgitateLast()
        {
            VoreEntity prey = preys.LastOrDefault(p => CanRegurgitate(p));
            Regurgitate(prey);
        }

        public void Dispose(VoreEntity prey)
        {
            DisposePrey(prey);
        }

        public void Digest(VoreEntity prey)
        {
            prey.digesting = true;
        }

        public bool AttemptRandomVore(VoreEntity target)
        {
            if (CanRandomVore(target))
            {
                if (Main.rand.NextFloat() <= target.GetRandomVoreChance(this))
                {
                    Swallow(target);
                }
            }
            return false;
        }

        public void Death()
        {
            foreach (VoreEntity prey in GetAllPrey(true))
            {
                if (IsSwallowed())
                {
                    RemovePrey(prey);
                    pred.Swallow(prey);
                }
                else
                {
                    Regurgitate(prey);
                }
            }
            if (IsSwallowed()) pred.Regurgitate(this);
        }

        public void ApplyCharm(CharmEffect effect, ItemTier tier)
        {
            charms[effect] = (ItemTier)Math.Max((int)charms[effect], (int)tier);
        }

        public void DigestTick()
        {
            digestCounter = 0;

            int digestDamage = (int)pred.charms.acid + (int)charms.hunger;
            int digestHeal = (int)pred.charms.life + (int)charms.life;
            int digestManaRegen = (int)pred.charms.mana + (int)charms.mana;

            digestDamage += (int)Math.Ceiling(GetLifeMax() * 0.05f * Math.Sqrt(GetDigestionRatio()));

            int digestAmount = digestDamage + (int)(Math.Sqrt(GetLifeMax()) * 0.1f);

            if(pred is VoreEntityPlayer vep && vep.player.HasBuff(ModContent.BuffType<RingHungerBuff>())) {
                digestAmount*=10;
                digestDamage*=5;
                digestHeal+=digestDamage/2;
            }

            digestValue = (int)MathHelper.Clamp(digestValue + digestAmount, 0, pred.GetDigestionLimit(this));
            if (VoreConfig.Instance.DebugInfo) Main.NewText(GetName() + " is digesting (" + digestValue + "/" + pred.GetDigestionLimit(this) + ")");

            if (GetLife() > 1)
            {
                if (digestDamage > 0) SetLife(Math.Max(1, GetLife() - digestDamage));
                if (digestHeal > 0) pred.SetLife(Math.Min(pred.GetLifeMax(), pred.GetLife() + digestHeal));
                if (digestManaRegen > 0) pred.SetMana(Math.Min(pred.GetManaMax(), pred.GetMana() + digestManaRegen));
            }

        }

        public void RegenTick()
        {
            regenCounter = 0;

            int lifeRegen = (int)pred.charms.life + (int)charms.life;
            int manaRegen = (int)pred.charms.mana + (int)charms.mana;

            if (lifeRegen > 0) SetLife(Math.Min(GetLifeMax(), GetLife() + lifeRegen));
            if (manaRegen > 0) SetMana(Math.Min(GetManaMax(), GetMana() + manaRegen));
            if (lifeRegen > 0) pred.SetLife(Math.Min(pred.GetLifeMax(), pred.GetLife() + lifeRegen));
            if (manaRegen > 0) pred.SetMana(Math.Min(pred.GetManaMax(), pred.GetMana() + manaRegen));
        }

        public void DigestionNoiseTick()
        {
            int noiseMinTime = IsDigestingAny() ? 60 : 120;
            int noiseMaxTime = IsDigestingAny() ? 120 : 480;

            noiseCounter = 0;
            noiseRate = Main.rand.Next(noiseMinTime, noiseMaxTime);

            PlaySound(GetDigestionNoise());
        }

        public void StruggleTick()
        {
            struggleCounter = 0;

            bool escape = !GetPredator().IsHostileTo(this) && charms.hunger == 0;

            if (!escape)
            {
                int struggleBonus = GetStruggleBonus(GetPredator());
                int escapeLimit = GetPredator().GetEscapeLimit(this);
                int struggleRoll = Main.rand.Next(0, struggleBonus);
                int escapeRoll = Main.rand.Next(0, escapeLimit);
                escapeRoll+=this.GetPredator().GetEscapeBonus(this);

                if (IsLocalPlayer() || GetPredator().IsLocalPlayer())
                {
                    if (VoreConfig.Instance.DebugInfo)
                        Main.NewText(GetName() + " is struggling! " + struggleRoll + " (" + struggleBonus + ") >= " + escapeRoll + " (" + escapeLimit + ")");
                    else
                        Main.NewText(GetName() + " is struggling!");
                }

                if (struggleRoll >= escapeRoll)
                {
                    digestValue = (int)MathHelper.Clamp(digestValue - struggleBonus, 0, pred.GetDigestionLimit(this));
                    escape = digestValue <= 0;

                    Main.PlaySound(GetPredator().GetHitSound(), GetPosition());
                }
            }

            if (escape)
            {
                if (IsLocalPlayer() || GetPredator().IsLocalPlayer()) Main.NewText(GetName() + " escaped!");

                GetPredator().Regurgitate(this);
            }
        }

        public void ResetTick()
        {
            if (!IsValid()) return;

            foreach (VoreEntity prey in GetAllPrey(true))
            {
                if (!prey.IsValid()) RemovePrey(prey);
            }
            if (pred != null && !pred.IsValid()) pred.RemovePrey(this);

            charms = GetBaseCharms();
        }

        public void UpdateTick()
        {
            if (!IsValid()) return;

            if (IsSwallowed())
            {
                SetStateSwallowed();
                SetPosition(pred.GetBellyLocation());
            }

            if (HasSwallowedAny())
            {
                if (noiseCounter >= noiseRate) DigestionNoiseTick();
                else noiseCounter++;

                foreach (VoreEntity prey in GetAllPrey())
                {
                    if (ShouldDigest(prey)) Digest(prey);
                    if (ShouldDispose(prey)) Dispose(prey);
                }
            }

            if (IsSwallowed() && !IsBeingDigested() && !IsChild())
            {
                int regenRate = 30;

                if (regenCounter >= regenRate) RegenTick();
                else regenCounter++;
            }
            if(this.IsSwallowed()) {
                if(this.GetPredator() is VoreEntityPlayer PredPlayer) {
                    PredPlayer.player.AddBuff(BuffID.Slow, (int)(120/Math.Max(this.GetDigestionRatio(), 0.2f)));
                }else if(this.GetPredator() is VoreEntityNPC PredNPC) {
                    PredNPC.npc.AddBuff(BuffID.Slow, (int)(120/Math.Max(this.GetDigestionRatio(), 0.2f)));
                }
            }
            if (IsBeingDigested() && !IsChild())
            {
                int digestRate = 60;

                if (digestCounter >= digestRate) DigestTick();
                else digestCounter++;
                if(this.GetLifeRatio()<=0.25f && this.GetPredator() is VoreEntityPlayer PredPlayer) {
                    PredPlayer.GetPlayer().AddBuff(BuffID.WellFed, (int)(this.GetLifeMax()/Math.Max(this.GetDigestionRatio(), 0.05f)));
                }
            }

            if (ShouldStruggle() && !IsChild())
            {
                int struggleRate = 90;

                if (struggleCounter >= struggleRate) StruggleTick();
                else struggleCounter++;
            }

            if (graceCounter > 0) graceCounter--;

            float targetRatio = GetAllPrey().Count > 0 ? GetAllPrey().Sum(p => p.GetSizeFactor() * (float)(Math.Sqrt(1f - p.GetDigestionRatio()) * 0.5f + p.GetLifeRatio() * 0.5f)) : 0f;
            if (targetRatio < bellyRatio) bellyRatio = MathHelper.Max(targetRatio, bellyRatio - 0.05f);
            if (targetRatio > bellyRatio) bellyRatio = MathHelper.Min(targetRatio, bellyRatio + 0.02f);
        }

        public virtual bool HasBelly() => GetBellyRatio() > 0f && !VoreConfig.Instance.DebugNoBellies;

        public abstract Texture2D GetBellyTexture();

        public virtual Rectangle GetBellyRect()
        {
            Texture2D texture = GetBellyTexture();

            int frameCount = 6;
            int frameSize = texture.Height / frameCount;
            int frame = 0;

            float ratio = GetBellyRatio();

            if (ratio >= 0.8f) frame = 5;
            else if (ratio >= 0.6f) frame = 4;
            else if (ratio >= 0.4f) frame = 3;
            else if (ratio >= 0.2f) frame = 2;
            else if (ratio > 0f) frame = 1;
            else frame = 0;

            return new Rectangle(0, frameSize * frame, texture.Width, frameSize);
        }

        public virtual float GetBellyRatio() => VoreConfig.Instance.DebugFullBellies ? float.PositiveInfinity : bellyRatio;

        public abstract Vector2 GetBellyOffset();

        public abstract Color GetBellyColor();

        public virtual Vector2 GetBellyLocation() => GetPosition() + new Vector2(0f, -5f);

        public virtual Vector2 GetRegurgitateLocation() => GetPosition() - new Vector2(0f, 20f);

        public virtual Vector2 GetDigestLocation() => GetPosition();

        public virtual int GetDigestionLimit(VoreEntity prey) => GetEscapeLimit(prey) * 2;

        private void ResetSelf()
        {
            if (pred != null) RestoreState();
            pred = null;
            digesting = false;
            digestCounter = 0;
            digestValue = 0;
            noiseCounter = 0;
            struggleCounter = 0;
        }

        internal void AddPrey(VoreEntity prey)
        {
            prey.ResetSelf();
            prey.pred = this;
            prey.swallowedLocation = prey.GetPosition();

            preys.Add(prey);
            prey.BackupState();
            prey.SetStateSwallowed();
            PlaySound(GetSwallowNoise());
            prey.OnSwallowedBy(this);
        }

        private void RemovePrey(VoreEntity prey)
        {
            prey.ResetSelf();
            preys.Remove(prey);
        }

        private void RegurgitatePrey(VoreEntity prey)
        {
            prey.graceCounter = 30;
            prey.RestoreState();
            prey.SetPosition(GetRegurgitateLocation());
            prey.Knockback(new Vector2(GetDirection() * 5f, -2.5f));
            PlaySound(GetRegurgitateNoise());
            RemovePrey(prey);
            prey.OnRegurgitatedBy(this);
        }

        private void DisposePrey(VoreEntity prey)
        {
            prey.graceCounter = 30;
            prey.RestoreState();
            prey.SetPosition(GetDigestLocation());
            PlaySound(GetDisposalNoise());
            RemovePrey(prey);
            prey.OnDisposedBy(this);
            this.OnDispose(prey);
            if(VoreConfig.Instance.EffectsDisposalGore) {
			    prey.SetLife(1);
			    prey.Damage(this, 1, 0f);
            } else {
			    prey.SetLife(0);
                if(prey is VoreEntityNPC vnpc && vnpc.npc != null) {
                    if(this is VoreEntityPlayer vep && vep.player.HasBuff(ModContent.BuffType<RingHungerBuff>())) {
                        vnpc.npc.active = false;
                    } else {
                        vnpc.npc.checkDead();
                        vnpc.npc.DeathSound = null;
                    }
                }
                (prey as VoreEntityPlayer)?.player?.KillMe(PlayerDeathReason.ByCustomReason(string.Format((prey as VoreEntityPlayer).GetRandomDeathMessage(), this, this)), 1, 0);
            }
            int soulChance = ((int)charms.soul + (int)prey.charms.soul) * 10;
            if (Main.hardMode && Main.rand.Next(100) < soulChance)
            {
                int soulBonus = (soulChance-100)/100;
                if(soulChance>100&&soulChance%100!=0&&Main.rand.Next(100) < soulChance%100)soulBonus++;
                WeightedRandom<int> drop = new WeightedRandom<int>();
                drop.Add(ItemID.SoulofFlight, 1);
                drop.Add(ItemID.SoulofLight, 1);
                drop.Add(ItemID.SoulofNight, 1);
                if (soulChance >= 50)
                {
                    drop.Add(ItemID.SoulofMight, 1 / 3);
                    drop.Add(ItemID.SoulofSight, 1 / 3);
                    drop.Add(ItemID.SoulofFright, 1 / 3);
                }
                if((this is VoreEntityNPC Vnpc) && ((Vnpc.GetNPC().type>=NPCID.StardustWormHead&&Vnpc.GetNPC().type<=NPCID.VortexSoldier)||Vnpc.GetNPC().type==NPCID.SolarSpearman)) {
				    drop = new WeightedRandom<int>();
                    Item.NewItem(prey.GetPosition(), ModContent.ItemType<BlackHoleFragment>(), 1+soulBonus);
                    return;
                }
                Item.NewItem(prey.GetPosition(), drop.Get(), 1+soulBonus);
            }
        }

        private void PlaySound(string name)
        {
            if (Main.dedServ || string.IsNullOrEmpty(name)) return;
            Mod mod = ModLoader.GetMod(nameof(VoreMod));
            var sound = mod.GetLegacySoundSlot(Terraria.ModLoader.SoundType.Custom, "Sounds/Custom/" + name);
            Main.PlaySound(sound.WithVolume(0.75f).WithPitchVariance(0.25f), GetPosition());
        }

        private bool IsLocalPlayer() => !Main.dedServ && Main.LocalPlayer.GetEntity() == this;

        public virtual void OnSwallowedBy(VoreEntity pred) { }

        public virtual void OnRegurgitatedBy(VoreEntity pred) { }

        public virtual void OnDisposedBy(VoreEntity pred) { }

        public virtual IEnumerable<VoreEntity> GetSiblings() { yield break; }

        public virtual bool IsParent() => false;

        public virtual VoreEntity GetParent() => null;

        public virtual IEnumerable<VoreEntity> GetChildren() { yield break; }

        public abstract bool IsValid();

        public abstract int GetID();

        public virtual void OnDispose(VoreEntity prey) { }

        public virtual void OnBurp() { }

        public abstract EntityTags GetTags();

        public virtual bool IsChild() => false;

        public abstract string GetName();

        public abstract int GetLife();

        public abstract void SetLife(int life);

        public abstract int GetLifeMax();

        public abstract int GetMana();

        public abstract void SetMana(int mana);

        public abstract int GetManaMax();

        public abstract LegacySoundStyle GetHitSound();

        public abstract int GetDirection();

        public abstract void SetPosition(Vector2 position);

        public abstract Vector2 GetPosition();

        public abstract float GetSizeFactor();

        public abstract void Damage(VoreEntity damager, int damage, float knockback);

        public abstract void Knockback(Vector2 knockback);

        public abstract void Heal(int healing);

        public abstract void BackupState();

        public abstract void RestoreState();

        public abstract void SetStateSwallowed();

        public abstract int GetStruggleBonus(VoreEntity pred);

        public abstract int GetEscapeLimit(VoreEntity prey);

		public virtual int GetEscapeBonus(VoreEntity prey) => 0;

        public abstract bool IsHostileTo(VoreEntity other);

        public abstract float GetRandomVoreChance(VoreEntity pred);

        public abstract int GetCapacity();

        public abstract CharmEffects GetBaseCharms();

        public virtual bool ShouldShowPrey() => false;

        public virtual bool ShouldShowWhileSwallowed() => IsSwallowed() && GetPredator().ShouldShowPrey() && !VoreConfig.Instance.DebugNoLayeredPrey;

        public virtual float GetScale() => IsSwallowed() ? 0.75f : 1f;

        public virtual bool EligibleForVore() => true;

        string GetSwallowNoise()
        {
            WeightedRandom<string> noises = new WeightedRandom<string>();
            if (VoreConfig.Instance.SoundsSwallowGulping)
            {
                noises.Add("Gulp");
            }
            if (noises.elements.Count == 0) return null;
            return noises.Get();
        }

        string GetRegurgitateNoise()
        {
            WeightedRandom<string> noises = new WeightedRandom<string>();
            if (VoreConfig.Instance.SoundsRegurgitatePuking)
            {
                noises.Add("Puke");
            }
            if (noises.elements.Count == 0) return null;
            return noises.Get();
        }

        string GetDigestionNoise()
        {
            WeightedRandom<string> noises = new WeightedRandom<string>();
            if (VoreConfig.Instance.SoundsDigestionGurgling)
            {
                noises.Add("afewlargergroans");
                noises.Add("agroan");
                noises.Add("blrp");
                noises.Add("blrrpgrougl");
                noises.Add("blrrrrp");
                noises.Add("brbrbrbrblrbrgblgr");
                noises.Add("burblegoingdown");
                noises.Add("burblingIthink");
                noises.Add("burblywhine");
                noises.Add("fewgroans");
                noises.Add("glorp");
                noises.Add("glorpgrowl");
                noises.Add("glowrpblorp");
                noises.Add("groooooorwp");
                noises.Add("gwouuurg");
                noises.Add("hardglrn");
                noises.Add("littlerumble-longer");
                noises.Add("littlerumble");
                noises.Add("singlebworb");
                noises.Add("singlegroan");
                noises.Add("someburbling-deeper");
                noises.Add("someburbling");
                noises.Add("somesquirts-take2");
                noises.Add("somesquirts");
                noises.Add("squirtsandgurgling");
                noises.Add("squirtsthenrumble");
            }
            if (VoreConfig.Instance.SoundsDigestionBurping)
            {
                noises.Add("burp", 0.5);
                noises.Add("burp-short", 0.5);
                noises.Add("wetbelch", 0.25);
            }
            if (VoreConfig.Instance.SoundsDigestionFarting)
            {
                noises.Add("fart", 0.25);
                noises.Add("toot");
            }
            if (noises.elements.Count == 0) return null;
            return noises.Get();
        }

        string GetDisposalNoise()
        {
            WeightedRandom<string> noises = new WeightedRandom<string>();
            if (VoreConfig.Instance.SoundsDisposalBelching)
            {
                if (GetTags().HasAll(EntityTags.Female)) noises.Add("BelchF");
                if (GetTags().HasAll(EntityTags.Male)) noises.Add("BelchM");
                noises.Add("wetbelch", 0.5);
            }
            if (VoreConfig.Instance.SoundsDisposalFarting)
            {
                noises.Add("fart-long");
                noises.Add("fart");
            }
            if (noises.elements.Count == 0) return null;
            return noises.Get();
        }

        public static implicit operator VoreEntity(NPC npc) => npc.GetEntity();

        public static implicit operator VoreEntity(Player player) => player.GetEntity();

        public override string ToString() => GetName();
    }
}
