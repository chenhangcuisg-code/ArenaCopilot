using System.Collections.Generic;

namespace HdtArenaHelper
{
	public enum CardKind
	{
		Unknown,
		Minion,
		Spell,
		Weapon,
		Location,
		Hero,
	}

	public sealed class CardSnapshot
	{
		public int EntityId { get; set; }
		public string CardId { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public int Cost { get; set; }
		public CardKind Kind { get; set; }
		public string Text { get; set; } = string.Empty;
	}

	public sealed class MinionSnapshot
	{
		public int EntityId { get; set; }
		public string CardId { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public int Attack { get; set; }
		public int Health { get; set; }
		public bool CanAttack { get; set; }
		public int AttacksRemaining { get; set; }
		public bool HasTaunt { get; set; }
		public bool HasDivineShield { get; set; }
		public bool IsFrozen { get; set; }
	}

	public sealed class PlayerSnapshot
	{
		public int HeroEntityId { get; set; }
		public string Class { get; set; } = string.Empty;
		public int Health { get; set; }
		public int Armor { get; set; }
		public int HeroAttack { get; set; }
		public bool CanAttack { get; set; }
		public int AttacksRemaining { get; set; }
		public bool IsImmune { get; set; }
		public int HeroPowerEntityId { get; set; }
		public int HeroPowerCost { get; set; }
		public bool HeroPowerAvailable { get; set; }
		public int HandSize { get; set; }
		public int DeckSize { get; set; }
		public int SecretCount { get; set; }
	}

	public sealed class GameSnapshot
	{
		public string GameId { get; set; } = string.Empty;
		public int Turn { get; set; }
		public bool IsMyTurn { get; set; }
		public int ManaAvailable { get; set; }
		public int ManaMaximum { get; set; }
		public PlayerSnapshot Friendly { get; set; } = new PlayerSnapshot();
		public PlayerSnapshot Opponent { get; set; } = new PlayerSnapshot();
		public IReadOnlyList<CardSnapshot> Hand { get; set; } = new List<CardSnapshot>();
		public IReadOnlyList<MinionSnapshot> FriendlyBoard { get; set; } = new List<MinionSnapshot>();
		public IReadOnlyList<MinionSnapshot> EnemyBoard { get; set; } = new List<MinionSnapshot>();
		public IReadOnlyList<string> ArenaDeck { get; set; } = new List<string>();
		public IReadOnlyList<string> CardsPlayedByOpponent { get; set; } = new List<string>();
	}
}
