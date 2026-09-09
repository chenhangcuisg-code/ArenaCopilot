using System.Reflection;
using Xunit;

namespace HdtArenaHelper.Tests
{
	public class AdvisorVisibilityRegressionTests
	{
		[Theory]
		[InlineData(HearthDb.Enums.GameType.GT_VS_AI, true, true)]
		[InlineData(HearthDb.Enums.GameType.GT_VS_AI, false, false)]
		[InlineData(HearthDb.Enums.GameType.GT_BATTLEGROUNDS, true, false)]
		[InlineData(HearthDb.Enums.GameType.GT_ARENA, true, true)]
		public void PracticeOptIn_PreservesOtherModeGates(HearthDb.Enums.GameType gameType, bool allowPractice, bool expected)
		{
			Assert.Equal(expected, GameWatcher.IsSupportedMatch((int)gameType, allowPractice));
		}

		[Fact]
		public void InactiveDiscoverWatcher_DoesNotClearOtherAdvice()
		{
			var watcher = new CardChoiceWatcher();
			var notifications = 0;
			watcher.OnChoicesGone += (_, __) => notifications++;
			var leave = typeof(CardChoiceWatcher).GetMethod("OnSceneLeft", BindingFlags.Instance | BindingFlags.NonPublic)!;
			leave.Invoke(watcher, null);
			leave.Invoke(watcher, null);
			Assert.Equal(0, notifications);
		}
	}
}
