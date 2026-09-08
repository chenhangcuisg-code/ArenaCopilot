using System;
using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Entity = Hearthstone_Deck_Tracker.Hearthstone.Entities.Entity;

namespace HdtArenaHelper
{
	internal static class SnapshotBuilder
	{
		internal static GameSnapshot? TryBuild()
		{
			var game = Core.Game;
			if(game == null || game.GameEntity == null || game.PlayerEntity == null
				|| game.Player?.Hero == null || game.Opponent?.Hero == null)
				return null;

			var mana = game.PlayerEntity.GetTag(GameTag.RESOURCES)
				- game.PlayerEntity.GetTag(GameTag.RESOURCES_USED)
				+ game.PlayerEntity.GetTag(GameTag.TEMP_RESOURCES);
			return new GameSnapshot
			{
				GameId = game.CurrentGameStats != null && game.CurrentGameStats.StartTime > DateTime.MinValue
					? game.CurrentGameStats.StartTime.Ticks.ToString()
					: game.GameEntity.Id.ToString(),
				Turn = game.GameEntity.GetTag(GameTag.TURN),
				IsMyTurn = game.PlayerEntity.GetTag(GameTag.CURRENT_PLAYER) > 0,
				ManaAvailable = Math.Max(0, mana),
				ManaMaximum = Math.Max(0, game.Player.MaxMana),
				Friendly = BuildPlayer(game.Player),
				Opponent = BuildPlayer(game.Opponent),
				Hand = game.Player.Hand.Where(x => x != null).Select(BuildCard).OrderBy(x => x.EntityId).ToList(),
				FriendlyBoard = game.Player.Minions.Where(x => x != null).Select(BuildMinion).OrderBy(x => x.EntityId).ToList(),
				EnemyBoard = game.Opponent.Minions.Where(x => x != null).Select(BuildMinion).OrderBy(x => x.EntityId).ToList(),
				CardsPlayedByOpponent = game.Opponent.CardsPlayedThisMatch
					.Where(x => x != null && !string.IsNullOrEmpty(x.Name)).Select(x => x.Name!).ToList(),
			};
		}

		private static PlayerSnapshot BuildPlayer(Hearthstone_Deck_Tracker.Hearthstone.Player player)
		{
			var hero = player.Hero!;
			var heroPower = player.PlayerEntities.FirstOrDefault(x => x != null && x.IsHeroPower && x.IsInPlay);
			return new PlayerSnapshot
			{
				HeroEntityId = hero.Id,
				Class = player.CurrentClass ?? player.OriginalClass ?? string.Empty,
				Health = Math.Max(0, hero.Health),
				Armor = Math.Max(0, hero.GetTag(GameTag.ARMOR)),
				HeroAttack = Math.Max(0, hero.Attack),
				CanAttack = CanAttack(hero),
				IsImmune = hero.GetTag(GameTag.IMMUNE) > 0,
				HeroPowerEntityId = heroPower?.Id ?? 0,
				HeroPowerCost = heroPower == null ? 0 : Math.Max(0, heroPower.Cost),
				HeroPowerAvailable = heroPower != null && heroPower.GetTag(GameTag.EXHAUSTED) == 0,
				HandSize = player.HandCount,
				DeckSize = player.DeckCount,
				SecretCount = player.SecretZone.Count(),
			};
		}

		private static CardSnapshot BuildCard(Entity entity)
		{
			HearthDb.Cards.All.TryGetValue(entity.CardId ?? string.Empty, out var card);
			return new CardSnapshot
			{
				EntityId = entity.Id,
				CardId = entity.CardId ?? string.Empty,
				Name = entity.LocalizedName ?? entity.Name ?? entity.CardId ?? entity.Id.ToString(),
				Cost = Math.Max(0, entity.Cost),
				Kind = Kind(card?.Type ?? CardType.INVALID),
				Text = card?.Text ?? string.Empty,
			};
		}

		private static MinionSnapshot BuildMinion(Entity entity)
			=> new MinionSnapshot
			{
				EntityId = entity.Id,
				CardId = entity.CardId ?? string.Empty,
				Name = entity.LocalizedName ?? entity.Name ?? entity.CardId ?? entity.Id.ToString(),
				Attack = Math.Max(0, entity.Attack),
				Health = Math.Max(0, entity.Health),
				CanAttack = CanAttack(entity),
				HasTaunt = entity.GetTag(GameTag.TAUNT) > 0,
				HasDivineShield = entity.GetTag(GameTag.DIVINE_SHIELD) > 0,
				IsFrozen = entity.GetTag(GameTag.FROZEN) > 0,
			};

		private static bool CanAttack(Entity entity)
		{
			var maxAttacks = entity.GetTag(GameTag.MEGA_WINDFURY) > 0 ? 4
				: entity.GetTag(GameTag.WINDFURY) > 0 ? 2 : 1;
			return entity.IsInPlay && entity.Attack > 0
				&& entity.GetTag(GameTag.EXHAUSTED) == 0
				&& entity.GetTag(GameTag.FROZEN) == 0
				&& entity.GetTag(GameTag.CANT_ATTACK) == 0
				&& entity.GetTag(GameTag.NUM_ATTACKS_THIS_TURN) < maxAttacks;
		}

		private static CardKind Kind(CardType type)
		{
			switch(type)
			{
				case CardType.MINION: return CardKind.Minion;
				case CardType.SPELL: return CardKind.Spell;
				case CardType.WEAPON: return CardKind.Weapon;
				case CardType.LOCATION: return CardKind.Location;
				case CardType.HERO: return CardKind.Hero;
				default: return CardKind.Unknown;
			}
		}
	}
}
