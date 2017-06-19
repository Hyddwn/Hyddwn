﻿using Aura.Channel.Network.Sending;
using Aura.Channel.Skills.Base;
using Aura.Channel.World.Entities;
using Aura.Channel.World.Entities.Creatures;
using Aura.Data;
using Aura.Mabi;
using Aura.Mabi.Const;
using Aura.Shared.Util;
using System;

namespace Aura.Channel.Skills.Transformations
{
	/// <summary>
	/// Handler for Paladin transformation.
	/// </summary>
	/// <remarks>
	/// Spirit of Order
	///   Var1: Max HP
	///   Var2: Max MP
	///   Var3: Max Stamina
	///   Var4: Defense
	///   Var5: Protection
	///   Var6: ?
	///   Var7: Passive Defense Activation Bonus (%)
	///   Var8: Magic Defense
	///   Var9: Magic Protection
	///   Var20: Duration
	///   IntVar1: Transformation Level
	///   IntVar2: Transformation Title
	///   
	/// Power of Order
	///   Var1: STR
	///   Var2: Will
	///   Var3: ?
	///   
	/// Eye of Order
	///   Var1: DEX
	///   Var2: Balance
	///   Var3: ?
	/// 
	/// Sword of Order
	///   Var1: Damage
	///   Var2: Injury Rate
	///   Var3: ?
	///   Var4: ?
	///   Var5: ?
	/// </remarks>
	[Skill(SkillId.SpiritOfOrder)]
	public class SpiritOfOrderHandler : StartStopSkillHandler
	{
		/// <summary>
		/// Transforms creature.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="dict"></param>
		/// <returns></returns>
		public override StartStopResult Start(Creature creature, Skill skill, MabiDictionary dict)
		{
			if (DateTime.Now < skill.CoolDownEnd)
			{
				creature.Notice(Localization.Get("You can't use this for the rest of the day."));
				return StartStopResult.Fail;
			}

			this.Transform(creature, skill);
			this.GiveStatMods(creature, skill);
			this.SetTimers(creature, skill);

			return StartStopResult.Okay;
		}

		/// <summary>
		/// Stops creature's transformation.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="dict"></param>
		/// <returns></returns>
		public override StartStopResult Stop(Creature creature, Skill skill, MabiDictionary dict)
		{
			this.RemoveStatMods(creature, skill);
			this.ResetTransformation(creature, skill);

			return StartStopResult.Okay;
		}

		/// <summary>
		/// Transforms creature and updates clients.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		private void Transform(Creature creature, Skill skill)
		{
			creature.Transformation = Transformation.Paladin;
			creature.TransformationSkillRank = skill.Info.Rank;
			creature.TransformationLevel = (TransformationLevel)skill.RankData.IntVar1;

			Send.UpdateTransformation(creature);
		}

		/// <summary>
		/// Returns timespan after which the transformation ends.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		private TimeSpan GetDuration(Creature creature, Skill skill)
		{
			var result = TimeSpan.FromSeconds(60 * 3 + 45); // 3m 45s

			// +7s per rebirth, up to 25, up to 6m
			// +1s per rebirth, past 25, no limit
			// -2s per age, up to 25
			// Reference: http://wiki.mabinogiworld.com/view/Paladin

			result.Add(TimeSpan.FromSeconds(7 * Math.Min(25, (int)creature.Age)));
			if (result > TimeSpan.FromMinutes(6))
				result = TimeSpan.FromMinutes(6);

			result.Add(TimeSpan.FromSeconds(1 * Math.Max(0, (int)creature.Age - 25)));
			result.Subtract(TimeSpan.FromSeconds(2 * Math.Min(25, (int)creature.Age)));

			var rate = ChannelServer.Instance.Conf.World.PaladinDurationRate;
			if (rate != 1)
				result = TimeSpan.FromMilliseconds(result.TotalMilliseconds * rate);

			return result;
		}

		/// <summary>
		/// Sets skill's auto cancel time and cool down.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		private void SetTimers(Creature creature, Skill skill)
		{
			creature.Skills.CancelAfter(skill.Info.Id, this.GetDuration(creature, skill));
			skill.CoolDownEnd = ErinnTime.GetNextTime(6, 0).DateTime;
		}

