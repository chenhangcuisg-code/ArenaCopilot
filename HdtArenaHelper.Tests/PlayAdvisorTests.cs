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
		public void StateHash_ChangesWhenRemainingAttacksChange()
		{
			var first = Snapshot(board: new[] { Minion(10, 3, true, attacksRemaining: 2) });
			var second = Snapshot(board: new[] { Minion(10, 3, true, attacksRemaining: 1) });
			Assert.NotEqual(StateHasher.Compute(first), StateHasher.Compute(second));
			var heroFirst = Snapshot(weaponAttack: 3);
			var heroSecond = Snapshot(weaponAttack: 3);
			heroFirst.Friendly.AttacksRemaining = 2;
			Assert.NotEqual(StateHasher.Compute(heroFirst), StateHasher.Compute(heroSecond));
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
		public void Lethal_UsesEveryRemainingWindfuryAttack()
		{
			var snapshot = Snapshot(
				opponentHealth: 6,
				board: new[] { Minion(10, 3, canAttack: true, attacksRemaining: 2) });

			var lethal = LethalCalculator.FindGuaranteedLethal(snapshot);

			Assert.NotNull(lethal);
			Assert.Equal(6, lethal!.Damage);
			Assert.Equal(2, lethal.Steps.Count);
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

		[Fact]
		public void TacticalFacts_ReportVisibleDamageAndCapacityWithoutGuessingHiddenCards()
		{
			var snapshot = Snapshot(
				mana: 6,
				opponentHealth: 12,
				weaponAttack: 2,
				hand: new[] { Card(1, 2), Card(2, 5) },
				board: new[] { Minion(10, 4, canAttack: true), Minion(11, 3, canAttack: false) },
				enemyBoard: new[] { Minion(20, 5, canAttack: false, taunt: true), Minion(21, 2, canAttack: false) });
			snapshot.Friendly.Armor = 4;
			snapshot.Opponent.Armor = 3;

			var facts = TacticalAnalyzer.Analyze(snapshot);

			Assert.Equal(34, facts.FriendlyEffectiveHealth);
			Assert.Equal(15, facts.OpponentEffectiveHealth);
			Assert.Equal(6, facts.ReadyAttackDamage);
			Assert.Equal(7, facts.EnemyVisibleBoardAttack);
			Assert.Equal(1, facts.EnemyTauntCount);
			Assert.True(facts.FaceAttacksBlocked);
			Assert.Equal(0, facts.UnblockedFaceAttackDamage);
			Assert.Equal(15, facts.LethalGap);
			Assert.Equal(5, facts.FriendlyBoardSlots);
			Assert.Equal(8, facts.HandSlots);
		}

		[Fact]
		public void PlayPrompt_IncludesTacticalFactsAndPersonalStrategy()
		{
			var snapshot = Snapshot(
				opponentHealth: 10,
				board: new[] { Minion(10, 4, canAttack: true) });
			var prompt = PromptBuilder.Play(snapshot, LegalActionGenerator.Generate(snapshot),
				new PersonalStrategyNotes("优先争夺场面", "上局漏算英雄攻击"));

			Assert.Contains("TACTICAL_FACTS", prompt);
			Assert.Contains("\"readyAttackDamage\":4", prompt);
			Assert.Contains("PERSONAL_STRATEGY", prompt);
			Assert.Contains("优先争夺场面", prompt);
			Assert.Contains("LEARNED_LESSONS", prompt);
			Assert.Contains("上局漏算英雄攻击", prompt);
		}

		[Fact]
		public void CodexExecutableResolver_FindsDesktopCliBeforeNpmCli()
		{
			var roaming = @"C:\profiles\me\roaming";
			var local = @"C:\profiles\me\local";
			var desktopCli = System.IO.Path.Combine(local, "Programs", "OpenAI", "Codex", "bin", "codex.exe");

			var found = CodexExecutableResolver.Find(roaming, local,
				path => string.Equals(path, desktopCli, System.StringComparison.OrdinalIgnoreCase));

			Assert.Equal(desktopCli, found);
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
				Friendly = new PlayerSnapshot
				{
					HeroEntityId = 100,
					Health = 30,
					HeroAttack = weaponAttack,
					CanAttack = weaponAttack > 0,
					AttacksRemaining = weaponAttack > 0 ? 1 : 0,
				},
				Opponent = new PlayerSnapshot { HeroEntityId = 200, Health = opponentHealth },
				Hand = hand ?? new List<CardSnapshot>(),
				FriendlyBoard = board ?? new List<MinionSnapshot>(),
				EnemyBoard = enemyBoard ?? new List<MinionSnapshot>(),
			};

		private static CardSnapshot Card(int id, int cost, CardKind kind = CardKind.Spell)
			=> new CardSnapshot { EntityId = id, CardId = "CARD_" + id, Name = "Card " + id, Cost = cost, Kind = kind };

		private static MinionSnapshot Minion(int id, int attack, bool canAttack, bool taunt = false,
			int attacksRemaining = 1)
			=> new MinionSnapshot
			{
				EntityId = id,
				Name = "Minion " + id,
				Attack = attack,
				Health = 3,
				CanAttack = canAttack,
				AttacksRemaining = canAttack ? attacksRemaining : 0,
				HasTaunt = taunt,
			};
	}
}
