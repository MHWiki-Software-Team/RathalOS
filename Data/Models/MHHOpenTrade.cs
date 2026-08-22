using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RathalOS.Data.Models
{
	public class MHHOpenTrade
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
		public WikiUser Executor { get; set; }
		public List<MHHCard> ExecutorOffer { get; set; } = [];
		public bool IsBuildingRecipientRequest { get; set; }
		public WikiUser Recipient { get; set; }
		public List<MHHCard> RecipientRequest { get; set; } = [];
		public DateTimeOffset Expires { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
	}
}
