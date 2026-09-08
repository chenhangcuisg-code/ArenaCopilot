using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace HdtArenaHelper
{
	internal static class PromptBuilder
	{
		internal static string Play(GameSnapshot snapshot, IReadOnlyList<LegalAction> legalActions)
			=> "Choose the strongest Hearthstone Arena line from the supplied legal action primitives. "
			+ "Never invent a card, target, effect, secret, or hidden opponent card. The primitives are "
			+ "not complete multi-step simulations: use only entity ids they contain, account for mana once, "
			+ "and describe uncertainty briefly. Return Chinese player-facing text.\n\nGAME_SNAPSHOT\n"
			+ JsonConvert.SerializeObject(snapshot, Formatting.None)
			+ "\n\nLEGAL_ACTION_PRIMITIVES\n"
			+ JsonConvert.SerializeObject(legalActions, Formatting.None);

		internal static string Draft(string deckClass, IReadOnlyList<int> deck,
			IReadOnlyList<DraftAiOption> options)
			=> "Choose exactly one Hearthstone Arena draft option. Base score is empirical/offline data and "
			+ "deckFit is the existing bounded synergy adjustment. Use deck context to break close calls; do "
			+ "not invent unavailable card facts. decision must be exactly A, B, or C. Return Chinese "
			+ "player-facing text.\n\nDRAFT\n"
			+ JsonConvert.SerializeObject(new { deckClass, deck = DeckContext(deck), options }, Formatting.None);

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
	}

	internal sealed class DraftAiOption
	{
		public string Id { get; set; } = string.Empty;
		public int DbfId { get; set; }
		public string Name { get; set; } = string.Empty;
		public double Score { get; set; }
		public double DeckFit { get; set; }
	}

	internal sealed class MulliganAiOption
	{
		public string Id { get; set; } = string.Empty;
		public int DbfId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string LocalRule { get; set; } = string.Empty;
	}
}
