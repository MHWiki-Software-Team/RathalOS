using Newtonsoft.Json;
using RathalOS.Infra;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RathalOS.Data.Models
{
	public class WikiUser
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		public required string Username { get; set; }
		public required ulong UserID { get; set; }
		public string WikiUsername { get; set; } = string.Empty;
		public int? WikiUserId { get; set; }
		public int LastEditCount { get; set; } = -1;
		public int Pulls { get; set; }
		public int LifetimePulls { get; set; }
		public int Boosters { get; set; }
		public int LifetimeBoosters { get; set; }
		public virtual List<WikiTask>? CreatedTasks { get; set; } = [];
		public virtual List<AssignedTask>? UserAssignments { get; set; } = [];
		public virtual List<WikiTaskUpdate>? Updates { get; set; } = [];
		public string FavoriteCardJson { get; set; } = "[]";
		public string TradeInventoryJson { get; set; } = "[]";
		public string RecyclingBinJson { get; set; } = "[]";
		public virtual List<MHHCard>? Cards { get; set; } = [];
	}

	public static class WikiUserExtensions
	{
		public static async Task CashInPulls(this WikiUser user)
		{
			using (MHWikiClient client = new())
			{
				Tuple<int, bool>? editInfo = await client.GetUserEdits(user.WikiUserId!.Value);
				if (editInfo != null)
				{
					int pullsToAdd = 0;
					bool isInDict = false;
					if (editInfo.Item2 && user.LastEditCount < 0)
					{
						//User is a bot user, catching up
						Dictionary<string, int> botUserVals = new Dictionary<string, int>()
						{
							{ "rampagerobot", 100 }
						};
						if (botUserVals.ContainsKey(user.Username.ToLower()))
						{
							pullsToAdd = botUserVals[user.Username.ToLower()];
							isInDict = true;
						}
					}
					if (!isInDict)
					{
						bool shouldCap = false;
						if (user.LastEditCount < 0)
						{
							shouldCap = true;
							user.LastEditCount = 0;
						}
						pullsToAdd = Convert.ToInt32(Math.Floor((editInfo.Item1 - user.LastEditCount) / 10f));
						if (shouldCap && pullsToAdd > 100)
						{
							pullsToAdd = 100;
						}
					}
					user.Pulls += pullsToAdd;
					user.LastEditCount = editInfo.Item1;
				}
			}
		}
	}
}
