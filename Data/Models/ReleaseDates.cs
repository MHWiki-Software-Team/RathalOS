using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RathalOS.Data.Models
{
	public class ReleaseDates
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		public required string Tag { get; set; }
		public required DateTime ReleaseDate { get; set; }
		public bool HasNotified { get; set; }
	}
}