		/// <summary>
		/// Gives stat mods for transformation, based on skill's rank and
		/// the passive transformation skill's ranks, heals creature,
		/// and updates clients.
		/// The mod identifiers are "Skill" and the transformation skill's id.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		private void GiveStatMods(Creature creature, Skill skill)
		{
			var powerOfOrder = creature.Skills.Get(SkillId.PowerOfOrder);
			var eyeOfOrder = creature.Skills.Get(SkillId.EyeOfOrder);
			var swordOfOrder = creature.Skills.Get(SkillId.SwordOfOrder);

			// Spirit of Order bonuses
			creature.StatMods.Add(Stat.LifeMaxMod, skill.RankData.Var1, StatModSource.Skill, (long)skill.Info.Id);
			creature.StatMods.Add(Stat.ManaMaxMod, skill.RankData.Var2, StatModSource.Skill, (long)skill.Info.Id);
			creature.StatMods.Add(Stat.StaminaMaxMod, skill.RankData.Var3, StatModSource.Skill, (long)skill.Info.Id);
			creature.StatMods.Add(Stat.DefenseMod, skill.RankData.Var4, StatModSource.Skill, (long)skill.Info.Id);
			creature.StatMods.Add(Stat.ProtectionMod, skill.RankData.Var5, StatModSource.Skill, (long)skill.Info.Id);

			if (AuraData.FeaturesDb.IsEnabled("PaladinMagicDefense"))
			{
				creature.StatMods.Add(Stat.MagicDefenseMod, skill.RankData.Var8, StatModSource.Skill, (long)skill.Info.Id);
				creature.StatMods.Add(Stat.MagicProtectionMod, skill.RankData.Var9, StatModSource.Skill, (long)skill.Info.Id);
			}

			// Power of Order bonuses
			if (powerOfOrder != null)
			{
				creature.StatMods.Add(Stat.StrMod, powerOfOrder.RankData.Var1, StatModSource.Skill, (long)skill.Info.Id);
				creature.StatMods.Add(Stat.WillMod, powerOfOrder.RankData.Var2, StatModSource.Skill, (long)skill.Info.Id);
			}

			// Eye of Order bonuses
			if (eyeOfOrder != null)
			{
				creature.StatMods.Add(Stat.DexMod, powerOfOrder.RankData.Var1, StatModSource.Skill, (long)skill.Info.Id);
				creature.StatMods.Add(Stat.BalanceMod, powerOfOrder.RankData.Var2, StatModSource.Skill, (long)skill.Info.Id);
			}

			// Sword of Order bonuses
			if (swordOfOrder != null)
			{
				creature.StatMods.Add(Stat.AttackMinMod, swordOfOrder.RankData.Var1, StatModSource.Skill, (long)skill.Info.Id);
				creature.StatMods.Add(Stat.AttackMaxMod, swordOfOrder.RankData.Var1, StatModSource.Skill, (long)skill.Info.Id);
				creature.StatMods.Add(Stat.InjuryMinMod, swordOfOrder.RankData.Var2, StatModSource.Skill, (long)skill.Info.Id);
				creature.StatMods.Add(Stat.InjuryMaxMod, swordOfOrder.RankData.Var2, StatModSource.Skill, (long)skill.Info.Id);
			}

			// Restore stats
			creature.Injuries -= skill.RankData.Var1;
			creature.Life += skill.RankData.Var1;
			creature.Mana += skill.RankData.Var2;
			creature.Hunger -= skill.RankData.Var3;
			creature.Stamina += skill.RankData.Var3;

			this.UpdateClientStats(creature);
		}

		/// <summary>
		/// Updates public and private stats relevant to the transformation
		/// on all clients in range of creature.
		/// </summary>
		/// <param name="creature"></param>
		private void UpdateClientStats(Creature creature)
		{
			Send.StatUpdate(creature, StatUpdateType.Public, Stat.Life, Stat.LifeInjured, Stat.LifeMaxMod, Stat.LifeMax);
			Send.StatUpdate(creature, StatUpdateType.Private,
				Stat.Life, Stat.LifeInjured, Stat.LifeMax, Stat.LifeMaxMod,
				Stat.Mana, Stat.ManaMax, Stat.ManaMaxMod,
				Stat.Stamina, Stat.Hunger, Stat.StaminaMax, Stat.StaminaMaxMod,
				Stat.DefenseMod, Stat.ProtectionMod, Stat.MagicDefenseMod, Stat.MagicProtectionMod,
				Stat.StrMod, Stat.WillMod, Stat.DexMod, Stat.BalanceMod,
				Stat.AttackMinMod, Stat.AttackMaxMod, Stat.InjuryMinMod, Stat.InjuryMaxMod
			);
		}

		/// <summary>
		/// Resets creature's transformation and updates clients.
		/// </summary>
		/// <param name="creature"></param>
		private void ResetTransformation(Creature creature, Skill skill)
		{
			creature.Transformation = Transformation.None;
			creature.TransformationSkillRank = SkillRank.Novice;
			creature.TransformationLevel = TransformationLevel.None;

			Send.UpdateTransformation(creature);
		}

		/// <summary>
		/// Removes stat mods added when the transformation began and
		/// updates clients.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		private void RemoveStatMods(Creature creature, Skill skill)
		{
			// Save stat rates before removing the bonuses
			var injuryRate = (1f / creature.LifeMax * creature.Injuries);
			var lifeRate = (1f / creature.LifeMax * creature.Life);
			var manaRate = (1f / creature.ManaMax * creature.Mana);
			var staminaRate = (1f / creature.StaminaMax * creature.Stamina);

			creature.StatMods.Remove(StatModSource.Skill, (long)skill.Info.Id);

			// Adjust stats to the percentages they were before removing
			// the bonuses.
			creature.Injuries = (creature.LifeMax * injuryRate);
			creature.Life = (creature.LifeMax * lifeRate);
			creature.Mana = (creature.ManaMax * manaRate);
			creature.Stamina = (creature.StaminaMax * staminaRate);

			this.UpdateClientStats(creature);
		}
	}
}
