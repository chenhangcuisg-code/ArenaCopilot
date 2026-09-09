using System;
using System.IO;

namespace HdtArenaHelper
{
	internal static class CodexExecutableResolver
	{
		internal static string? Find(string roamingAppData, string localAppData, Func<string, bool> exists)
		{
			if(exists == null)
				throw new ArgumentNullException(nameof(exists));

			var candidates = new[]
			{
				Path.Combine(localAppData, "Programs", "OpenAI", "Codex", "bin", "codex.exe"),
				Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "codex.exe"),
				Path.Combine(roamingAppData, "npm", "node_modules", "@openai", "codex", "node_modules",
					"@openai", "codex-win32-x64", "vendor", "x86_64-pc-windows-msvc", "bin", "codex.exe"),
			};
			foreach(var candidate in candidates)
			{
				if(exists(candidate))
					return candidate;
			}
			return null;
		}
	}
}
