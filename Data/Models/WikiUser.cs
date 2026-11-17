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
		public int Pulls { get; set; }
		public int LifetimePulls { get; set; }
		public int Boosters { get; set; }
		public int LifetimeBoosters { get; set; }
		public string FavoriteCardJson { get; set; } = "[]";
		public string TradeInventoryJson { get; set; } = "[]";
		public string RecyclingBinJson { get; set; } = "[]";
		public virtual List<MHHCard>? Cards { get; set; } = [];
		public int GrantPulls { get; set; } = 5;
		public DateTime LastActiveCheck { get; set; } = DateTime.MinValue;
	}

	public static class WikiUserExtensions
	{
		public static async Task<int?> CashInPulls(this WikiUser user)
		{
			if (DateTime.Now - user.LastActiveCheck >= TimeSpan.FromDays(28))
			{
				using (MHWikiClient client = new())
				{
					return await client.GetUserEdits(user.WikiUserId!.Value);
				}
			}
			return null;
		}
	}
}
