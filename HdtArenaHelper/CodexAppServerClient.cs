using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HdtArenaHelper
{
	internal sealed class CodexAccount
	{
		public string Type { get; set; } = string.Empty;
		public string PlanType { get; set; } = string.Empty;
	}

	internal sealed class CodexAppServerClient : IDisposable
	{
		private readonly string _workingDirectory;
		private readonly SemaphoreSlim _turnLock = new SemaphoreSlim(1, 1);
		private readonly ConcurrentDictionary<long, TaskCompletionSource<JObject>> _requests
			= new ConcurrentDictionary<long, TaskCompletionSource<JObject>>();
		private readonly object _writeLock = new object();
		private Process? _process;
		private StreamWriter? _input;
		private long _nextId;
		private string? _activeAgentMessage;
		private TaskCompletionSource<string>? _activeTurn;
		private string? _lastError;
		private bool _disposed;

		internal CodexAppServerClient(string workingDirectory)
		{
			_workingDirectory = workingDirectory;
		}

		internal async Task StartAsync()
		{
			if(_process != null && !_process.HasExited)
				return;

			var nativeCli = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				"npm", "node_modules", "@openai", "codex", "node_modules", "@openai",
				"codex-win32-x64", "vendor", "x86_64-pc-windows-msvc", "bin", "codex.exe");
			var useNativeCli = File.Exists(nativeCli);
			var fileCredentials = File.Exists(Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "auth.json"));
			var start = new ProcessStartInfo
			{
				FileName = useNativeCli ? nativeCli : "cmd.exe",
				Arguments = useNativeCli
					? (fileCredentials ? "-c cli_auth_credentials_store=file app-server" : "app-server")
					: "/d /s /c \"codex app-server\"",
				WorkingDirectory = _workingDirectory,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
				StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
			};
			_process = Process.Start(start) ?? throw new InvalidOperationException("Could not start codex app-server.");
			// .NET Framework's redirected stdin writer emits a UTF-8 BOM. The app-server
			// treats that BOM as part of the first NDJSON message, so terminate that
			// preamble as an intentionally empty line before sending initialize.
			_input = _process.StandardInput;
			_input.WriteLine();
			_input.Flush();
			_ = Task.Run(() => ReadLoop(_process.StandardOutput));
			_ = Task.Run(() => DrainErrors(_process.StandardError));

			await RequestAsync("initialize", new
			{
				clientInfo = new { name = "arena_copilot_hdt", title = "Arena Copilot for HDT", version = "0.1.0" },
				capabilities = new { optOutNotificationMethods = new[] { "item/agentMessage/delta" } },
			}).ConfigureAwait(false);
			Notify("initialized", new { });
		}

		internal async Task<CodexAccount?> ReadAccountAsync()
		{
			await StartAsync().ConfigureAwait(false);
			var response = await RequestAsync("account/read", new { refreshToken = false }).ConfigureAwait(false);
			var account = response["result"]?["account"];
			return account == null || account.Type == JTokenType.Null
				? null
				: new CodexAccount
				{
					Type = (string?)account["type"] ?? string.Empty,
					PlanType = (string?)account["planType"] ?? string.Empty,
				};
		}

		internal async Task<string> StartChatGptLoginAsync()
		{
			await StartAsync().ConfigureAwait(false);
			var response = await RequestAsync("account/login/start", new
			{
				type = "chatgpt",
				useHostedLoginSuccessPage = true,
				appBrand = "chatgpt",
			}).ConfigureAwait(false);
			return (string?)response["result"]?["authUrl"] ?? string.Empty;
		}

		internal async Task<string> StartThreadAsync(string? model)
		{
			await StartAsync().ConfigureAwait(false);
			var parameters = new JObject
			{
				["cwd"] = _workingDirectory,
				["approvalPolicy"] = "never",
				["sandbox"] = "read-only",
				["serviceName"] = "arena_copilot_hdt",
				["ephemeral"] = true,
			};
			if(!string.IsNullOrWhiteSpace(model))
				parameters["model"] = model;
			var response = await RequestAsync("thread/start", parameters).ConfigureAwait(false);
			return (string?)response["result"]?["thread"]?["id"]
				?? throw new InvalidDataException("Codex did not return a thread id.");
		}

		internal async Task<string> RunTurnAsync(string threadId, string prompt, string effort)
		{
			await _turnLock.WaitAsync().ConfigureAwait(false);
			try
			{
				_activeAgentMessage = null;
				_activeTurn = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
				var parameters = new JObject
				{
					["threadId"] = threadId,
					["input"] = JArray.FromObject(new[] { new { type = "text", text = prompt } }),
					["effort"] = string.IsNullOrWhiteSpace(effort) ? "medium" : effort,
					["approvalPolicy"] = "never",
					["sandboxPolicy"] = JObject.FromObject(new { type = "readOnly" }),
					["outputSchema"] = AdvisorResponse.OutputSchema,
				};
				await RequestAsync("turn/start", parameters).ConfigureAwait(false);
				var finished = await Task.WhenAny(_activeTurn.Task, Task.Delay(TimeSpan.FromMinutes(3)))
					.ConfigureAwait(false);
				if(finished != _activeTurn.Task)
					throw new TimeoutException("Codex advice timed out.");
				return await _activeTurn.Task.ConfigureAwait(false);
			}
			finally
			{
				_activeTurn = null;
				_turnLock.Release();
			}
		}

		private async Task<JObject> RequestAsync(string method, object parameters)
		{
			if(_disposed)
				throw new ObjectDisposedException(nameof(CodexAppServerClient));
			var id = Interlocked.Increment(ref _nextId);
			var completion = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
			_requests[id] = completion;
			Write(new JObject { ["method"] = method, ["id"] = id, ["params"] = JObject.FromObject(parameters) });
			var finished = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(30)))
				.ConfigureAwait(false);
			if(finished == completion.Task)
				return await completion.Task.ConfigureAwait(false);
			_requests.TryRemove(id, out _);
			throw new TimeoutException($"Codex request '{method}' timed out. {_lastError}".Trim());
		}

		private void Notify(string method, object parameters)
			=> Write(new JObject { ["method"] = method, ["params"] = JObject.FromObject(parameters) });

		private void Write(JObject message)
		{
			lock(_writeLock)
			{
				if(_input == null)
					throw new InvalidOperationException("Codex app-server is not running.");
				_input.WriteLine(message.ToString(Formatting.None));
				_input.Flush();
			}
		}

		private async Task ReadLoop(StreamReader output)
		{
			try
			{
				string? line;
				while((line = await output.ReadLineAsync().ConfigureAwait(false)) != null)
				{
					JObject message;
					try { message = JObject.Parse(line); }
					catch(JsonException) { continue; }

					var id = (long?)message["id"];
					if(id.HasValue && message["method"] == null && _requests.TryRemove(id.Value, out var request))
					{
						if(message["error"] != null)
							request.TrySetException(new InvalidOperationException((string?)message["error"]?["message"] ?? "Codex request failed."));
						else
							request.TrySetResult(message);
						continue;
					}

					var method = (string?)message["method"];
					if(id.HasValue && method != null)
					{
						Write(new JObject { ["id"] = id.Value, ["result"] = new JObject { ["decision"] = "decline" } });
						continue;
					}
					if(method == "item/completed" && (string?)message["params"]?["item"]?["type"] == "agentMessage")
						_activeAgentMessage = (string?)message["params"]?["item"]?["text"];
					if(method == "turn/completed")
					{
						var status = (string?)message["params"]?["turn"]?["status"];
						if(status == "completed" && !string.IsNullOrWhiteSpace(_activeAgentMessage))
							_activeTurn?.TrySetResult(_activeAgentMessage!);
						else
							_activeTurn?.TrySetException(new InvalidOperationException(
								(string?)message["params"]?["turn"]?["error"]?["message"] ?? "Codex turn did not complete."));
					}
				}
				FailAll(new EndOfStreamException("Codex app-server stopped."));
			}
			catch(Exception ex)
			{
				FailAll(ex);
			}
		}

		private async Task DrainErrors(StreamReader errors)
		{
			string? line;
			while((line = await errors.ReadLineAsync().ConfigureAwait(false)) != null)
			{
				if(line.Contains("Failed to deserialize JSONRPCMessage: expected value at line 1 column 1"))
					continue;
				_lastError = line;
			}
		}

		private void FailAll(Exception ex)
		{
			foreach(var request in _requests.Values)
				request.TrySetException(ex);
			_requests.Clear();
			_activeTurn?.TrySetException(ex);
		}

		public void Dispose()
		{
			if(_disposed)
				return;
			_disposed = true;
			FailAll(new ObjectDisposedException(nameof(CodexAppServerClient)));
			try
			{
				if(_process != null && !_process.HasExited)
					_process.Kill();
			}
			catch(InvalidOperationException ex)
			{
				Debug.WriteLine(ex.Message);
			}
			_process?.Dispose();
			_turnLock.Dispose();
		}
	}
}
