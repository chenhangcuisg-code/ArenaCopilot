using System;
using System.Linq;
using Newtonsoft.Json;

namespace HdtArenaHelper
{
	/// <summary>
	/// Arithmetic that is safe to state without simulating card text or guessing hidden cards.
	/// The language model receives these values as facts so it can spend its turn budget on
	/// sequencing and strategy instead of repeatedly recounting the visible board.
	/// </summary>
	public sealed class TacticalFacts
	{
		[JsonProperty("friendlyEffectiveHealth")]
		public int FriendlyEffectiveHealth { get; set; }

		[JsonProperty("opponentEffectiveHealth")]
		public int OpponentEffectiveHealth { get; set; }

		[JsonProperty("readyAttackDamage")]
		public int ReadyAttackDamage { get; set; }

		[JsonProperty("unblockedFaceAttackDamage")]
		public int UnblockedFaceAttackDamage { get; set; }

		[JsonProperty("lethalGap")]
		public int LethalGap { get; set; }

		[JsonProperty("enemyVisibleBoardAttack")]
		public int EnemyVisibleBoardAttack { get; set; }

		[JsonProperty("enemyTauntCount")]
		public int EnemyTauntCount { get; set; }

		[JsonProperty("faceAttacksBlocked")]
		public bool FaceAttacksBlocked { get; set; }

		[JsonProperty("friendlyBoardSlots")]
		public int FriendlyBoardSlots { get; set; }

		[JsonProperty("handSlots")]
		public int HandSlots { get; set; }
	}

	public static class TacticalAnalyzer
	{
		private const int BoardLimit = 7;
		private const int HandLimit = 10;

		public static TacticalFacts Analyze(GameSnapshot snapshot)
		{
			if(snapshot == null)
				throw new ArgumentNullException(nameof(snapshot));

			var boardDamage = snapshot.FriendlyBoard
				.Where(x => x.CanAttack && x.Attack > 0)
				.Sum(x => x.Attack * Math.Max(1, x.AttacksRemaining));
			var heroDamage = snapshot.Friendly.CanAttack
				? Math.Max(0, snapshot.Friendly.HeroAttack)
					* Math.Max(1, snapshot.Friendly.AttacksRemaining)
				: 0;
			var readyDamage = boardDamage + heroDamage;
			var enemyTaunts = snapshot.EnemyBoard.Count(x => x.HasTaunt);
			var blocked = enemyTaunts > 0 || snapshot.Opponent.IsImmune;
			var faceDamage = blocked ? 0 : readyDamage;
			var opponentHealth = Math.Max(0, snapshot.Opponent.Health)
				+ Math.Max(0, snapshot.Opponent.Armor);

			return new TacticalFacts
			{
				FriendlyEffectiveHealth = Math.Max(0, snapshot.Friendly.Health)
					+ Math.Max(0, snapshot.Friendly.Armor),
				OpponentEffectiveHealth = opponentHealth,
				ReadyAttackDamage = readyDamage,
				UnblockedFaceAttackDamage = faceDamage,
				LethalGap = Math.Max(0, opponentHealth - faceDamage),
				EnemyVisibleBoardAttack = snapshot.EnemyBoard
					.Where(x => !x.IsFrozen && x.Attack > 0)
					.Sum(x => x.Attack),
				EnemyTauntCount = enemyTaunts,
				FaceAttacksBlocked = blocked,
				FriendlyBoardSlots = Math.Max(0, BoardLimit - snapshot.FriendlyBoard.Count),
				HandSlots = Math.Max(0, HandLimit - snapshot.Hand.Count),
			};
		}
	}
}
