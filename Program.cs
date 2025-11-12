using RathalOS.Infra;

internal class Program
{
	private static void Main(string[] args) => new Startup().Initialize().GetAwaiter().GetResult();
}