using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RathalOS.Data.Models
{
	public class MHHCardStorage
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		public int Series { get; set; }
		public int CardNumber { get; set; }
		public CardDeco Decoration { get; set; }
		public byte[] StoredCard { get; set; } = [];
	}
}
