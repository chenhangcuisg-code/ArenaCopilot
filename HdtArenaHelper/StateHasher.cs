using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HdtArenaHelper
{
	public static class StateHasher
	{
		public static string Compute(GameSnapshot snapshot)
		{
			if(snapshot == null)
				throw new ArgumentNullException(nameof(snapshot));

			var text = new StringBuilder()
				.Append(snapshot.GameId).Append('|').Append(snapshot.Turn).Append('|')
				.Append(snapshot.IsMyTurn).Append('|').Append(snapshot.ManaAvailable).Append('|')
				.Append(snapshot.ManaMaximum).Append('|')
				.Append(Player(snapshot.Friendly)).Append('|').Append(Player(snapshot.Opponent));
			foreach(var card in snapshot.Hand.OrderBy(x => x.EntityId))
				text.Append("|h:").Append(card.EntityId).Append(':').Append(card.CardId).Append(':').Append(card.Cost);
			foreach(var minion in snapshot.FriendlyBoard.OrderBy(x => x.EntityId))
				text.Append("|f:").Append(Minion(minion));
			foreach(var minion in snapshot.EnemyBoard.OrderBy(x => x.EntityId))
				text.Append("|e:").Append(Minion(minion));
			foreach(var card in snapshot.CardsPlayedByOpponent)
				text.Append("|op:").Append(card);

			using(var sha = SHA256.Create())
				return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())))
					.Replace("-", string.Empty).ToLowerInvariant();
		}

		private static string Player(PlayerSnapshot player)
			=> string.Join(":", player.HeroEntityId, player.Class, player.Health, player.Armor,
				player.HeroAttack, player.CanAttack, player.IsImmune, player.HeroPowerEntityId, player.HeroPowerCost,
				player.HeroPowerAvailable, player.HandSize, player.DeckSize, player.SecretCount);

		private static string Minion(MinionSnapshot minion)
			=> string.Join(":", minion.EntityId, minion.CardId, minion.Attack, minion.Health,
				minion.CanAttack, minion.HasTaunt, minion.HasDivineShield, minion.IsFrozen);
	}
}
