using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HdtArenaHelper
{
	public sealed class AdvisorResponse
	{
		public string Decision { get; }
		public double Confidence { get; }
		public IReadOnlyList<string> Steps { get; }
		public IReadOnlyList<string> Reasons { get; }
		public string Risk { get; }
		public bool IsLocalLethal { get; }

		public AdvisorResponse(string decision, double confidence, IReadOnlyList<string> steps,
			IReadOnlyList<string> reasons, string risk, bool isLocalLethal = false)
		{
			Decision = decision;
			Confidence = Math.Max(0, Math.Min(1, confidence));
			Steps = steps;
			Reasons = reasons;
			Risk = risk;
			IsLocalLethal = isLocalLethal;
		}

		internal static AdvisorResponse Parse(string json)
		{
			var root = JObject.Parse(json);
			return new AdvisorResponse(
				(string?)root["decision"] ?? "UNKNOWN",
				(double?)root["confidence"] ?? 0,
				root["steps"]?.ToObject<List<string>>() ?? new List<string>(),
				root["reason"]?.ToObject<List<string>>() ?? new List<string>(),
				(string?)root["risk"] ?? string.Empty);
		}

		internal static JObject OutputSchema => JObject.Parse(@"{
  'type':'object',
  'properties':{
    'decision':{'type':'string'},
    'confidence':{'type':'number','minimum':0,'maximum':1},
    'steps':{'type':'array','items':{'type':'string'}},
    'reason':{'type':'array','items':{'type':'string'}},
    'risk':{'type':'string'}
  },
  'required':['decision','confidence','steps','reason','risk'],
  'additionalProperties':false
}");
	}
}
