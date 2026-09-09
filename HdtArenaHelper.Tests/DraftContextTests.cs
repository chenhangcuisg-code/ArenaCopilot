using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace HdtArenaHelper.Tests
{
	public class DraftContextTests
	{
		[Fact]
		public void Projection_IncludesDuplicatesAndWholePackage()
		{
			var context = JObject.FromObject(DraftContext.Build("MAGE", new[] { 995, 995 }, new[]
			{
				new DraftAiOption { Id = "A", DbfId = 315, PackageDbfIds = new[] { 995, 995 } }
			}));
			Assert.Equal(2, context["deck"]!.Count());
			Assert.Equal(3, context["options"]![0]!["cards"]!.Count());
			Assert.Equal(5, (int)context["options"]![0]!["projectedCardCount"]!);
		}

		[Fact]
		public void UnknownCard_RemainsExplicitlyUnknownInsteadOfZeroCost()
		{
			var context = JObject.FromObject(DraftContext.Build("MAGE", Array.Empty<int>(), new[]
			{
				new DraftAiOption { Id = "A", DbfId = int.MaxValue, HasScoreData = false }
			}));
			var card = context["options"]![0]!["cards"]![0]!;
			Assert.False((bool)card["known"]!);
			Assert.Equal(JTokenType.Null, card["cost"]!.Type);
			Assert.False((bool)context["options"]![0]!["hasScoreData"]!);
		}
	}
}
