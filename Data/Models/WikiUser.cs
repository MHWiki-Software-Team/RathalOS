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
		public virtual List<WikiTask>? CreatedTasks { get; set; } = [];
		public virtual List<AssignedTask>? UserAssignments { get; set; } = [];
		public virtual List<WikiTaskUpdate>? Updates { get; set; } = [];
	}
}
