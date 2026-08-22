using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace RathalOS.Data.Models
{
	public class MHHEnvironmentVariables
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }
		public int TotalPulls { get; set; }
		public DateTimeOffset LastHolo { get; set; }
		public DateTimeOffset LastSpecial { get; set; }
		public DateTimeOffset LastRare { get; set; }
		public SpecialEditions CurrentSpecialEdition { get; set; } = SpecialEditions.Metal;
		public Events CurrentEvent { get; set; } = Events.None;
		public int Monkeys { get; set; }
	}

	public enum Events
	{
		[Description("None")]
		None,
		[Description("Double Rare Card Chance")]
		DoubleRare,
		[Description("Double Special Edition Card Chance")]
		DoubleSpecial,
		[Description("Double Holo Card Chance")]
		DoubleHolo,
		[Description("Double Pull Gain")]
		DoublePull,
		[Description("Double Booster Gain")]
		DoubleBooster
	}

	public enum SpecialEditions
	{
		[Description("Metal")]
		Metal,
		[Description("Crystal")]
		Crystal
	}

	public static class EnumExtensions
	{
		public static string? GetDescription(this Enum value)
		{
			Type type = value.GetType();
			string? name = Enum.GetName(type, value);
			if (name != null)
			{
				FieldInfo? field = type.GetField(name);
				if (field != null)
				{
					if (Attribute.GetCustomAttribute(field,
							 typeof(DescriptionAttribute)) is DescriptionAttribute attr)
					{
						return attr.Description;
					}
				}
			}
			return null;
		}
	}
}
