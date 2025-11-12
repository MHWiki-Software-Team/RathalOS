using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GraviOS.Data
{
	public class AssignedTask
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		public WikiUser? Assignee { get; set; }
		public WikiTask? Assignment { get; set; }
	}
}
