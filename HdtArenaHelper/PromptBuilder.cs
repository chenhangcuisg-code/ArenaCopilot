using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace HdtArenaHelper
{
	internal static class PromptBuilder
	{
		internal static string Play(GameSnapshot snapshot, IReadOnlyList<LegalAction> legalActions,
			PersonalStrategyNotes notes)
			=> "Choose the strongest Hearthstone Arena line from the supplied legal action primitives. "
			+ "Never invent a card, target, effect, secret, or hidden opponent card. The primitives are "
			+ "not complete multi-step simulations: use only entity ids they contain, account for mana once, "
			+ "and describe uncertainty briefly. Treat TACTICAL_FACTS as local arithmetic over the current "
			+ "visible state. Check survival and lethal before value, then give one exact executable sequence "
			+ "with named targets. Return Chinese player-facing text.\n\nGAME_SNAPSHOT\n"
			+ JsonConvert.SerializeObject(snapshot, Formatting.None)
			+ "\n\nTACTICAL_FACTS\n"
			+ JsonConvert.SerializeObject(TacticalAnalyzer.Analyze(snapshot), Formatting.None)
			+ "\n\nLEGAL_ACTION_PRIMITIVES\n"
			+ JsonConvert.SerializeObject(legalActions, Formatting.None)
			+ PersonalContext(notes);

		internal static string Draft(string deckClass, IReadOnlyList<int> deck,
			IReadOnlyList<DraftAiOption> options)
			=> "Choose exactly one Hearthstone Arena draft option using statistical/offline scores, current "
			+ "deck structure and supplied card text. Scores already include deck-fit: never add it twice. "
			+ "Compare each complete package, preserving duplicates. Structure counts are descriptive, not "
			+ "validated targets; unknown cards are excluded. Consider draft progress, early minion curve, "
			+ "removal, draw, survival and finishing tools. Never invent effects or predict a whole-deck win "
			+ "rate. Scores and confidence are not win rates. Historical card-inclusion win rate is not "
			+ "the causal win rate of this deck after picking it. decision must be exactly A, B, or C. "
			+ "steps must give one short comparison per option. reason must contain four concise Chinese "
			+ "items labelled 联动、缺口、可靠性、代价: name actual synergy partners and copy counts (or "
			+ "say none), explain the deck gap, distinguish existing synergy from speculative future picks, "
			+ "and explain the opportunity cost versus the strongest alternative. risk must state missing "
			+ "data and uncertain assumptions. Return Chinese player-facing text.\n\nDRAFT\n"
			+ JsonConvert.SerializeObject(DraftContext.Build(deckClass, deck, options), Formatting.None);

		internal static string Choice(string choiceKind, GameSnapshot snapshot,
			IReadOnlyList<DraftAiOption> options)
			=> $"Choose exactly one {choiceKind} option. decision must be exactly A, B, or C. "
			+ "Use the visible board and deck-fit data; never invent hidden information. Return Chinese "
			+ "player-facing text.\n\nCHOICE\n"
			+ JsonConvert.SerializeObject(new { snapshot, options }, Formatting.None);

		internal static string Mulligan(string deckClass, bool onCoin, IReadOnlyList<int> deck,
			IReadOnlyList<MulliganAiOption> hand)
			=> "Choose a mulligan for the complete opening hand. decision must concisely list KEEP and TOSS "
			+ "by A/B/C/D labels. Treat localRule as a deterministic deck-relative signal, but resolve "
			+ "situational cases from curve and hand composition. Never invent opponent cards. Return Chinese "
			+ "player-facing text.\n\nMULLIGAN\n"
			+ JsonConvert.SerializeObject(new { deckClass, onCoin, deck = DeckContext(deck), hand }, Formatting.None);

		private static object[] DeckContext(IReadOnlyList<int> deck)
			=> deck.Select(dbfId =>
			{
				var card = HearthDb.Cards.GetFromDbfId(dbfId);
				return (object)new
				{
					dbfId,
					name = card?.Name ?? dbfId.ToString(),
					cost = card?.Cost ?? 0,
					type = card?.Type.ToString() ?? "Unknown",
				};
			}).ToArray();

		private static string PersonalContext(PersonalStrategyNotes notes)
			=> "\n\nPERSONAL_STRATEGY\n" + notes.PersonalStrategy
				+ "\n\nLEARNED_LESSONS\n" + notes.Lessons;
	}

	internal sealed class DraftAiOption
	{
		public string Id { get; set; } = string.Empty;
		public int DbfId { get; set; }
		public string Name { get; set; } = string.Empty;
		public double Score { get; set; }
		public double DeckFit { get; set; }
		public IReadOnlyList<int> PackageDbfIds { get; set; } = new int[0];
		public bool HasScoreData { get; set; } = true;
		public bool IsLowConfidence { get; set; }
	}

	internal sealed class MulliganAiOption
	{
		public string Id { get; set; } = string.Empty;
		public int DbfId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string LocalRule { get; set; } = string.Empty;
	}
}
