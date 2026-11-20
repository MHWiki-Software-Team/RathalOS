using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz;
using Quartz.Impl;
using Quartz.Logging;
using RathalOS.Data.Context;
using RathalOS.Data.Models;
using System.Configuration;
using System.Diagnostics;
using System.Text;

namespace RathalOS.Infra
{
	public class Utilities
	{
		public static string CardStoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cards");
		public static SocketForumChannel? Forum { get; set; }
		public static IUser? Owner { get; set; }

		private ulong _validThread;
#pragma warning disable IDE0044 // Add readonly modifier
		private object _lock = new();
#pragma warning restore IDE0044 // Add readonly modifier
		private static DiscordSocketClient? _client;
		private static ServiceProvider? _services;
		private static InteractionService? _interactionService;
		private static IScheduler? _scheduler;
		private static IReadOnlyCollection<IApplicationCommand> _cmds = [];
		private static ulong? _cmdId = null;
		private IMemoryCache? _cache;

		public static IGuildUser ToIGuildUser(IUser user)
		{
			SocketGuild mainGuild = _client!.Guilds.First(x => x.Id == Convert.ToUInt64(ConfigurationManager.AppSettings.Get("ServerID")));
			return mainGuild.GetUser(user.Id);
		}

		public static ulong GetCommandId(string commandName)
		{
			return _cmds.First(x => x.Name == commandName).Id;
		}

		public async Task Initialize()
		{
			await using var services = ConfigureServices();
			_services = services;
			_cache = services.GetRequiredService<IMemoryCache>();
			DiscordSocketConfig socketConfig = new()
			{
				GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.GuildPresences | GatewayIntents.MessageContent | GatewayIntents.Guilds | GatewayIntents.GuildMessages
			};
			_client = new DiscordSocketClient(socketConfig);
			_client.Ready += OnReady;
			_client.Log += Client_Log;
			_interactionService = new InteractionService(_client.Rest);
			_interactionService.Log += Client_Log;
			services.GetRequiredService<CommandService>().Log += Client_Log;
			await _client.LoginAsync(TokenType.Bot, ConfigurationManager.AppSettings.Get("Token"));
			await _client.StartAsync();
			await Task.Delay(Timeout.Infinite);
			await _scheduler!.Shutdown();
		}

