using System.Collections.Generic;
using System.Linq;

namespace HdtArenaHelper
{
	public enum LegalActionKind
	{
		PlayCard,
		Attack,
		UseHeroPower,
		EndTurn,
	}

	public sealed class LegalAction
	{
		public string Id { get; set; } = string.Empty;
		public LegalActionKind Kind { get; set; }
		public int SourceEntityId { get; set; }
		public int? TargetEntityId { get; set; }
		public string Description { get; set; } = string.Empty;
	}

	public static class LegalActionGenerator
	{
		private const int BoardLimit = 7;

		public static IReadOnlyList<LegalAction> Generate(GameSnapshot snapshot)
		{
			var result = new List<LegalAction>();
			if(!snapshot.IsMyTurn)
				return result;

			foreach(var card in snapshot.Hand.Where(x => x.Cost <= snapshot.ManaAvailable))
			{
				if((card.Kind == CardKind.Minion || card.Kind == CardKind.Location)
					&& snapshot.FriendlyBoard.Count >= BoardLimit)
					continue;
				result.Add(new LegalAction
				{
					Id = "play:" + card.EntityId,
					Kind = LegalActionKind.PlayCard,
					SourceEntityId = card.EntityId,
					Description = $"Play [{card.EntityId}] {card.Name} ({card.Cost} mana)",
				});
			}

			var taunts = snapshot.EnemyBoard.Where(x => x.HasTaunt).ToList();
			var targets = taunts.Count > 0 ? taunts : snapshot.EnemyBoard.ToList();
			foreach(var attacker in snapshot.FriendlyBoard.Where(x => x.CanAttack && x.Attack > 0))
			{
				foreach(var target in targets)
					result.Add(Attack(attacker.EntityId, attacker.Name, target.EntityId, target.Name,
						attacker.AttacksRemaining));
				if(taunts.Count == 0)
					result.Add(Attack(attacker.EntityId, attacker.Name, snapshot.Opponent.HeroEntityId, "enemy hero",
						attacker.AttacksRemaining));
			}

			if(snapshot.Friendly.CanAttack && snapshot.Friendly.HeroAttack > 0)
			{
				foreach(var target in targets)
					result.Add(Attack(snapshot.Friendly.HeroEntityId, "your hero", target.EntityId, target.Name,
						snapshot.Friendly.AttacksRemaining));
				if(taunts.Count == 0)
					result.Add(Attack(snapshot.Friendly.HeroEntityId, "your hero",
						snapshot.Opponent.HeroEntityId, "enemy hero", snapshot.Friendly.AttacksRemaining));
			}

			if(snapshot.Friendly.HeroPowerAvailable
				&& snapshot.Friendly.HeroPowerCost <= snapshot.ManaAvailable)
			{
				result.Add(new LegalAction
				{
					Id = "hero-power:" + snapshot.Friendly.HeroPowerEntityId,
					Kind = LegalActionKind.UseHeroPower,
					SourceEntityId = snapshot.Friendly.HeroPowerEntityId,
					Description = $"Use hero power ({snapshot.Friendly.HeroPowerCost} mana)",
				});
			}

			result.Add(new LegalAction { Id = "end-turn", Kind = LegalActionKind.EndTurn, Description = "End turn" });
			return result;
		}

		private static LegalAction Attack(int sourceId, string source, int targetId, string target,
			int attacksRemaining)
			=> new LegalAction
			{
				Id = $"attack:{sourceId}:{targetId}",
				Kind = LegalActionKind.Attack,
				SourceEntityId = sourceId,
				TargetEntityId = targetId,
				Description = $"[{sourceId}] {source} attacks [{targetId}] {target}"
					+ (attacksRemaining > 1 ? $" ({attacksRemaining} attacks remain)" : string.Empty),
			};
	}
}
