using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RathalOS.Data.Models
{
	public class WikiTask
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		public required string Title { get; set; }
		public DateTime TimeStamp { get; set; }
		public DateTime LastActive { get; set; }
		public DateTime LastUpdate { get; set; }
		public required ulong ChannelID { get; set; }
		public int CreatorId { get; set; }
		public required WikiUser Creator { get; set; }
		public virtual List<AssignedTask> Assigned { get; set; } = [];
		public required string Description { get; set; }
		public virtual List<WikiTaskUpdate> Updates { get; set; } = [];
		public required string TagsCSV { get; set; }
		public bool Archived { get; set; } = false;
		public bool Completed { get; set; } = false;
		public DateTime? CompletedOn { get; set; }
		public bool OnHold { get; set; } = false;
		[NotMapped]
		public bool Stale
		{
			get
			{
				return DateTime.UtcNow - LastActive > new TimeSpan(7, 0, 0, 0) && !Completed && !TagsCSV.Contains("Discussion");
			}
		}
		[NotMapped]
		public bool NeedsUpdate
		{
			get
			{
				return DateTime.UtcNow - LastUpdate > new TimeSpan(7, 0, 0, 0) && !Completed && !Stale && !TagsCSV.Contains("Discussion");
			}
		}
	}
}