		private async Task OnReady()
		{
			try
			{
				Console.WriteLine($"Connected to these servers as '{_client!.CurrentUser.Username}': ");
				foreach (var guild in _client.Guilds)
					Console.WriteLine($"- {guild.Name}");
				await _interactionService!.AddModuleAsync(typeof(InteractionEngine), _services!);
				await _interactionService!.RegisterCommandsToGuildAsync(Convert.ToUInt64(ConfigurationManager.AppSettings.Get("ServerID")));
				_client.InteractionCreated += async (x) =>
				{
					SocketInteractionContext ctx = new(_client, x);
					await _interactionService.ExecuteCommandAsync(ctx, _services);
				};
				_client.AutocompleteExecuted += async arg =>
				{
					InteractionContext ctx = new(_client, arg, arg.Channel);
					await _interactionService!.ExecuteCommandAsync(ctx, services: _services);
				};
				SocketGuild mainGuild = _client.Guilds.First(x => x.Id == Convert.ToUInt64(ConfigurationManager.AppSettings.Get("ServerID")));
				Forum = mainGuild.ForumChannels.First(x => x.Id == Convert.ToUInt64(ConfigurationManager.AppSettings.Get("ForumID")));
				_validThread = Forum.Id;
				_client.InteractionCreated += Client_InteractionCreated;
				List<Task> allCards = new List<Task>();
				using (Wiki_DbContext ctxt = new())
				{
					ctxt.Database.Migrate();
					await Wiki_DbContext.GetEnvironmentVariables();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
					for (int series = 1; series <= 8; series++)
					{
						int[] seriesCnts = [];
						//starters have a weird border and no rarities
						bool isBase = true;// _rand.Next(0, 2) == 0;
						if (isBase)
						{
							seriesCnts = [90, 77, 77, 77, 90, 77, 75, 75];
						}
						else
						{
							seriesCnts = [17, 24, 22, 3, 27, 28, 24, 26];
						}
						for (int cardNumber = 1; cardNumber <= seriesCnts[series - 1]; cardNumber++)
						{
							StoreRootCard(series, cardNumber);
							Console.WriteLine($"Stored or read B{series:D2} {cardNumber}/{seriesCnts[series - 1]}");
						}
					}
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
				}
				Owner = mainGuild.Users.First(x => x.Username.Equals(ConfigurationManager.AppSettings.Get("BotOwner"), StringComparison.CurrentCultureIgnoreCase));
				StdSchedulerFactory factory = new();
				_scheduler = await factory.GetScheduler();
				await _scheduler.Start();
				IJobDetail dailyTasks = JobBuilder.Create<DailyTasksJob>()
					.WithIdentity("dailyTasks")
					.Build();
				//Every day at noon ET, 11 AM CT
				ITrigger trigger = TriggerBuilder.Create()
					.WithIdentity("trigger")
					.StartAt(DateTime.Now.Date.AddHours(11))
					.WithSimpleSchedule(x => x
						.WithIntervalInHours(24)
						.RepeatForever())
					.Build();
				await _scheduler.ScheduleJob(dailyTasks, trigger);
				IJobDetail hourlyTasks = JobBuilder.Create<HourlyTasksJob>()
					.WithIdentity("hourlyTasks")
					.Build();
				//Every hour
				ITrigger trigger2 = TriggerBuilder.Create()
					.WithIdentity("trigger2")
					.WithSimpleSchedule(x => x
						.WithIntervalInHours(1)
						.RepeatForever())
					.Build();
				await _scheduler.ScheduleJob(hourlyTasks, trigger2);
				LogProvider.SetCurrentLogProvider(new ConsoleLogProvider());
				_cmds = await mainGuild.GetApplicationCommandsAsync();
				MHHEnvironmentVariables dbVars = await Wiki_DbContext.GetEnvironmentVariables();
				await SetMonkeys(dbVars.Monkeys);
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		public static async Task<MHHCardStorage> FetchRootCard(int series, int cardNumber)
		{
			string seriesPath = Path.Combine(CardStoragePath, series.ToString());
			if (!Directory.Exists(seriesPath))
			{
				Directory.CreateDirectory(seriesPath);
			}
			string cardPath = Path.Combine(seriesPath, cardNumber + ".json");
			string fileText = await File.ReadAllTextAsync(cardPath);
			return JsonConvert.DeserializeObject<MHHCardStorage>(fileText)!;
		}

		public static async Task StoreRootCard(int series, int cardNumber)
		{
			string seriesPath = Path.Combine(CardStoragePath, series.ToString());
			if (!Directory.Exists(seriesPath))
			{
				Directory.CreateDirectory(seriesPath);
			}
			string cardPath = Path.Combine(seriesPath, cardNumber + ".json");
			if (!File.Exists(cardPath))
			{
				MHHCardPackage pkg = await MHHCardPackage.BuildCardPackage(true, series, cardNumber);
				await File.WriteAllTextAsync(cardPath, JsonConvert.SerializeObject(new MHHCardStorage()
				{
					CardNumber = cardNumber,
					Series = series,
					StoredCard = pkg.CardBytes
				}));
			}
		}

		public static async Task SetMonkeys(int monkeys)
		{
			await _client!.SetGameAsync($"Now up to {monkeys} monkeys btw",
					type: ActivityType.CustomStatus);
			Console.WriteLine($"Activity set to '{_client.Activity.Name}'");
		}

		private async Task Client_InteractionCreated(SocketInteraction arg)
		{
			try
			{
				switch (arg)
				{
					case SocketMessageComponent component:
						switch (component.Data.CustomId)
						{
							case string btnId when btnId.StartsWith("DismissTrade"):
								{
									if (InteractionEngine.PaginationPages.ContainsKey(component.Message.Id))
									{
										InteractionEngine.PaginationPages.Remove(component.Message.Id);
									}
									if (InteractionEngine.CardListPagination.ContainsKey(component.Message.Id))
									{
										InteractionEngine.CardListPagination.Remove(component.Message.Id);
									}
									await component.Message.DeleteAsync();
									int tradeId = Convert.ToInt32(btnId.Replace("DismissTradeID-", ""));
									using (Wiki_DbContext ctxt = new())
									{
										ctxt.MHHOpenTrades.Remove(await ctxt.MHHOpenTrades.FirstAsync(x => x.Id == tradeId));
										await ctxt.SaveChangesAsync();
									}
								}
								break;
							case "Dismiss":
								try
								{
									if (InteractionEngine.PaginationPages.ContainsKey(component.Message.Id))
									{
										InteractionEngine.PaginationPages.Remove(component.Message.Id);
									}
									if (InteractionEngine.CardListPagination.ContainsKey(component.Message.Id))
									{
										InteractionEngine.CardListPagination.Remove(component.Message.Id);
									}
									await component.Message.DeleteAsync();
								}
								catch (HttpException e)
								{
									//TODO: This throws an error on ephemeral messages, and using arg.DeleteOriginalResponseAsync() also throws an error. Letting this slide takes the whole damn bot down.
								}
								break;
							case "DismissRecycle":
								try
								{
									if (InteractionEngine.OpenRecycles.ContainsKey(arg.User.Id))
									{
										InteractionEngine.OpenRecycles.Remove(arg.User.Id);
									}
									if (InteractionEngine.RecycleMessages.ContainsKey(arg.User.Id))
									{
										InteractionEngine.RecycleMessages.Remove(arg.User.Id);
									}
									await component.Message.DeleteAsync();
								}
								catch (HttpException e)
								{
									//TODO: This throws an error on ephemeral messages, and using arg.DeleteOriginalResponseAsync() also throws an error. Letting this slide takes the whole damn bot down.
								}
								break;
							case "RecycleButton":
								{
									await InteractionEngine.RunRecycle(arg);
									await arg.DeleteOriginalResponseAsync();
								}
								break;
							case string btnId when btnId.StartsWith("ViewTradeList") || btnId.StartsWith("ViewRecyclingBin"):
								{
									await arg.DeferAsync();
									int userId = Convert.ToInt32(btnId.Replace("ViewTradeListID-", "").Replace("ViewRecyclingBinID-", ""));
									WikiUser? user = null;
									using (Wiki_DbContext ctxt = new())
									{
										user = await ctxt.WikiUsers.Include(x => x.Cards).FirstAsync(x => x.Id == userId);
									}
									List<MHHCard> cards = [];
									bool isTrade = btnId.StartsWith("ViewTradeList");
									ComponentBuilderV2 builder = new ComponentBuilderV2()
										.WithTextDisplay($"## {user.Username}'s {(isTrade ? "Trade List" : "Recycling Bin")}");
									if (user.Cards != null)
									{
										int[] listCardIds = [];
										if (isTrade)
										{
											if (!string.IsNullOrEmpty(user.TradeInventoryJson) && user.TradeInventoryJson != "[]")
											{
												listCardIds = [.. JsonConvert.DeserializeObject<JArray>(user.TradeInventoryJson)!.Select(x => x.Value<int>())];
											}
										}
										else
										{
											if (!string.IsNullOrEmpty(user.RecyclingBinJson) && user.RecyclingBinJson != "[]")
											{
												listCardIds = [.. JsonConvert.DeserializeObject<JArray>(user.RecyclingBinJson)!.Select(x => x.Value<int>())];
											}
										}
										cards = [.. user.Cards.OrderBy(x => x.CardName).ThenBy(x => x.Rarity).ThenBy(x => x.Decoration).Where(x => listCardIds.Contains(x.Id))];
										List<IMessageComponentBuilder> components = [];
										foreach (MHHCard card in cards.Take(10))
										{
											string decoName = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
											components.Add(new SectionBuilder()
											{
												Accessory = new ButtonBuilder() { CustomId = $"FireCardCommandID-{card.Id}", Label = "View", Emote = new Emoji("🃏"), Style = ButtonStyle.Primary },
												Components = [
													new TextDisplayBuilder() { Content = $"**{card.CardName}{decoName}** [{card.Rarity}]" }
												]
											});
										}
										if (components.Count == 0)
										{
											components.Add(new TextDisplayBuilder($"This user doesn't have any cards in their {(isTrade ? "Trade List" : "Recycling Bin")}."));
										}
										builder = builder.WithContainer(new ContainerBuilder(components) { AccentColor = Discord.Color.LightOrange });
										if (cards.Count > 10)
										{
											builder = builder.WithActionRow([
												new ButtonBuilder() {
													CustomId = "NextListButton",
													Emote = new Emoji("➡️"),
													Label = "Next",
													Style = ButtonStyle.Primary
												},
												new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
											]);
										}
										else
										{
											builder = builder.WithActionRow([
												new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
											]);
										}
									}
									List<MHHCard[]> cardSets = [];
									if (user.Cards != null && cards.Count > 10)
									{
										int cntr = 0;
										while (cntr <= cards.Count)
										{
											MHHCard[] toAdd = [.. cards.Skip(cntr).Take(10)];
											if (toAdd.Length > 0)
											{
												cardSets.Add(toAdd);
											}
											cntr += 10;
										}
									}
									builder.WithTextDisplay($"-# Page 1/{(cardSets.Count == 0 ? 1 : cardSets.Count)}");
									RestFollowupMessage msg = await arg.FollowupAsync(components: builder.Build(), ephemeral: false);
									InteractionEngine.CardListPagination.Add(msg.Id, new Tuple<int, List<MHHCard[]>>(0, cardSets));
									InteractionEngine.MessageStoreTime.Add(msg.Id, DateTime.Now);
								}
								break;
							case string btnId when btnId.StartsWith("AcceptTrade"):
								{
									int tradeId = Convert.ToInt32(btnId.Replace("AcceptTradeID-", ""));
									using (Wiki_DbContext ctxt = new())
									{
										MHHOpenTrade trade = await ctxt.MHHOpenTrades.Include(x => x.Executor).Include(x => x.Recipient).Include(x => x.ExecutorOffer).Include(x => x.RecipientRequest).FirstAsync(x => x.Id == tradeId);
										WikiUser execUser = await ctxt.WikiUsers.FirstAsync(x => x.Id == trade.Executor.Id);
										WikiUser recipUser = await ctxt.WikiUsers.FirstAsync(x => x.Id == trade.Recipient.Id);
										int[] execIds = [.. trade.ExecutorOffer?.Select(x => x.Id) ?? []];
										List<MHHCard> execCards = [.. ctxt.MHHCards.Where(x => execIds.Any(y => y == x.Id))];
										int[] recipIds = [.. trade.RecipientRequest?.Select(x => x.Id) ?? []];
										List<MHHCard> recipCards = [.. ctxt.MHHCards.Where(x => recipIds.Any(y => y == x.Id))];
										if (!string.IsNullOrEmpty(execUser.FavoriteCardJson) && execUser.FavoriteCardJson != "[]")
										{
											execUser.FavoriteCardJson = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<JArray>(execUser.FavoriteCardJson)!
												.Select(x => x.Value<int>())
												.Where(x => !execCards.Any(y => y.Id == x)).ToArray());
										}
										if (!string.IsNullOrEmpty(execUser.TradeInventoryJson) && execUser.TradeInventoryJson != "[]")
										{
											execUser.TradeInventoryJson = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<JArray>(execUser.TradeInventoryJson)!
												.Select(x => x.Value<int>())
												.Where(x => !execCards.Any(y => y.Id == x)).ToArray());
										}
										if (!string.IsNullOrEmpty(execUser.RecyclingBinJson) && execUser.RecyclingBinJson != "[]")
										{
											execUser.RecyclingBinJson = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<JArray>(execUser.RecyclingBinJson)!
												.Select(x => x.Value<int>())
												.Where(x => !execCards.Any(y => y.Id == x)).ToArray());
										}
										if (!string.IsNullOrEmpty(recipUser.FavoriteCardJson) && recipUser.FavoriteCardJson != "[]")
										{
											recipUser.FavoriteCardJson = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<JArray>(recipUser.FavoriteCardJson)!
												.Select(x => x.Value<int>())
												.Where(x => !recipCards.Any(y => y.Id == x)).ToArray());
										}
										if (!string.IsNullOrEmpty(recipUser.TradeInventoryJson) && recipUser.TradeInventoryJson != "[]")
										{
											recipUser.TradeInventoryJson = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<JArray>(recipUser.TradeInventoryJson)!
												.Select(x => x.Value<int>())
												.Where(x => !recipCards.Any(y => y.Id == x)).ToArray());
										}
										if (!string.IsNullOrEmpty(recipUser.RecyclingBinJson) && recipUser.RecyclingBinJson != "[]")
										{
											recipUser.RecyclingBinJson = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<JArray>(recipUser.RecyclingBinJson)!
												.Select(x => x.Value<int>())
												.Where(x => !recipCards.Any(y => y.Id == x)).ToArray());
										}
										if (InteractionEngine.OpenRecycles.ContainsKey(execUser.UserID))
										{
											InteractionEngine.OpenRecycles.Remove(execUser.UserID);
										}
										if (InteractionEngine.OpenRecycles.ContainsKey(recipUser.UserID))
										{
											InteractionEngine.OpenRecycles.Remove(recipUser.UserID);
										}
										execUser.Cards!.RemoveAll(x => execCards.Any(y => y.Id == x.Id));
										recipUser.Cards!.AddRange(execCards);
										recipUser.Cards!.RemoveAll(x => recipCards.Any(y => y.Id == x.Id));
										execUser.Cards!.AddRange(recipCards);
										ctxt.MHHOpenTrades.Remove(trade);
										await ctxt.SaveChangesAsync();
										SocketGuild mainGuild = _client!.Guilds.First(x => x.Id == Convert.ToUInt64(ConfigurationManager.AppSettings.Get("ServerID")));
										SocketGuildUser exec = mainGuild.Users.First(x => x.Id == trade.Executor.UserID);
										SocketGuildUser recip = mainGuild.Users.First(x => x.Id == trade.Recipient.UserID);
										await exec.SendMessageAsync($"Your trade offer to {recip.DisplayName} has been accepted!.");
										await recip.SendMessageAsync($"Trade offer from {exec.DisplayName} has been accepted!");
										await arg.RespondAsync("Trade accepted!");
									}
								}
								break;
							case string btnId when btnId.StartsWith("RejectTrade"):
								{
									int tradeId = Convert.ToInt32(btnId.Replace("RejectTradeID-", ""));
									using (Wiki_DbContext ctxt = new())
									{
										MHHOpenTrade trade = await ctxt.MHHOpenTrades.Include(x => x.Executor).Include(x => x.Recipient).Include(x => x.ExecutorOffer).Include(x => x.RecipientRequest).FirstAsync(x => x.Id == tradeId);
										ctxt.MHHOpenTrades.Remove(trade);
										await ctxt.SaveChangesAsync();
										SocketGuild mainGuild = _client!.Guilds.First(x => x.Id == Convert.ToUInt64(ConfigurationManager.AppSettings.Get("ServerID")));
										SocketGuildUser exec = mainGuild.Users.First(x => x.Id == trade.Executor.UserID);
										SocketGuildUser recip = mainGuild.Users.First(x => x.Id == trade.Recipient.UserID);
										await exec.SendMessageAsync($"Your trade offer to {recip.DisplayName} has been rejected.");
										await recip.SendMessageAsync($"Trade offer from {exec.DisplayName} has been rejected!");
										await arg.RespondAsync("Trade rejected!");
									}
								}
								break;
							case string btnId when btnId.StartsWith("CounterTrade"):
								{
									int tradeId = Convert.ToInt32(btnId.Replace("CounterTradeID-", ""));
									MHHOpenTrade? oldTrade = null;
									using (Wiki_DbContext ctxt = new())
									{
										oldTrade = ctxt.MHHOpenTrades.Include(x => x.Executor)
												.ThenInclude(x => x.Cards)
											.Include(x => x.Recipient)
												.ThenInclude(x => x.Cards)
											.Include(x => x.ExecutorOffer)
											.Include(x => x.RecipientRequest).First(x => x.Id == tradeId);
									}
									await arg.DeferAsync();
									try
									{
										SocketGuild mainGuild = _client!.Guilds.First(x => x.Id == Convert.ToUInt64(ConfigurationManager.AppSettings.Get("ServerID")));
										IGuildUser exec = mainGuild.Users.First(x => x.Id == oldTrade.Executor.UserID);
										await exec.SendMessageAsync($"Your trade offer with {arg.User.Username} has been rejected; however, they are building a counter-offer.");
										WikiUser? executor = await InteractionEngine.GetUser(arg.User);
										if (string.IsNullOrEmpty(executor?.WikiUsername))
										{
											await arg.FollowupAsync($"Your Wiki username and ID could not be linked using your Discord account! Please either change your nickname or display name in the server to match your Wiki username, or run </link-user:{Utilities._cmds.First(x => x.Name == "link-user")}> before trying to pull cards.", ephemeral: true);
											return;
										}
										WikiUser? recipient = await InteractionEngine.GetUser(exec, addIfNotExists: false);
										if (string.IsNullOrEmpty(recipient?.WikiUsername))
										{
											await arg.FollowupAsync($"This user doesn't have their Wiki account linked yet! Have them run </link-user:{_cmds.First(x => x.Name == "link-user")}> first.", ephemeral: true);
											return;
										}
										MHHOpenTrade trade = new()
										{
											Expires = DateTimeOffset.Now.AddHours(6)
										};
										using (Wiki_DbContext ctxt = new())
										{
											ctxt.MHHOpenTrades.Remove(ctxt.MHHOpenTrades.Include(x => x.Executor)
												.ThenInclude(x => x.Cards)
											.Include(x => x.Recipient)
												.ThenInclude(x => x.Cards)
											.Include(x => x.ExecutorOffer)
											.Include(x => x.RecipientRequest).First(x => x.Id == oldTrade.Id));
											trade.Executor = await ctxt.WikiUsers.FirstAsync(x => x.Id == executor.Id);
											trade.Recipient = await ctxt.WikiUsers.FirstAsync(x => x.Id == recipient.Id);
											await ctxt.MHHOpenTrades.AddAsync(trade);
											await ctxt.SaveChangesAsync();
										}
										ComponentBuilderV2 builder = new ComponentBuilderV2()
											.WithTextDisplay($"## Your Trade List")
											.WithTextDisplay($"-# First, select the cards from your Trade List that you want to offer. Click the \"View\" button next to the card(s) you want to add, and then click \"Add to Trade\" on the card display. When you're ready to move on to the next step of the trade, click \"Continue\"."); ;
										List<MHHCard> tradeCards = [];
										if (executor.Cards != null)
										{
											int[] tradeIds = [];
											if (!string.IsNullOrEmpty(executor.TradeInventoryJson) && executor.TradeInventoryJson != "[]")
											{
												tradeIds = [.. JsonConvert.DeserializeObject<JArray>(executor.TradeInventoryJson)!.Select(x => x.Value<int>())];
											}
											tradeCards = [.. executor.Cards.OrderBy(x => x.CardName).ThenBy(x => x.Rarity).ThenBy(x => x.Decoration).Where(x => tradeIds.Contains(x.Id))];
											List<IMessageComponentBuilder> components = [];
											foreach (MHHCard card in tradeCards.Take(10))
											{
												string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
												components.Add(new SectionBuilder()
												{
													Accessory = new ButtonBuilder() { CustomId = $"FireTradeCardCommandID-{JsonConvert.SerializeObject(new { TradeId = trade.Id, CardId = card.Id })}", Label = "View", Emote = new Emoji("🃏"), Style = ButtonStyle.Primary },
													Components = [
														new TextDisplayBuilder() { Content = $"**{card.CardName}{deco}** [{card.Rarity}]" }
													]
												});
											}
											if (components.Count == 0)
											{
												components.Add(new TextDisplayBuilder("You don't have any cards in your Trade List."));
											}
											builder = builder.WithContainer(new ContainerBuilder(components) { AccentColor = Discord.Color.LightOrange });
											if (tradeCards.Count > 10)
											{
												builder = builder.WithActionRow([
													new ButtonBuilder() {
														CustomId = $"NextTradeListButtonID-{trade.Id}",
														Emote = new Emoji("➡️"),
														Label = "Next",
														Style = ButtonStyle.Primary
													},
													new ButtonBuilder()
													{
														CustomId = $"FinalizeTradeButtonID-{trade.Id}",
														Emote = new Emoji("➡️"),
														Label = "Continue",
														Style = ButtonStyle.Primary
													},
													new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"DismissTradeID-{trade.Id}", Emote = new Emoji("🔚"), Label = $"Close" }
												]);
											}
											else
											{
												builder = builder.WithActionRow([
													new ButtonBuilder()
													{
														CustomId = $"FinalizeTradeButtonID-{trade.Id}",
														Emote = new Emoji("➡️"),
														Label = "Continue",
														Style = ButtonStyle.Primary
													},
													new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"DismissTradeID-{trade.Id}", Emote = new Emoji("🔚"), Label = $"Close" }
												]);
											}
										}
										List<MHHCard[]> cardSets = [];
										if (tradeCards != null && tradeCards.Count > 10)
										{
											int cntr = 0;
											while (cntr <= tradeCards.Count)
											{
												MHHCard[] toAdd = [.. tradeCards.Skip(cntr).Take(10)];
												if (toAdd.Length > 0)
												{
													cardSets.Add(toAdd);
												}
												cntr += 10;
											}
										}
										builder.WithTextDisplay($"-# Page 1/{(cardSets.Count == 0 ? 1 : cardSets.Count)}");
										RestFollowupMessage msg = await arg.FollowupAsync(components: builder.Build(), ephemeral: false);
										if (executor.Cards != null && executor.Cards.Count > 10)
										{
											InteractionEngine.CardListPagination.Add(msg.Id, new Tuple<int, List<MHHCard[]>>(0, cardSets));
										}
										InteractionEngine.CardUsers.Add(msg.Id, arg.User.Id);
										InteractionEngine.MessageStoreTime.Add(msg.Id, DateTime.Now);
									}
									catch (Exception e)
									{
										Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
									}
								}
								break;
							case string btnId when btnId.StartsWith("FinalizeTradeButton"):
								{
									await arg.DeferAsync();
									MHHOpenTrade? trade = null;
									int id = Convert.ToInt32(btnId.Replace("FinalizeTradeButtonID-", ""));
									bool isFinal = false;
									using (Wiki_DbContext ctxt = new())
									{
										trade = ctxt.MHHOpenTrades.Include(x => x.Executor)
												.ThenInclude(x => x.Cards)
											.Include(x => x.Recipient)
												.ThenInclude(x => x.Cards)
											.Include(x => x.ExecutorOffer)
											.Include(x => x.RecipientRequest).First(x => x.Id == id);
										if ((trade.IsBuildingRecipientRequest && trade.RecipientRequest.Count == 0) || (!trade.IsBuildingRecipientRequest && trade.ExecutorOffer.Count == 0))
										{
											await arg.FollowupAsync("You need to select at least one card from the Trade List before continuing!", ephemeral: true);
											return;
										}
										else
										{
											if (!trade.IsBuildingRecipientRequest)
											{
												trade.IsBuildingRecipientRequest = true;
											}
											else
											{
												isFinal = true;
											}
											ctxt.MHHOpenTrades.Attach(trade);
											await ctxt.SaveChangesAsync();
										}
									}
									if (arg.User.Id == trade.Executor.UserID)
									{
										try
										{
											if (isFinal)
											{
												StringBuilder sb = new();
												sb.AppendLine("===========THEIR OFFER (their cards)===========");
												foreach (MHHCard card in trade.ExecutorOffer)
												{
													string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
													sb.AppendLine($"{card.CardId}: {card.CardName} [{card.Rarity}]{deco}");
												}
												sb.AppendLine("===========THEIR REQUEST (your cards)===========");
												foreach (MHHCard card in trade.RecipientRequest)
												{
													string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
													sb.AppendLine($"{card.CardId}: {card.CardName} [{card.Rarity}]{deco}");
												}
												sb.AppendLine();
												sb.AppendLine("If you accept this request, go back to the message you downloaded this file from and click \"Accept\". To reject the trade entirely, click \"Reject\". To submit a counteroffer, click \"Counter\".");
												SocketGuild mainGuild = _client!.Guilds.First(x => x.Id == Convert.ToUInt64(ConfigurationManager.AppSettings.Get("ServerID")));
												IUser recip = mainGuild.Users.First(x => x.Id == trade.Recipient.UserID);
												await arg.DeleteOriginalResponseAsync();
												using (MemoryStream ms = new(Encoding.Unicode.GetBytes(sb.ToString())))
												{
													await recip.SendFileAsync(attachment: new FileAttachment(ms, "tradeOffer.txt"), components: new ComponentBuilderV2()
														.WithTextDisplay($"## Offered Trade from {trade.Executor.Username}")
														.WithActionRow([
															new ButtonBuilder() { CustomId = "AcceptTradeID-" + trade.Id, Label = "Accept", Emote = new Emoji("✅"), Style = ButtonStyle.Primary },
															new ButtonBuilder() { CustomId = "RejectTradeID-" + trade.Id, Label = "Reject", Emote = new Emoji("❌"), Style = ButtonStyle.Primary },
															new ButtonBuilder() { CustomId = "CounterTradeID-" + trade.Id, Label = "Counter", Emote = new Emoji("🔃"), Style = ButtonStyle.Primary }
														])
														.WithFile(new FileComponentBuilder(new UnfurledMediaItemProperties("attachment://tradeOffer.txt")))
														.Build());
												}
												await arg.RespondAsync($"Your trade has been sent to {trade.Recipient.Username}'s DMs. When they respond, you will be notified!", ephemeral: true);
											}
											else
											{
												bool isOffer = !trade.IsBuildingRecipientRequest;
												string helpText = "-# First, select the cards from your Trade List that you want to offer. Click the \"View\" button next to the card(s) you want to add, and then click \"Add to Trade\" on the card display. When you're ready to move on to the next step of the trade, click \"Continue\".";
												if (!isOffer)
												{
													helpText = "-# Now, select the cards from your trade partner's Trade List that you want to request in exchange. Use the same method you did before to add cards to the trade request. When you're ready to submit the trade to your partner for approval, click \"Send Trade Offer\".";
												}
												ComponentBuilderV2 builder = new ComponentBuilderV2()
													.WithTextDisplay($"## {(isOffer ? "Your" : trade.Recipient.Username + "'s")} Trade List")
													.WithTextDisplay(helpText);
												List<MHHCard> tradeCards = [];
												if (trade.Recipient.Cards != null)
												{
													int[] tradeIds = [];
													if (!string.IsNullOrEmpty(trade.Recipient.TradeInventoryJson) && trade.Recipient.TradeInventoryJson != "[]")
													{
														tradeIds = [.. JsonConvert.DeserializeObject<JArray>(trade.Recipient.TradeInventoryJson)!.Select(x => x.Value<int>())];
													}
													tradeCards = [.. trade.Recipient.Cards.OrderBy(x => x.CardName).ThenBy(x => x.Rarity).ThenBy(x => x.Decoration).Where(x => tradeIds.Contains(x.Id))];
													List<IMessageComponentBuilder> components = [];
													foreach (MHHCard card in tradeCards.Take(10))
													{
														string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
														components.Add(new SectionBuilder()
														{
															Accessory = new ButtonBuilder() { CustomId = $"FireTradeCardCommandID-{JsonConvert.SerializeObject(new { TradeId = trade.Id, CardId = card.Id })}", Label = "View", Emote = new Emoji("🃏"), Style = ButtonStyle.Primary },
															Components = [
																new TextDisplayBuilder() { Content = $"**{card.CardName}{deco}** [{card.Rarity}]" }
															]
														});
													}
													if (components.Count == 0)
													{
														components.Add(new TextDisplayBuilder("You don't have any cards in your Trade List."));
													}
													builder = builder.WithContainer(new ContainerBuilder(components) { AccentColor = Discord.Color.LightOrange });
													if (tradeCards.Count > 10)
													{
														builder = builder.WithActionRow([
															new ButtonBuilder() {
																CustomId = $"NextTradeListButtonID-{trade.Id}",
																Emote = new Emoji("➡️"),
																Label = "Next",
																Style = ButtonStyle.Primary
															},
															new ButtonBuilder()
															{
																CustomId = $"FinalizeTradeButtonID-{trade.Id}",
																Emote = new Emoji("➡️"),
																Label = "Send Trade Offer",
																Style = ButtonStyle.Primary
															},
															new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"DismissTradeID-{trade.Id}", Emote = new Emoji("🔚"), Label = $"Close" }
														]);
													}
													else
													{
														builder = builder.WithActionRow([
															new ButtonBuilder()
															{
																CustomId = $"FinalizeTradeButtonID-{trade.Id}",
																Emote = new Emoji("➡️"),
																Label = "Send Trade Offer",
																Style = ButtonStyle.Primary
															},
															new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"DismissTradeID-{trade.Id}", Emote = new Emoji("🔚"), Label = $"Close" }
														]);
													}
												}
												List<MHHCard[]> cardSets = [];
												if (tradeCards != null && tradeCards.Count > 10)
												{
													int cntr = 0;
													while (cntr <= tradeCards.Count)
													{
														MHHCard[] toAdd = [.. tradeCards.Skip(cntr).Take(10)];
														if (toAdd.Length > 0)
														{
															cardSets.Add(toAdd);
														}
														cntr += 10;
													}
												}
												builder.WithTextDisplay($"-# Page 1/{(cardSets.Count == 0 ? 1 : cardSets.Count)}");
												await arg.DeleteOriginalResponseAsync();
												RestFollowupMessage msg = await arg.FollowupAsync(components: builder.Build(), ephemeral: false);
												if (trade.Recipient.Cards != null && trade.Recipient.Cards.Count > 10)
												{
													InteractionEngine.CardListPagination.Add(msg.Id, new Tuple<int, List<MHHCard[]>>(0, cardSets));
												}
												InteractionEngine.CardUsers.Add(msg.Id, arg.User.Id);
												InteractionEngine.MessageStoreTime.Add(msg.Id, DateTime.Now);
											}
										}
										catch (Exception e)
										{
											Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
										}
									}
								}
								break;
							case string btnId when (btnId.StartsWith("FavoriteCard") || btnId.StartsWith("AddTradeCard") || btnId.StartsWith("AddRecycleCard")) && !btnId.Contains("TradeCardCommand") && !btnId.Contains("RecycleCardCommand"):
								if (InteractionEngine.CardUsers[component.Message.Id] == arg.User.Id)
								{
									await arg.DeferAsync();
									using (Wiki_DbContext ctxt = new())
									{
										int id = Convert.ToInt32(btnId.Replace("FavoriteCardID-", "").Replace("AddTradeCardID-", "").Replace("AddRecycleCardID-", ""));
										WikiUser user = await ctxt.WikiUsers.FirstAsync(x => x.UserID == arg.User.Id);
										List<int> newList = [];
										string list = btnId.StartsWith("FavoriteCard") ? user.FavoriteCardJson : btnId.StartsWith("AddTradeCard") ? user.TradeInventoryJson : user.RecyclingBinJson;
										if (!string.IsNullOrEmpty(list))
										{
											newList = [.. JsonConvert.DeserializeObject<JArray>(list)!.Select(x => x.Value<int>())];
										}
										if (btnId.StartsWith("FavoriteCard") && newList.Count >= 10)
										{
											await arg.FollowupAsync("You already have 10 favorites!", ephemeral: true);
										}
										else
										{
											newList.Add(id);
											string response = "";
											if (btnId.StartsWith("FavoriteCard"))
											{
												response = "favorited";
												user.FavoriteCardJson = JsonConvert.SerializeObject(newList);
											}
											else if (btnId.StartsWith("AddRecycleCard"))
											{
												response = "added to your Recycling Bin";
												user.RecyclingBinJson = JsonConvert.SerializeObject(newList);
											}
											else
											{
												response = "added to your Trade List";
												user.TradeInventoryJson = JsonConvert.SerializeObject(newList);
											}
											await ctxt.SaveChangesAsync();
											await arg.FollowupAsync($"Card {response}!", ephemeral: true);
										}
									}
								}
								break;
							case string btnId when (btnId.StartsWith("UnfavoriteCard") || btnId.StartsWith("RemoveTradeCard") || btnId.StartsWith("RemoveRecycleCard")) && !btnId.Contains("TradeCardCommand") && !btnId.Contains("RecycleCardCommand"):
								if (InteractionEngine.CardUsers[component.Message.Id] == arg.User.Id)
								{
									await arg.DeferAsync();
									using (Wiki_DbContext ctxt = new())
									{
										int id = Convert.ToInt32(btnId.Replace("UnfavoriteCardID-", "").Replace("RemoveTradeCardID-", "").Replace("RemoveRecycleCardID-", ""));
										WikiUser user = await ctxt.WikiUsers.FirstAsync(x => x.UserID == arg.User.Id);
										List<int> newList = [];
										string list = btnId.StartsWith("FavoriteCard") ? user.FavoriteCardJson : btnId.StartsWith("RemoveTradeCard") ? user.TradeInventoryJson : user.RecyclingBinJson;
										if (!string.IsNullOrEmpty(list))
										{
											newList = [.. JsonConvert.DeserializeObject<JArray>(list)!.Select(x => x.Value<int>())];
										}
										newList.Remove(id);
										string response = "";
										if (btnId.StartsWith("FavoriteCard"))
										{
											response = "unfavorited";
											user.FavoriteCardJson = JsonConvert.SerializeObject(newList);
										}
										else if (btnId.StartsWith("AddRecycleCard"))
										{
											response = "removed from your Recycling Bin";
											user.RecyclingBinJson = JsonConvert.SerializeObject(newList);
										}
										else
										{
											response = "removed from your Trade List";
											user.TradeInventoryJson = JsonConvert.SerializeObject(newList);
										}
										await ctxt.SaveChangesAsync();
										await arg.FollowupAsync($"Card {response}!", ephemeral: true);
									}
								}
								break;
							case string btnId when btnId.StartsWith("AddTradeCardCommandID") || btnId.StartsWith("RemoveTradeCardCommandID"):
								{
									await arg.DeferAsync();
									using (Wiki_DbContext ctxt = new())
									{
										JObject data = JsonConvert.DeserializeObject<JObject>(btnId.Replace("AddTradeCardCommandID-", "").Replace("RemoveTradeCardCommandID-", ""))!;
										int tradeId = data.Value<int>("TradeId");
										int cardId = data.Value<int>("CardId");
										MHHCard? card = null;
										MHHOpenTrade trade = await ctxt.MHHOpenTrades.Include(x => x.Executor).Include(x => x.Recipient).Include(x => x.ExecutorOffer).Include(x => x.RecipientRequest).FirstAsync(x => x.Id == tradeId);
										if (arg.User.Id == trade.Executor.UserID)
										{
											bool addCard = btnId.StartsWith("AddTradeCardCommandID");
											try
											{
												if (!trade.IsBuildingRecipientRequest)
												{
													WikiUser? usr = await ctxt.WikiUsers.Include(x => x.Cards).FirstOrDefaultAsync(x => arg.User.Id == x.UserID);
													card = usr?.Cards?.FirstOrDefault(x => x.Id == cardId);
												}
												else
												{
													card = await ctxt.MHHCards.FirstAsync(x => x.Id == cardId);
												}
												if (card == null)
												{
													await arg.FollowupAsync("You don't have a card with this ID!", ephemeral: true);
													return;
												}
												if (!trade.IsBuildingRecipientRequest)
												{
													trade.ExecutorOffer ??= [];
													if (addCard)
													{
														trade.ExecutorOffer.Add(card);
													}
													else
													{
														trade.ExecutorOffer.RemoveAll(x => x.Id == card.Id);
													}
												}
												else
												{
													trade.RecipientRequest ??= [];
													if (addCard)
													{
														trade.RecipientRequest.Add(card);
													}
													else
													{
														trade.RecipientRequest.RemoveAll(x => x.Id == card.Id);
													}
												}
												ctxt.MHHOpenTrades.Attach(trade);
												await ctxt.SaveChangesAsync();
												await arg.FollowupAsync($"Card {(addCard ? "added to" : "removed from")} {(!trade.IsBuildingRecipientRequest ? "your offer" : "your request")}!", ephemeral: true);
												await arg.DeleteOriginalResponseAsync();
											}
											catch (Exception e)
											{
												Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
											}
										}
									}
								}
								break;
							case string btnId when btnId.StartsWith("AddRecycleCardCommandID") || btnId.StartsWith("RemoveRecycleCardCommandID"):
								{
									await arg.DeferAsync(true);
									MHHCard? card = null;
									int cardId = Convert.ToInt32(btnId.Replace("AddRecycleCardCommandID-", "").Replace("RemoveRecycleCardCommandID-", ""));
									bool cardInTrades = false;
									using (Wiki_DbContext ctxt = new())
									{
										card = ctxt.WikiUsers.Include(x => x.Cards).FirstOrDefault(x => x.UserID == arg.User.Id)?.Cards?.FirstOrDefault(x => x.Id == cardId);
										cardInTrades = ctxt.MHHOpenTrades.Any(x => x.ExecutorOffer.Any(y => y.Id == cardId) || x.RecipientRequest.Any(y => y.Id == cardId));
									}
									bool addCard = btnId.StartsWith("AddRecycleCardCommandID");
									try
									{
										if (card == null)
										{
											await arg.FollowupAsync("You don't have a card with this ID!", ephemeral: true);
											return;
										}
										if (cardInTrades)
										{
											await arg.FollowupAsync($"This card is in an open trade! Either resolve the trade, or contact {MentionUtils.MentionUser(338081040134307840)} to resolve the issue if the trade is no longer open/avaiable.", ephemeral: true);
											return;
										}
										if (addCard)
										{
											InteractionEngine.OpenRecycles[arg.User.Id].Add(card.Id);
										}
										else
										{
											InteractionEngine.OpenRecycles[arg.User.Id].Remove(card.Id);
										}
										await arg.FollowupAsync($"Card {(addCard ? "added to" : "removed from")} your recycling pile!", ephemeral: true);
										await arg.DeleteOriginalResponseAsync();
									}
									catch (Exception e)
									{
										Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
									}
								}
								break;
							case string btnId when btnId.StartsWith("FireRecycleCardCommand"):
								{
									await arg.DeferAsync();
									int cardId = Convert.ToInt32(btnId.Replace("FireRecycleCardCommandID-", ""));
									MHHCard? card = null;
									WikiUser? user = null;
									bool hasCard = InteractionEngine.OpenRecycles[arg.User.Id].Any(x => x == cardId);
									using (Wiki_DbContext ctxt = new())
									{
										user = await ctxt.WikiUsers.Include(x => x.Cards).FirstOrDefaultAsync(x => arg.User.Id == x.UserID);
										card = user?.Cards?.FirstOrDefault(x => x.Id == cardId);
									}
									try
									{
										if (card == null)
										{
											await arg.FollowupAsync("You don't have a card with this ID!", ephemeral: true);
											return;
										}
										MHHCardPackage pkg = await MHHCardPackage.BuildCardPackage(card);
										using (MemoryStream stream = new(pkg.CardBytes))
										{
											string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
											string title = $"{card.CardName}{deco}";
											_cmdId ??= _cmds.First(x => x.Name == "mhhc-info").Id;
											RestFollowupMessage msg = await arg.FollowupWithFileAsync(attachment: new(stream, $"{card.CardId}.png"), components: new ComponentBuilderV2()
												.WithTextDisplay($"## {title}")
												.WithMediaGallery([new MediaGalleryItemProperties()
												{
													Description = card.CardId,
													Media = new UnfurledMediaItemProperties($"attachment://{card.CardId}.png")
												}])
												.WithTextDisplay($"**Name**: {card.CardName}\r\n**ID**: {card.CardId}\r\n**Rarity**: {card.Rarity}\r\n**Decoration**: {card.Decoration.GetDescription()}")
												.WithTextDisplay($"-# Scans by Grender; TL by Mir, Yuwika, and MandL27. </mhhc-info:{_cmdId}> for their links!")
												.WithActionRow([
													new ButtonBuilder() { CustomId = $"{(!hasCard ? "Add" : "Remove")}RecycleCardCommandID-{card.Id}", Label = !hasCard ? "Add to Recycle Pile" : "Remove from Recycle Pile", Emote = new Emoji("🔃"), Style = ButtonStyle.Primary },
													new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
												]).Build(), ephemeral: false);
											InteractionEngine.CardUsers.Add(msg.Id, arg.User.Id);
											InteractionEngine.MessageStoreTime.Add(msg.Id, DateTime.Now);
										}
									}
									catch (Exception e)
									{
										Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
									}
								}
								break;
							case string btnId when btnId.StartsWith("FireTradeCardCommand"):
								{
									JObject data = JsonConvert.DeserializeObject<JObject>(btnId.Replace("FireTradeCardCommandID-", ""))!;
									int tradeId = data.Value<int>("TradeId");
									int cardId = data.Value<int>("CardId");
									MHHCard? card = null;
									MHHOpenTrade? trade = null;
									using (Wiki_DbContext ctxt = new())
									{
										trade = await ctxt.MHHOpenTrades.Include(x => x.Executor).Include(x => x.Recipient).Include(x => x.ExecutorOffer).Include(x => x.RecipientRequest).FirstAsync(x => x.Id == tradeId);
										if (!trade.IsBuildingRecipientRequest)
										{
											WikiUser? usr = await ctxt.WikiUsers.Include(x => x.Cards).FirstOrDefaultAsync(x => arg.User.Id == x.UserID);
											card = usr?.Cards?.FirstOrDefault(x => x.Id == cardId);
										}
										else
										{
											card = await ctxt.MHHCards.FirstAsync(x => x.Id == cardId);
										}
									}
									if (arg.User.Id == trade.Executor.UserID)
									{
										await arg.DeferAsync();
										try
										{
											if (card == null)
											{
												await arg.FollowupAsync("You don't have a card with this ID!", ephemeral: true);
												return;
											}
											bool hasCard = !trade.IsBuildingRecipientRequest ? trade.ExecutorOffer.Any(x => x.Id == card.Id) : trade.RecipientRequest.Any(x => x.Id == card.Id);
											MHHCardPackage pkg = await MHHCardPackage.BuildCardPackage(card);
											using (MemoryStream stream = new(pkg.CardBytes))
											{
												string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
												string title = $"{card.CardName}{deco}";
												_cmdId ??= _cmds.First(x => x.Name == "mhhc-info").Id;
												RestFollowupMessage msg = await arg.FollowupWithFileAsync(attachment: new(stream, $"{card.CardId}.png"), components: new ComponentBuilderV2()
													.WithTextDisplay($"## {title}")
													.WithMediaGallery([new MediaGalleryItemProperties()
													{
														Description = card.CardId,
														Media = new UnfurledMediaItemProperties($"attachment://{card.CardId}.png")
													}])
													.WithTextDisplay($"**Name**: {card.CardName}\r\n**ID**: {card.CardId}\r\n**Rarity**: {card.Rarity}\r\n**Decoration**: {card.Decoration.GetDescription()}")
													.WithTextDisplay($"-# Scans by Grender; TL by Mir, Yuwika, and MandL27. </mhhc-info:{_cmdId}> for their links!")
													.WithActionRow([
														new ButtonBuilder() { CustomId = $"{(!hasCard ? "Add" : "Remove")}TradeCardCommandID-{JsonConvert.SerializeObject(new { TradeId = trade.Id, CardId = card.Id })}", Label = !hasCard ? "Add to Trade" : "Remove from Trade", Emote = new Emoji("🔃"), Style = ButtonStyle.Primary },
														new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"RemoveTradeCardID-{card.Id}", Emote = new Emoji("📉"), Label = "Remove from Trade List" },
													new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
													]).Build(), ephemeral: false);
												InteractionEngine.CardUsers.Add(msg.Id, arg.User.Id);
												InteractionEngine.MessageStoreTime.Add(msg.Id, DateTime.Now);
											}
										}
										catch (Exception e)
										{
											Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
										}
									}
								}
								break;
							case string btnId when btnId.StartsWith("FireCardCommand"):
								{
									int id = Convert.ToInt32(btnId[(btnId.IndexOf('-') + 1)..]);
									arg.DeferAsync();
									var infoMsg = await arg.FollowupAsync("Fetching card...", ephemeral: true);
									try
									{
										MHHCard? card = null;
										bool isUsersCard = false;
										bool isFavorite = false;
										bool isTrading = false;
										bool isRecycling = false;
										using (Wiki_DbContext ctxt = new())
										{
											card = await ctxt.MHHCards.FirstAsync(x => x.Id == id);
											isUsersCard = ctxt.WikiUsers.Any(x => x.UserID == arg.User.Id && x.Cards != null && x.Cards!.Any(y => y.Id == id));
											if (isUsersCard)
											{
												WikiUser user = await ctxt.WikiUsers.Include(x => x.Cards).FirstAsync(x => arg.User.Id == x.UserID);
												isFavorite = !string.IsNullOrEmpty(user.FavoriteCardJson) && user.FavoriteCardJson != "[]" && JsonConvert.DeserializeObject<JArray>(user.FavoriteCardJson)!.Any(x => x.Value<int>() == id);
												isTrading = !string.IsNullOrEmpty(user.TradeInventoryJson) && user.TradeInventoryJson != "[]" && JsonConvert.DeserializeObject<JArray>(user.TradeInventoryJson)!.Any(x => x.Value<int>() == id);
												isRecycling = !string.IsNullOrEmpty(user.RecyclingBinJson) && user.RecyclingBinJson != "[]" && JsonConvert.DeserializeObject<JArray>(user.RecyclingBinJson)!.Any(x => x.Value<int>() == id);
											}
										}
										if (card == null)
										{
											await arg.FollowupAsync("This card doesn't exist (??????)!", ephemeral: true);
											return;
										}
										MHHCardPackage pkg = await MHHCardPackage.BuildCardPackage(card);
										using (MemoryStream stream = new(pkg.CardBytes))
										{
											string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
											string title = $"{card.CardName}{deco}";
											_cmdId ??= _cmds.First(x => x.Name == "mhhc-info").Id;
											ComponentBuilderV2 builder = new ComponentBuilderV2()
												.WithTextDisplay($"## {title}")
												.WithMediaGallery([new MediaGalleryItemProperties()
												{
													Description = card.CardId,
													Media = new UnfurledMediaItemProperties($"attachment://{card.CardId}.png")
												}])
												.WithTextDisplay($"**Name**: {card.CardName}\r\n**JP Name**: {card.CardNameJP}\r\n**ID**: {card.CardId}\r\n**Type**: {card.CardType}\r\n**Rarity**: {card.Rarity}\r\n**Decoration**: {card.Decoration.GetDescription()}\r\n**Description**: {card.CardDescription}")
												.WithTextDisplay($"-# Scans by Grender; TL by Mir, Yuwika, and MandL27. </mhhc-info:{_cmdId}> for their links!");
											if (isUsersCard)
											{
												builder = builder.WithActionRow([
													new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"{(isFavorite ? "Unfavorite" : "Favorite") + "CardID-" + id}", Emote = new Emoji(isFavorite ? "❌" : "❤️"), Label = $"{(isFavorite ? "Unfavorite" : "Favorite")}" },
													new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"{(isTrading ? "RemoveTrade" : "AddTrade") + "CardID-" + id}", Emote = new Emoji(isTrading ? "📉" : "📈"), Label = $"{(isTrading ? "Remove from Trade List" : "Add to Trade List")}" },
													new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"{(isRecycling ? "RemoveRecycle" : "AddRecycle") + "CardID-" + id}", Emote = new Emoji(isRecycling ? "❌" : "🗑️"), Label = $"{(isRecycling ? "Remove from Recycling Bin" : "Add to Recycling Bin")}" },
													new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
												]);
											}
											else
											{
												builder = builder.WithActionRow([
													new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
												]);
											}
											RestFollowupMessage msg = await arg.FollowupWithFileAsync(attachment: new(stream, $"{card.CardId}.png"), components: builder.Build(), ephemeral: false);
											infoMsg.DeleteAsync();
											InteractionEngine.CardUsers.Add(msg.Id, arg.User.Id);
											InteractionEngine.MessageStoreTime.Add(msg.Id, DateTime.Now);
										}
									}
									catch (Exception e)
									{
										Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
									}
								}
								break;
							case "NextTradeListButton":
							case "PreviousTradeListButton":
								{
									MHHOpenTrade? trade = null;
									int id = Convert.ToInt32(component.Data.CustomId.Replace("NextTradeListButtonID-", "").Replace("PreviousTradeListButtonID-", ""));
									using (Wiki_DbContext ctxt = new())
									{
										trade = await ctxt.MHHOpenTrades.Include(x => x.Executor).Include(x => x.Recipient).Include(x => x.ExecutorOffer).Include(x => x.RecipientRequest).FirstAsync(x => x.Id == id);
										if (!trade.IsBuildingRecipientRequest)
										{
											trade.IsBuildingRecipientRequest = true;
										}
										ctxt.MHHOpenTrades.Attach(trade);
										await ctxt.SaveChangesAsync();
									}
									Tuple<int, List<MHHCard[]>> pages = InteractionEngine.CardListPagination[component.Message.Id];
									int index = pages.Item1;
									if (component.Data.CustomId.StartsWith("NextTradeListButton"))
									{
										index++;
									}
									else
									{
										index--;
									}
									if (index >= 0 && index < pages.Item2.Count)
									{
										bool isOffer = !trade.IsBuildingRecipientRequest;
										string helpText = "-# First, select the cards from your Trade List that you want to offer. Click the \"View\" button next to the card(s) you want to add, and then click \"Add to Trade\" on the card display. When you're ready to move on to the next step of the trade, click \"Continue\".";
										if (!isOffer)
										{
											helpText = "-# Now, select the cards from your trade partner's Trade List that you want to request in exchange. Use the same method you did before to add cards to the trade request. When you're ready to submit the trade to your partner for approval, click \"Send Trade Offer\".";
										}
										ComponentBuilderV2 builder = new ComponentBuilderV2()
											.WithTextDisplay($"## {(isOffer ? "Your" : trade.Recipient.Username + "'s")} Trade List")
											.WithTextDisplay(helpText);
										List<IMessageComponentBuilder> components = [];
										foreach (MHHCard card in pages.Item2[index])
										{
											string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
											components.Add(new SectionBuilder()
											{
												Accessory = new ButtonBuilder() { CustomId = $"FireTradeCardCommandID-{JsonConvert.SerializeObject(new { TradeId = trade.Id, CardId = card.Id })}", Label = "View", Emote = new Emoji("🃏"), Style = ButtonStyle.Primary },
												Components = [
													new TextDisplayBuilder() { Content = $"**{card.CardName}{deco}** [{card.Rarity}]" }
												]
											});
										}
										builder = builder.WithContainer(new ContainerBuilder(components) { AccentColor = Discord.Color.LightOrange });
										List<ButtonBuilder> buttons = [];
										if (pages.Item2.Count > 1)
										{
											if (index == 0)
											{
												buttons =
												[
													new() {
														CustomId = $"NextTradeListButtonID-{trade.Id}",
														Emote = new Emoji("➡️"),
														Label = "Next",
														Style = ButtonStyle.Primary
													}
												];
											}
											else if (index == pages.Item2.Count - 1)
											{
												buttons =
												[
													new()
													{
														CustomId = $"PreviousTradeListButtonID-{trade.Id}",
														Emote = new Emoji("⬅️"),
														Label = "Previous",
														Style = ButtonStyle.Primary
													}
												];
											}
											else
											{
												buttons =
												[
													new()
													{
														CustomId = $"PreviousTradeListButtonID-{trade.Id}",
														Emote = new Emoji("⬅️"),
														Label = "Previous",
														Style = ButtonStyle.Primary
													},
													new() {
														CustomId = $"NextTradeListButtonID-{trade.Id}",
														Emote = new Emoji("➡️"),
														Label = "Next",
														Style = ButtonStyle.Primary
													}
												];
											}
											buttons.Add(new()
											{
												CustomId = $"FinalizeTradeButtonID-{trade.Id}",
												Emote = new Emoji("➡️"),
												Label = !trade.IsBuildingRecipientRequest ? "Continue" : "Send Trade Offer",
												Style = ButtonStyle.Primary
											});
											if (buttons.Count > 0)
											{
												builder.WithActionRow(components: buttons);
											}
											builder.WithTextDisplay($"-# Page {index + 1}/{pages.Item2.Count}");
											await component.UpdateAsync(x => x.Components = builder.Build());
											InteractionEngine.CardListPagination[component.Message.Id] = new Tuple<int, List<MHHCard[]>>(index, pages.Item2);
										}
									}
								}
								break;
							case "NextRecycleListButton":
							case "PreviousRecycleListButton":
							case "NextListButton":
							case "PreviousListButton":
								{
									Tuple<int, List<MHHCard[]>> pages = InteractionEngine.CardListPagination[component.Message.Id];
									int index = pages.Item1;
									if (component.Data.CustomId == "NextListButton" || component.Data.CustomId == "NextRecycleListButton")
									{
										index++;
									}
									else
									{
										index--;
									}
									if (index >= 0 && index < pages.Item2.Count)
									{
										ComponentBuilderV2 builder = new();
										if (component.Data.CustomId.Contains("Recycle"))
										{
											builder.WithTextDisplay($"## Your Recycling Bin")
												.WithTextDisplay($"-# First, select the cards from your Recycling Bin that you want to trade in. You **must** select 5 cards to recycle. A card will be randomly selected from the group of 5 to be the template for the recycle. The resulting card will be one rarity higher, one decoration level higher, or both one rarity and one decoration level higher, than the template card. Thus, a higher average rarity and decoration level of your bin will increase your odds for receiving a card of the next highest rarity and/or decoration level. The resulting card will *always* be an upgrade from the lowest rarity card provided. When all 5 cards are selected, click the \"Recycle\" button to receive your card.");
										}
										else
										{

											builder.WithTextDisplay($"## Card Inventory");
										}
										List<IMessageComponentBuilder> components = [];
										foreach (MHHCard card in pages.Item2[index])
										{
											string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
											components.Add(new SectionBuilder()
											{
												Accessory = new ButtonBuilder() { CustomId = $"Fire{(component.Data.CustomId.Contains("Recycle") ? "Recycle" : "")}CardCommandID-{card.Id}", Label = "View", Emote = new Emoji("🃏"), Style = ButtonStyle.Primary },
												Components = [
													new TextDisplayBuilder() { Content = $"**{card.CardName}{deco}** [{card.Rarity}]" }
												]
											});
										}
										builder = builder.WithContainer(new ContainerBuilder(components) { AccentColor = Discord.Color.LightOrange });
										List<ButtonBuilder> buttons = [];
										if (pages.Item2.Count > 1)
										{
											if (index == 0)
											{
												buttons =
												[
													new() {
														CustomId = $"Next{(component.Data.CustomId.Contains("Recycle") ? "Recycle" : "")}ListButton",
														Emote = new Emoji("➡️"),
														Label = "Next",
														Style = ButtonStyle.Primary
													}
												];
											}
											else if (index == pages.Item2.Count - 1)
											{
												buttons =
												[
													new()
													{
														CustomId = $"Previous{(component.Data.CustomId.Contains("Recycle") ? "Recycle" : "")}ListButton",
														Emote = new Emoji("⬅️"),
														Label = "Previous",
														Style = ButtonStyle.Primary
													}
												];
											}
											else
											{
												buttons =
												[
													new()
													{
														CustomId = $"Previous{(component.Data.CustomId.Contains("Recycle") ? "Recycle" : "")}ListButton",
														Emote = new Emoji("⬅️"),
														Label = "Previous",
														Style = ButtonStyle.Primary
													},
														new() {
														CustomId = $"Next{(component.Data.CustomId.Contains("Recycle") ? "Recycle" : "")}ListButton",
														Emote = new Emoji("➡️"),
														Label = "Next",
														Style = ButtonStyle.Primary
													}
												];
											}
											if (component.Data.CustomId.Contains("Recycle"))
											{
												buttons.Add(new ButtonBuilder()
												{
													CustomId = "RecycleButton",
													Emote = new Emoji("🗑️"),
													Label = "Recycle",
													Style = ButtonStyle.Primary
												});
											}
											buttons.Add(new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" });
											if (buttons.Count > 0)
											{
												builder.WithActionRow(components: buttons);
											}
											builder.WithTextDisplay($"-# Page {index + 1}/{pages.Item2.Count}");
											await component.UpdateAsync(x => x.Components = builder.Build());
											InteractionEngine.CardListPagination[component.Message.Id] = new Tuple<int, List<MHHCard[]>>(index, pages.Item2);
										}
									}
								}
								break;
							case "NextCardButton":
							case "PreviousCardButton":
								{
									Tuple<int, MHHCardPackage[]> pages = InteractionEngine.PaginationPages[component.Message.Id];
									int index = pages.Item1;
									if (component.Data.CustomId == "NextCardButton")
									{
										index++;
									}
									else
									{
										index--;
									}
									if (index >= 0 && index < pages.Item2.Length)
									{
										List<ButtonBuilder> buttons = [];
										if (pages.Item2.Length > 1)
										{
											if (index == 0)
											{
												buttons =
												[
													new() {
														CustomId = "NextCardButton",
														Emote = new Emoji("➡️"),
														Label = "Next",
														Style = ButtonStyle.Primary
													}
												];
											}
											else if (index == pages.Item2.Length - 1)
											{
												buttons =
												[
													new()
													{
														CustomId = "PreviousCardButton",
														Emote = new Emoji("⬅️"),
														Label = "Previous",
														Style = ButtonStyle.Primary
													}
												];
											}
											else
											{
												buttons =
												[
													new()
													{
														CustomId = "PreviousCardButton",
														Emote = new Emoji("⬅️"),
														Label = "Previous",
														Style = ButtonStyle.Primary
													},
													new() {
														CustomId = "NextCardButton",
														Emote = new Emoji("➡️"),
														Label = "Next",
														Style = ButtonStyle.Primary
													}
												];
											}
										}
										MHHCardPackage pkg = pages.Item2[index];
										MHHCard card = pkg.Card;
										bool isUsersCard = false;
										bool isFavorite = false;
										bool isTrading = false;
										bool isRecycling = false;
										using (Wiki_DbContext ctxt = new())
										{
											MHHCard? dbCard = await ctxt.MHHCards.FirstOrDefaultAsync(x => x.Guid == card.Guid);
											if (dbCard != null)
											{
												card.Id = dbCard.Id;
												WikiUser? user = await ctxt.WikiUsers.FirstOrDefaultAsync(x => x.UserID == arg.User.Id && x.Cards != null && x.Cards.Any(y => y.Id == card.Id));
												isUsersCard = user != null;
												if (user != null)
												{
													isFavorite = !string.IsNullOrEmpty(user.FavoriteCardJson) && user.FavoriteCardJson != "[]" && JsonConvert.DeserializeObject<JArray>(user.FavoriteCardJson)!.Any(x => x.Value<int>() == card.Id);
													isTrading = !string.IsNullOrEmpty(user.TradeInventoryJson) && user.TradeInventoryJson != "[]" && JsonConvert.DeserializeObject<JArray>(user.TradeInventoryJson)!.Any(x => x.Value<int>() == card.Id);
													isRecycling = !string.IsNullOrEmpty(user.RecyclingBinJson) && user.RecyclingBinJson != "[]" && JsonConvert.DeserializeObject<JArray>(user.RecyclingBinJson)!.Any(x => x.Value<int>() == card.Id);
												}
											}
										}
										using (MemoryStream stream = new(pages.Item2[index].CardBytes))
										{
											string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
											string title = $"{card.CardName}{deco}";
											await component.UpdateAsync(x =>
											{
												_cmdId ??= _cmds.First(x => x.Name == "mhhc-info").Id;
												x.Flags = MessageFlags.ComponentsV2;
												x.Attachments = new FileAttachment[] { new(stream, $"{card.CardId}.png") };
												ComponentBuilderV2 builder = new ComponentBuilderV2()
													.WithTextDisplay($"## {title}")
													.WithMediaGallery(pages.Item2
														.Where((x, y) => y == index)
														.Select((x, y) => new MediaGalleryItemProperties()
														{
															Description = card.CardId,
															Media = new UnfurledMediaItemProperties($"attachment://{card.CardId}.png")
														}))
													.WithTextDisplay($"**Name**: {card.CardName}\r\n**JP Name**: {card.CardNameJP}\r\n**ID**: {card.CardId}\r\n**Type**: {card.CardType}\r\n**Rarity**: {card.Rarity}\r\n**Decoration**: {card.Decoration.GetDescription()}\r\n**Description**: {card.CardDescription}")
													.WithTextDisplay($"-# Scans by Grender; TL by Mir, Yuwika, and MandL27. </mhhc-info:{_cmdId}> for their links!");
												if (isUsersCard)
												{
													builder = builder.WithActionRow([
														new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"{(isFavorite ? "Unfavorite" : "Favorite") + "CardID-" + card.Id}", Emote = new Emoji(isFavorite ? "❌" : "❤️"), Label = $"{(isFavorite ? "Unfavorite" : "Favorite")}" },
														new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"{(isTrading ? "RemoveTrade" : "AddTrade") + "CardID-" + card.Id}", Emote = new Emoji(isTrading ? "📉" : "📈"), Label = $"{(isTrading ? "Remove from Trade List" : "Add to Trade List")}" },
														new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"{(isRecycling ? "RemoveRecycle" : "AddRecycle") + "CardID-" + card.Id}", Emote = new Emoji(isRecycling ? "❌" : "🗑️"), Label = $"{(isRecycling ? "Remove from Recycling Bin" : "Add to Recycling Bin")}" },
														new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
													]);
												}
												if (buttons.Count > 0)
												{
													builder.WithActionRow(components: buttons);
												}
												builder.WithTextDisplay($"-# Card {index + 1}/{pages.Item2.Length}");
												x.Components = builder.Build();
											});
										}
										InteractionEngine.PaginationPages[component.Message.Id] = new Tuple<int, MHHCardPackage[]>(index, pages.Item2);
									}
								}
								break;
						}
						break;
					default: break;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		public static T GetService<T>() where T : notnull
		{
			return _services!.GetRequiredService<T>();
		}

		private static async Task Client_Log(LogMessage arg)
		{
			await Task.Run(() =>
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - {arg.Message}");
			});
		}

		private static ServiceProvider ConfigureServices()
		{
			return new ServiceCollection()
				.AddMemoryCache(delegate (MemoryCacheOptions opt) { opt.SizeLimit = 17179869184; })
				.AddSingleton<DiscordSocketClient>()
				.AddSingleton<CommandService>()
				.AddSingleton<InteractionEngine>()
				.BuildServiceProvider();
		}
	}
}
