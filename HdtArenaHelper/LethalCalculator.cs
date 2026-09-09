using System;
using System.Collections.Generic;
using System.Linq;

namespace HdtArenaHelper
{
	public sealed class LethalLine
	{
		public int Damage { get; }
		public IReadOnlyList<string> Steps { get; }

		public LethalLine(int damage, IReadOnlyList<string> steps)
		{
			Damage = damage;
			Steps = steps;
		}
	}

	public static class LethalCalculator
	{
		public static LethalLine? FindGuaranteedLethal(GameSnapshot snapshot)
		{
			if(!snapshot.IsMyTurn || snapshot.Opponent.IsImmune || snapshot.EnemyBoard.Any(x => x.HasTaunt))
				return null;

			var ready = snapshot.FriendlyBoard.Where(x => x.CanAttack && x.Attack > 0).ToList();
			var damage = 0;
			var steps = new List<string>();
			foreach(var attacker in ready)
			{
				var attacks = Math.Max(1, attacker.AttacksRemaining);
				damage += attacker.Attack * attacks;
				for(var i = 0; i < attacks; i++)
					steps.Add($"[{attacker.EntityId}] {attacker.Name} -> enemy hero ({attacker.Attack})");
			}
			if(snapshot.Friendly.CanAttack && snapshot.Friendly.HeroAttack > 0)
			{
				var attacks = Math.Max(1, snapshot.Friendly.AttacksRemaining);
				damage += snapshot.Friendly.HeroAttack * attacks;
				for(var i = 0; i < attacks; i++)
					steps.Add($"Your hero -> enemy hero ({snapshot.Friendly.HeroAttack})");
			}

			return damage >= snapshot.Opponent.Health + snapshot.Opponent.Armor
				? new LethalLine(damage, steps)
				: null;
		}
	}
}
