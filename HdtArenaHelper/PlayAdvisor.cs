using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HdtArenaHelper
{
	internal sealed class PlayAdviceEventArgs : EventArgs
	{
		public AdvisorResponse Advice { get; }
		public string StateHash { get; }

		internal PlayAdviceEventArgs(AdvisorResponse advice, string stateHash)
		{
			Advice = advice;
			StateHash = stateHash;
		}
	}

	internal sealed class PlayAdvisor
	{
		private static readonly TimeSpan StableDelay = TimeSpan.FromMilliseconds(900);
		private readonly CodexAdvisorService _codex;
		private string? _observedHash;
		private DateTime _stableSinceUtc;
		private string? _requestedHash;
		private Task<AdvisorResponse>? _request;
		private string? _requestHash;
		private string? _gameId;
		private IReadOnlyList<int> _arenaDeck = new List<int>();

		internal event EventHandler<PlayAdviceEventArgs>? AdviceReady;
		internal event EventHandler? AdviceGone;

		internal PlayAdvisor(CodexAdvisorService codex)
		{
			_codex = codex;
		}

		internal void Poll(bool enabled)
		{
			if(_request != null && _request.IsCompleted)
				CompleteRequest();

			if(!enabled || !TryRead(out var snapshot) || !snapshot.IsMyTurn)
			{
				ClearObserved();
				return;
			}

			if(_gameId != snapshot.GameId)
			{
				_gameId = snapshot.GameId;
				_requestedHash = null;
				AdviceGone?.Invoke(this, EventArgs.Empty);
			}

			var hash = StateHasher.Compute(snapshot);
			if(hash != _observedHash)
			{
				_observedHash = hash;
				_stableSinceUtc = DateTime.UtcNow;
				if(_requestedHash != null && _requestedHash != hash)
					AdviceGone?.Invoke(this, EventArgs.Empty);
				return;
			}
			if(_request != null || _requestedHash == hash || DateTime.UtcNow - _stableSinceUtc < StableDelay)
				return;

			var lethal = LethalCalculator.FindGuaranteedLethal(snapshot);
			if(lethal != null)
			{
				_requestedHash = hash;
				AdviceReady?.Invoke(this, new PlayAdviceEventArgs(new AdvisorResponse(
					"LETHAL", 1, lethal.Steps, new[] { $"确定伤害 {lethal.Damage}" }, "无随机性", true), hash));
				return;
			}

			_requestedHash = hash;
			_requestHash = hash;
			_request = _codex.AdvisePlayAsync(snapshot, LegalActionGenerator.Generate(snapshot));
		}

		internal void SetDeck(IReadOnlyList<int> deck)
		{
			_arenaDeck = deck?.ToList() ?? new List<int>();
		}

		internal void Reset()
		{
			_observedHash = null;
			_requestedHash = null;
			_request = null;
			_requestHash = null;
			_gameId = null;
		}

		private bool TryRead(out GameSnapshot snapshot)
		{
			try
			{
				var built = SnapshotBuilder.TryBuild();
				if(built != null)
				{
					built.ArenaDeck = _arenaDeck.Select(DeckCardName).ToList();
					snapshot = built;
					return true;
				}
			}
			catch(Exception ex)
			{
				Hearthstone_Deck_Tracker.Utility.Logging.Log.Info(
					"[ArenaHelper] play snapshot unavailable: " + ex.Message);
			}
			snapshot = new GameSnapshot();
			return false;
		}

		private static string DeckCardName(int dbfId)
		{
			var card = HearthDb.Cards.GetFromDbfId(dbfId);
			return card?.Name ?? dbfId.ToString();
		}

		private void CompleteRequest()
		{
			var request = _request!;
			var hash = _requestHash!;
			_request = null;
			_requestHash = null;
			if(request.Status == TaskStatus.RanToCompletion && _observedHash == hash)
				AdviceReady?.Invoke(this, new PlayAdviceEventArgs(request.Result, hash));
			else if(request.IsFaulted)
				Hearthstone_Deck_Tracker.Utility.Logging.Log.Info(
					"[ArenaHelper] Codex advice failed: " + request.Exception?.GetBaseException().Message);
		}

		private void ClearObserved()
		{
			if(_observedHash != null)
				AdviceGone?.Invoke(this, EventArgs.Empty);
			_observedHash = null;
			_requestedHash = null;
		}
	}
}
