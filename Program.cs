using RathalOS.Infra;

internal class Program
{
	private static void Main(string[] args) => new Utilities().Initialize().GetAwaiter().GetResult();
}