using Discord;
using Newtonsoft.Json.Linq;
using System.Reflection;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace RathalOS.Infra
{
	public class MHWikiClient : IDisposable
	{
		private WikiClient _client { get; set; }

		public MHWikiClient()
		{
			_client = new()
			{
				ClientUserAgent = "RathalOS/" + Assembly.GetEntryAssembly()!.GetName().Version,
			};
		}

		public async Task<Tuple<int,bool>?> GetUserEdits(int userId)
		{
			WikiSite site = new(_client, "https://monsterhunterwiki.org/api.php");
			await site.Initialization;
			try
			{
				JToken userToken = await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new { action = "query", list = "users", ususerids = $"{userId}", usprop = "editcount|groups" }), new CancellationToken());
				userToken = userToken.Value<JToken>("query")!.Value<JArray>("users")!.First();
				return new(userToken.Value<int>("editcount"), userToken.Value<JArray>("groups")!.Any(x => x.Value<string>() == "bot"));
			}
			catch (WikiClientException ex)
			{
				Console.WriteLine(ex.Message);
				return null;
			}
		}

		public async Task<JToken?> GetWikiUsername(IGuildUser discordUser)
		{
			WikiSite site = new(_client, "https://monsterhunterwiki.org/api.php");
			await site.Initialization;
			try
			{
				return await GetWikiUsername($"{discordUser.Username}|{discordUser.GlobalName}|{discordUser.Nickname}|{discordUser.DisplayName}");
			}
			catch (WikiClientException ex)
			{
				Console.WriteLine(ex.Message);
				return null;
			}
		}

		public async Task<JToken?> GetWikiUsername(string ususers)
		{
			WikiSite site = new(_client, "https://monsterhunterwiki.org/api.php");
			await site.Initialization;
			try
			{
				return await site.InvokeMediaWikiApiAsync(new MediaWikiFormRequestMessage(new { action = "query", list = "users", ususers }), new CancellationToken());
			}
			catch (WikiClientException ex)
			{
				Console.WriteLine(ex.Message);
				return null;
			}
		}

		public void Dispose()
		{
			_client.Dispose();
		}
	}
}
