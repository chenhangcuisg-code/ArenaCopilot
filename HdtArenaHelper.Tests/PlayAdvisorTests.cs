using System.Collections.Generic;
using Xunit;

namespace HdtArenaHelper.Tests
{
	public class PlayAdvisorTests
	{
		[Fact]
		public void StateHash_IsStableForEquivalentSnapshots()
		{
			var first = Snapshot(hand: new[] { Card(2, 3), Card(1, 2) });
			var second = Snapshot(hand: new[] { Card(1, 2), Card(2, 3) });

			Assert.Equal(StateHasher.Compute(first), StateHasher.Compute(second));
		}

		[Fact]
		public void StateHash_ChangesWhenManaChanges()
		{
			var first = Snapshot(mana: 4);
			var second = Snapshot(mana: 3);

			Assert.NotEqual(StateHasher.Compute(first), StateHasher.Compute(second));
		}

		[Fact]
		public void Lethal_UsesReadyAttackersAndWeaponWhenNoTauntExists()
		{
			var snapshot = Snapshot(
				opponentHealth: 8,
				weaponAttack: 2,
				board: new[] { Minion(10, 4, canAttack: true), Minion(11, 2, canAttack: true) });

			var lethal = LethalCalculator.FindGuaranteedLethal(snapshot);

			Assert.NotNull(lethal);
			Assert.Equal(8, lethal!.Damage);
			Assert.Equal(3, lethal.Steps.Count);
		}

		[Fact]
		public void Lethal_DoesNotClaimFaceDamageThroughTaunt()
		{
			var snapshot = Snapshot(
				opponentHealth: 4,
				board: new[] { Minion(10, 5, canAttack: true) },
				enemyBoard: new[] { Minion(20, 1, canAttack: false, taunt: true) });

			Assert.Null(LethalCalculator.FindGuaranteedLethal(snapshot));
		}

		[Fact]
		public void Lethal_DoesNotClaimDamageAgainstImmuneHero()
		{
			var snapshot = Snapshot(
				opponentHealth: 4,
				board: new[] { Minion(10, 5, canAttack: true) });
			snapshot.Opponent.IsImmune = true;

			Assert.Null(LethalCalculator.FindGuaranteedLethal(snapshot));
		}

		[Fact]
		public void LegalActions_RespectManaBoardSpaceAndTaunt()
		{
			var snapshot = Snapshot(
				mana: 3,
				hand: new[] { Card(1, 2, CardKind.Minion), Card(2, 4, CardKind.Spell) },
				board: new[] { Minion(10, 3, canAttack: true) },
				enemyBoard: new[] { Minion(20, 2, canAttack: false, taunt: true) });

			var actions = LegalActionGenerator.Generate(snapshot);

			Assert.Contains(actions, x => x.Kind == LegalActionKind.PlayCard && x.SourceEntityId == 1);
			Assert.DoesNotContain(actions, x => x.Kind == LegalActionKind.PlayCard && x.SourceEntityId == 2);
			Assert.Contains(actions, x => x.Kind == LegalActionKind.Attack && x.TargetEntityId == 20);
			Assert.DoesNotContain(actions, x => x.Kind == LegalActionKind.Attack && x.TargetEntityId == snapshot.Opponent.HeroEntityId);
		}

		[Fact]
		public void AdvisorResponse_ParsesStructuredOutput()
		{
			var advice = AdvisorResponse.Parse(
				"{\"decision\":\"B\",\"confidence\":0.82,\"steps\":[\"play\"],\"reason\":[\"tempo\"],\"risk\":\"AoE\"}");

			Assert.Equal("B", advice.Decision);
			Assert.Equal(0.82, advice.Confidence, 2);
			Assert.Equal("play", Assert.Single(advice.Steps));
			Assert.Equal("tempo", Assert.Single(advice.Reasons));
			Assert.Equal("AoE", advice.Risk);
		}

		[Fact]
		public void AdvisorVisibility_DoesNotDependOnScoreData()
		{
			var state = new OverlayState();

			state.AdvisorShown();

			Assert.True(state.WantVisible(dataReady: false));
			state.AdvisorGone();
			Assert.False(state.WantVisible(dataReady: false));
		}

		[Fact]
		public void LegalActions_IncludeAffordableUnusedHeroPower()
		{
			var snapshot = Snapshot(mana: 2);
			snapshot.Friendly.HeroPowerEntityId = 99;
			snapshot.Friendly.HeroPowerCost = 2;
			snapshot.Friendly.HeroPowerAvailable = true;

			var actions = LegalActionGenerator.Generate(snapshot);

			Assert.Contains(actions, x => x.Kind == LegalActionKind.UseHeroPower && x.SourceEntityId == 99);
		}

		private static GameSnapshot Snapshot(int mana = 4, int opponentHealth = 30,
			int weaponAttack = 0, IReadOnlyList<CardSnapshot>? hand = null,
			IReadOnlyList<MinionSnapshot>? board = null, IReadOnlyList<MinionSnapshot>? enemyBoard = null)
			=> new GameSnapshot
			{
				GameId = "test",
				Turn = 5,
				IsMyTurn = true,
				ManaAvailable = mana,
				ManaMaximum = 5,
				Friendly = new PlayerSnapshot { HeroEntityId = 100, Health = 30, HeroAttack = weaponAttack, CanAttack = weaponAttack > 0 },
				Opponent = new PlayerSnapshot { HeroEntityId = 200, Health = opponentHealth },
				Hand = hand ?? new List<CardSnapshot>(),
				FriendlyBoard = board ?? new List<MinionSnapshot>(),
				EnemyBoard = enemyBoard ?? new List<MinionSnapshot>(),
			};

		private static CardSnapshot Card(int id, int cost, CardKind kind = CardKind.Spell)
			=> new CardSnapshot { EntityId = id, CardId = "CARD_" + id, Name = "Card " + id, Cost = cost, Kind = kind };

		private static MinionSnapshot Minion(int id, int attack, bool canAttack, bool taunt = false)
			=> new MinionSnapshot { EntityId = id, Name = "Minion " + id, Attack = attack, Health = 3, CanAttack = canAttack, HasTaunt = taunt };
	}
}
