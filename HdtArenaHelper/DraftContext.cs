using System.Collections.Generic;
using System.Linq;

namespace HdtArenaHelper
{
	internal static class DraftContext
	{
		internal static object Build(string deckClass, IReadOnlyList<int> deck,
			IReadOnlyList<DraftAiOption> options) => new
			{
				deckClass,
				draftedCardCount = deck.Count,
				deck = Cards(deck),
				curveBuckets = new[] { "0-1", "2", "3", "4", "5", "6", "7+" },
				currentStructure = DeckMechanics.Describe(deck.ToArray()),
				options = options.Select(option =>
				{
					var added = new[] { option.DbfId }.Concat(option.PackageDbfIds).ToArray();
					var projected = deck.Concat(added).ToArray();
					return new
					{
						id = option.Id,
						name = option.Name,
						displayScore = option.HasScoreData ? (double?)option.Score : null,
						hasScoreData = option.HasScoreData,
						lowConfidence = option.IsLowConfidence,
						deckFitAlreadyIncluded = option.DeckFit,
						cards = Cards(added),
						projectedCardCount = projected.Length,
						projectedStructure = DeckMechanics.Describe(projected),
						unknownCardCount = projected.Count(id => HearthDb.Cards.GetFromDbfId(id) == null)
					};
				}).ToArray()
			};

		internal static object[] Cards(IEnumerable<int> ids) => ids.Select(id =>
		{
			var card = HearthDb.Cards.GetFromDbfId(id);
			return (object)new
			{
				dbfId = id,
				known = card != null,
				name = card?.Name ?? id.ToString(),
				cost = card == null ? (int?)null : card.Cost,
				type = card?.Type.ToString() ?? "Unknown",
				text = card?.Text
			};
		}).ToArray();
	}
}
