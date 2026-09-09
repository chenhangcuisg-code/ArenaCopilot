using System;
using System.IO;

namespace HdtArenaHelper
{
	internal sealed class PersonalStrategyNotes
	{
		internal string PersonalStrategy { get; }
		internal string Lessons { get; }

		internal PersonalStrategyNotes(string personalStrategy, string lessons)
		{
			PersonalStrategy = personalStrategy ?? string.Empty;
			Lessons = lessons ?? string.Empty;
		}
	}

	internal sealed class PersonalStrategyStore
	{
		private const int MaxSectionCharacters = 12000;
		private const string DefaultStrategy =
			"Play a balanced, tempo-conscious line. Check lethal and survival first; then prefer board initiative, efficient mana use, and flexible resources.";
		private const string EmptyLessons = "# Strategy lessons\n\nAdd one concrete correction per line after reviewing a game.";
		private readonly string _templateDirectory;

		internal string ProfilePath { get; }
		internal string LessonsPath { get; }

		internal PersonalStrategyStore(string dataDirectory, string templateDirectory)
		{
			if(string.IsNullOrWhiteSpace(dataDirectory))
				throw new ArgumentException("A personal-strategy directory is required.", nameof(dataDirectory));
			ProfilePath = Path.Combine(dataDirectory, "personal-strategy.md");
			LessonsPath = Path.Combine(dataDirectory, "strategy-lessons.md");
			_templateDirectory = templateDirectory ?? string.Empty;
		}

		internal PersonalStrategyNotes Read()
		{
			EnsureFiles();
			return new PersonalStrategyNotes(ReadBounded(ProfilePath, DefaultStrategy),
				ReadBounded(LessonsPath, EmptyLessons));
		}

		internal void EnsureFiles()
		{
			var directory = Path.GetDirectoryName(ProfilePath)!;
			Directory.CreateDirectory(directory);
			if(!File.Exists(ProfilePath))
			{
				var template = Path.Combine(_templateDirectory, "personal-strategy.template.md");
				File.WriteAllText(ProfilePath, File.Exists(template) ? File.ReadAllText(template) : DefaultStrategy);
			}
			if(!File.Exists(LessonsPath))
				File.WriteAllText(LessonsPath, EmptyLessons);
		}

		private static string ReadBounded(string path, string fallback)
		{
			try
			{
				var text = File.ReadAllText(path).Trim();
				if(text.Length == 0)
					return fallback;
				return text.Length <= MaxSectionCharacters ? text : text.Substring(0, MaxSectionCharacters);
			}
			catch(IOException)
			{
				return fallback;
			}
			catch(UnauthorizedAccessException)
			{
				return fallback;
			}
		}
	}
}
