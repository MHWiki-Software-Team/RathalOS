using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;

namespace RathalOS.Infra
{
	public class Startup
	{
		private static DiscordSocketClient? _client;
		private static ServiceProvider? _services;

		public async Task Initialize()
		{
			await using var services = ConfigureServices();
			_services = services;
			DiscordSocketConfig socketConfig = new()
			{
				GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.GuildPresences | GatewayIntents.MessageContent | GatewayIntents.Guilds | GatewayIntents.GuildMessages
			};
			_client = new DiscordSocketClient(socketConfig);
			_client.Ready += OnReady;
			_client.Log += Client_Log;
			services.GetRequiredService<CommandService>().Log += Client_Log;
			await _client.LoginAsync(TokenType.Bot, ConfigurationManager.AppSettings.Get("Token"));
			await _client.StartAsync();
			await Task.Delay(Timeout.Infinite);
		}

		private async Task OnReady()
		{
			try
			{
				Console.WriteLine($"Connected to these servers as '{_client!.CurrentUser.Username}': ");
				foreach (var guild in _client.Guilds)
					Console.WriteLine($"- {guild.Name}");
				await _client.SetGameAsync("The Guild authorizes you to hunt that incomplete wiki article!",
					type: ActivityType.CustomStatus);
				Console.WriteLine($"Activity set to '{_client.Activity.Name}'");
				_services!.GetRequiredService<InteractionEngine>().Initialize(_client);
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
		}

		public static async Task<bool> ChannelExists(ulong channelId)
		{
			IChannel res = await _client!.GetChannelAsync(channelId);
			return res != null;
		}

		private static async Task Client_Log(LogMessage arg)
		{
			await Task.Run(() =>
			{
				Console.WriteLine(arg.Message);
			});
		}

		private static ServiceProvider ConfigureServices()
		{
			return new ServiceCollection()
				.AddSingleton<DiscordSocketClient>()
				.AddSingleton<CommandService>()
				.AddSingleton<InteractionEngine>()
				.BuildServiceProvider();
		}
	}
}
