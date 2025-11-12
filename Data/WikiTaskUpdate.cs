using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GraviOS.Data
{
	public class WikiTaskUpdate
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		public DateTime TimeStamp { get; set; }
		public WikiUser? Creator { get; set; }
		public required string Update { get; set; }
		public WikiTask? Task { get; set; }
	}
}
