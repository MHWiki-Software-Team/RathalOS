using Newtonsoft.Json;
using RathalOS.Infra;

internal class Program
{
	private static void Main()
	{
		try
		{
			new Utilities().Initialize().GetAwaiter().GetResult();
		}
		catch (Exception e)
		{
			Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
		}
	}
}