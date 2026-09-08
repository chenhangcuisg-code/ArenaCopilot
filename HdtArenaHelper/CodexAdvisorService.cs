using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HdtArenaHelper
{
	internal sealed class CodexAdvisorService : IDisposable
	{
		private readonly CodexAppServerClient _client;
		private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _threads
			= new ConcurrentDictionary<string, Lazy<Task<string>>>();
		private readonly string? _model;
		private readonly string _effort;

		internal CodexAdvisorService(string strategyDirectory, string? model = null, string effort = "medium")
		{
			_client = new CodexAppServerClient(strategyDirectory);
			_model = model;
			_effort = effort;
		}

		internal Task<CodexAccount?> ReadAccountAsync() => _client.ReadAccountAsync();

		internal async Task BeginLoginAsync()
		{
			var url = await _client.StartChatGptLoginAsync().ConfigureAwait(false);
			if(!string.IsNullOrWhiteSpace(url))
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}

		internal async Task<AdvisorResponse> AdvisePlayAsync(GameSnapshot snapshot,
			IReadOnlyList<LegalAction> actions)
		{
			var thread = await ThreadAsync("game:" + snapshot.GameId).ConfigureAwait(false);
			var json = await _client.RunTurnAsync(thread, PromptBuilder.Play(snapshot, actions), _effort)
				.ConfigureAwait(false);
			return AdvisorResponse.Parse(json);
		}

		internal async Task<AdvisorResponse> AdviseDraftAsync(string runKey, string deckClass,
			IReadOnlyList<int> deck, IReadOnlyList<DraftAiOption> options)
		{
			var thread = await ThreadAsync("draft:" + runKey).ConfigureAwait(false);
			var json = await _client.RunTurnAsync(thread, PromptBuilder.Draft(deckClass, deck, options), _effort)
				.ConfigureAwait(false);
			return AdvisorResponse.Parse(json);
		}

		internal async Task<AdvisorResponse> AdviseChoiceAsync(string gameId, string choiceKind,
			GameSnapshot snapshot, IReadOnlyList<DraftAiOption> options)
		{
			var thread = await ThreadAsync("game:" + gameId).ConfigureAwait(false);
			var json = await _client.RunTurnAsync(thread, PromptBuilder.Choice(choiceKind, snapshot, options), _effort)
				.ConfigureAwait(false);
			return AdvisorResponse.Parse(json);
		}

		internal async Task<AdvisorResponse> AdviseMulliganAsync(string gameKey, string deckClass,
			bool onCoin, IReadOnlyList<int> deck, IReadOnlyList<MulliganAiOption> hand)
		{
			var thread = await ThreadAsync("game:" + gameKey).ConfigureAwait(false);
			var json = await _client.RunTurnAsync(thread, PromptBuilder.Mulligan(deckClass, onCoin, deck, hand), _effort)
				.ConfigureAwait(false);
			return AdvisorResponse.Parse(json);
		}

		private async Task<string> ThreadAsync(string key)
		{
			var lazy = _threads.GetOrAdd(key,
				_ => new Lazy<Task<string>>(() => _client.StartThreadAsync(_model)));
			try
			{
				return await lazy.Value.ConfigureAwait(false);
			}
			catch
			{
				_threads.TryRemove(key, out _);
				throw;
			}
		}

		public void Dispose() => _client.Dispose();
	}
}
