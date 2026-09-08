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
			var damage = ready.Sum(x => x.Attack);
			var steps = ready.Select(x => $"[{x.EntityId}] {x.Name} -> enemy hero ({x.Attack})").ToList();
			if(snapshot.Friendly.CanAttack && snapshot.Friendly.HeroAttack > 0)
			{
				damage += snapshot.Friendly.HeroAttack;
				steps.Add($"Your hero -> enemy hero ({snapshot.Friendly.HeroAttack})");
			}

			return damage >= snapshot.Opponent.Health + snapshot.Opponent.Armor
				? new LethalLine(damage, steps)
				: null;
		}
	}
}
