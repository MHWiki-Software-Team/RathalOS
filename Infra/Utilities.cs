using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RathalOS.Data.Context;
using RathalOS.Data.Models;
using System.Configuration;
using System.Text;

namespace RathalOS.Infra
{
	public class Utilities
	{
		private ulong _validThread;
		private object _lock { get; set; } = new object();
		private static readonly List<ulong> _taskThreadIds = [];
		private static List<AutocompleteResult> _taskResults = [];
		public static IUser? Owner { get; set; }
		public static List<AutocompleteResult> TaskResults { get => _taskResults; set => _taskResults = value; }

		private static DiscordSocketClient? _client;
		private static ServiceProvider? _services;
		private static InteractionService? _interactionService;

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
			_interactionService = new InteractionService(_client.Rest);
			_interactionService.Log += Client_Log;
			services.GetRequiredService<CommandService>().Log += Client_Log;
			await _client.LoginAsync(TokenType.Bot, ConfigurationManager.AppSettings.Get("Token"));
			await _client.StartAsync();
			await Task.Delay(Timeout.Infinite);
		}

		private async Task OnReady()
		{
			Console.WriteLine($"Connected to these servers as '{_client!.CurrentUser.Username}': ");
			foreach (var guild in _client.Guilds)
				Console.WriteLine($"- {guild.Name}");
			await _client.SetGameAsync("The Guild authorizes you to hunt that incomplete wiki article!",
				type: ActivityType.CustomStatus);
			Console.WriteLine($"Activity set to '{_client.Activity.Name}'");
			await _interactionService!.AddModuleAsync(typeof(InteractionEngine), _services!);
			await _interactionService!.RegisterCommandsToGuildAsync(1311754186995666964);
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
			SocketForumChannel taskForum = mainGuild.ForumChannels.First(x => x.Id == Convert.ToUInt64(ConfigurationManager.AppSettings.Get("ForumID")));
			_validThread = taskForum.Id;
			_client.ThreadCreated += Client_ThreadCreated;
			_client.ThreadUpdated += Client_ThreadUpdated;
			_client.ThreadDeleted += Client_ThreadDeleted;
			_client.ThreadMemberJoined += Client_ThreadMemberJoined;
			_client.ThreadMemberLeft += Client_ThreadMemberLeft;
			_client.MessageReceived += Cient_MessageReceived;
			using (Wiki_DbContext ctxt = new())
			{
				_taskThreadIds.AddRange(ctxt.WikiTasks.Select(x => x.ChannelID));
				foreach (WikiTask task in ctxt.WikiTasks.Include(x => x.Creator).OrderBy(x => x.Title))
				{
					SocketGuildUser? usr = mainGuild.Users.FirstOrDefault(x => x.Id == task.Creator.UserID);
					string userName = usr == null ? task.Creator.Username + " (no longer in server)" : usr!.DisplayName;
					TaskResults.Add(new AutocompleteResult($"{task.Title} - from {userName}", task.Id));
				}
				TaskResults = [.. TaskResults.OrderBy(x => x.Name)];
			}
			Owner = mainGuild.Users.First(x => x.Username.Equals(ConfigurationManager.AppSettings.Get("BotOwner"), StringComparison.CurrentCultureIgnoreCase));
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
				.AddSingleton<TaskAutocomplete>()
				.BuildServiceProvider();
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1868:Unnecessary call to 'Contains(item)'", Justification = "<Pending>")]
		public static async Task DeleteTask(ulong channelId)
		{
			if (_taskThreadIds.Contains(channelId))
			{
				_taskThreadIds.Remove(channelId);
				using Wiki_DbContext ctxt = new();
				WikiTask? task = ctxt.WikiTasks.Include(x => x.Updates).FirstOrDefault(x => x.ChannelID == channelId);
				if (task != null)
				{
					ctxt.WikiTaskUpdates.RemoveRange(task.Updates);
					ctxt.AssignedTasks.RemoveRange(ctxt.AssignedTasks.Where(x => x.Assignment != null && x.Assignment.Id == task.Id));
					ctxt.WikiTasks.Remove(task);
					await ctxt.SaveChangesAsync();
					TaskResults = [.. TaskResults.Where(x => (int)x.Value != task.Id).OrderBy(x => x.Name)];
				}
			}
		}

		private Task Client_ThreadMemberLeft(SocketThreadUser arg)
		{
			if (_taskThreadIds.Contains(arg.Thread.Id))
			{
				using Wiki_DbContext ctxt = new();
				WikiUser? user = ctxt.WikiUsers.FirstOrDefault(x => x.UserID == arg.Id);
				if (user == null)
				{
					user = new WikiUser()
					{
						UserID = arg.Id,
						Username = arg.Username
					};
					ctxt.WikiUsers.Add(user);
					ctxt.SaveChanges();
				}
				WikiTask? task = ctxt.WikiTasks.Include(x => x.Updates).FirstOrDefault(x => x.ChannelID == arg.Thread.Id);
				if (task != null)
				{
					ctxt.AssignedTasks.RemoveRange(ctxt.AssignedTasks.Where(x => x.Assignee != null && x.Assignee.UserID == arg.Id));
					task.Assigned = [.. task.Assigned.Where(x => !(x.Assignee != null && x.Assignee.UserID == arg.Id))];
					ctxt.SaveChanges();
				}
			}
			return Task.CompletedTask;
		}

		private Task Client_ThreadMemberJoined(SocketThreadUser arg)
		{
			if (_taskThreadIds.Contains(arg.Thread.Id))
			{
				using Wiki_DbContext ctxt = new();
				WikiUser? user = ctxt.WikiUsers.FirstOrDefault(x => x.UserID == arg.Id);
				if (user == null)
				{
					user = new WikiUser()
					{
						UserID = arg.Id,
						Username = arg.Username
					};
					ctxt.WikiUsers.Add(user);
					ctxt.SaveChanges();
				}
				WikiTask? task = ctxt.WikiTasks.Include(x => x.Updates).FirstOrDefault(x => x.ChannelID == arg.Thread.Id);
				if (task != null)
				{
					task.Assigned.Add(new AssignedTask()
					{
						Assignee = user
					});
					ctxt.SaveChanges();
				}
			}
			return Task.CompletedTask;
		}

		private async Task Client_ThreadDeleted(Cacheable<SocketThreadChannel, ulong> arg)
		{
			await DeleteTask(arg.Id);
		}

		private async Task Client_ThreadUpdated(Cacheable<SocketThreadChannel, ulong> arg1, SocketThreadChannel arg2)
		{
			if (_taskThreadIds.Contains(arg2.Id))
			{
				using Wiki_DbContext ctxt = new();
				WikiTask task = await ctxt.WikiTasks.FirstAsync(x => x.ChannelID == arg2.Id);
				bool taskIsComplete = arg2.Name.Contains("(COMPLETED)", StringComparison.CurrentCultureIgnoreCase);
				bool taskOnHold = arg2.Name.Contains("(HOLD)", StringComparison.CurrentCultureIgnoreCase);
				bool taskArchive = arg2.Name.Contains("(ARCHIVE)", StringComparison.CurrentCultureIgnoreCase);
				bool anyChanges = false;
				IForumChannel prnt = (IForumChannel)arg2.ParentChannel;
				string[] currentTags = [.. prnt.Tags.Where(x => arg2.AppliedTags.Contains(x.Id)).Select(x => x.Name)];
				string parsedName = arg2.Name.Replace("(COMPLETED)", "", StringComparison.CurrentCultureIgnoreCase)
					.Replace("(HOLD)", "", StringComparison.CurrentCultureIgnoreCase)
					.Replace("(ARCHIVE)", "", StringComparison.CurrentCultureIgnoreCase)
					.Trim();
				bool nameChanges = false;
				if (task.Title != parsedName)
				{
					task.Title = parsedName;
					anyChanges = true;
					nameChanges = true;
				}
				string[] originalTags = task.TagsCSV.Split(",");
				if (!currentTags.SequenceEqual(task.TagsCSV.Split(",")))
				{
					task.TagsCSV = string.Join(",", currentTags);
					anyChanges = true;
				}
				if (taskIsComplete && !task.Completed)
				{
					task.Completed = true;
					task.CompletedOn = DateTime.UtcNow;
					anyChanges = true;
				}
				else if (!taskIsComplete && task.Completed)
				{
					task.Completed = false;
					task.CompletedOn = null;
					anyChanges = true;
				}
				if (taskOnHold && !task.OnHold)
				{
					task.OnHold = true;
					anyChanges = true;
				}
				else if (!taskOnHold && task.OnHold)
				{
					task.OnHold = false;
					anyChanges = true;
				}
				if (taskArchive && !task.Archived)
				{
					task.Archived = true;
					anyChanges = true;
				}
				else if (!taskArchive && task.Archived)
				{
					task.Archived = false;
					anyChanges = true;
				}
				if (anyChanges)
				{
					task.LastActive = DateTime.UtcNow;
					await ctxt.SaveChangesAsync();
					if (nameChanges)
					{
						SocketGuildUser? usr = arg2.Guild.Users.FirstOrDefault(x => x.Id == task.Creator.UserID);
						string userName = usr == null ? task.Creator.Username + " (no longer in server)" : usr!.DisplayName;
						TaskResults.First(x => (int)x.Value == task.Id).Name = $"{task.Title} - from {userName}";
						TaskResults = [.. TaskResults.OrderBy(x => x.Name)];
					}
				}
				if (!currentTags.SequenceEqual(originalTags))
				{
					bool rolesTagged = false;
					StringBuilder sb = new();
					sb.AppendLine("A tag has been added to this task! The following role has been added due to their potential interest and/or assistance needed in this thread:");
					string[] newTags = [.. currentTags.Where(x => !originalTags.Contains(x))];
					foreach (string tag in newTags)
					{
						SocketRole? role = arg2.Guild.Roles.FirstOrDefault(x => x.Name.Equals(tag, StringComparison.CurrentCultureIgnoreCase));
						if (role != null)
						{
							sb.AppendLine(MentionUtils.MentionRole(role.Id));
							rolesTagged = true;
						}
					}
					if (rolesTagged)
					{
						await arg2.SendMessageAsync(sb.ToString());
					}
				}
			}
		}

		private async Task Cient_MessageReceived(SocketMessage arg)
		{
			if (_taskThreadIds.Contains(arg.Channel.Id))
			{
				using Wiki_DbContext ctxt = new();
				WikiTask task = await ctxt.WikiTasks.FirstAsync(x => x.ChannelID == arg.Channel.Id);
				task.LastActive = DateTime.UtcNow;
				await ctxt.SaveChangesAsync();
			}
		}

		private Task Client_ThreadCreated(SocketThreadChannel arg)
		{
			if (arg.ParentChannel.ChannelType == ChannelType.Forum && arg.ParentChannel.Id == _validThread && !_taskThreadIds.Contains(arg.Id))
			{
				lock (_lock)
				{
					using Wiki_DbContext ctxt = new();
					if (!ctxt.WikiTasks.Any(x => x.ChannelID == arg.Id))
					{
						IForumChannel chnl = (IForumChannel)arg.ParentChannel;
						_taskThreadIds.Add(arg.Id);
						WikiUser? user = ctxt.WikiUsers.FirstOrDefault(x => x.UserID == arg.Owner.Id);
						if (user == null)
						{
							user = new WikiUser()
							{
								UserID = arg.Owner.Id,
								Username = arg.Owner.Username
							};
							ctxt.WikiUsers.Add(user);
							ctxt.SaveChanges();
						}
						string[] tags = [.. chnl.Tags.Where(x => arg.AppliedTags.Contains(x.Id)).Select(x => x.Name)];
						WikiTask task = new()
						{
							Title = arg.Name,
							ChannelID = arg.Id,
							TimeStamp = DateTime.UtcNow,
							LastUpdate = DateTime.UtcNow,
							LastActive = DateTime.UtcNow,
							Description = ((IMessage[])arg.GetMessagesAsync(1).FlattenAsync().Result).First().Content,
							Creator = user,
							TagsCSV = string.Join(",", tags),
							Assigned =
							[
								new AssignedTask() { Assignee = user }
							]
						};
						ctxt.WikiTasks.Add(task);
						ctxt.SaveChanges();
						SocketGuildUser? usr = arg.Guild.Users.FirstOrDefault(x => x.Id == task.Creator.UserID);
						string userName = usr == null ? task.Creator.Username + " (no longer in server)" : usr!.DisplayName;
						TaskResults.Add(new AutocompleteResult($"{task.Title} - from {userName}", task.Id));
						TaskResults = [.. TaskResults.OrderBy(x => x.Name)];
						bool rolesTagged = false;
						StringBuilder sb = new();
						sb.AppendLine("A new task has been created! The following role(s) have been notified due to their potential interest and/or assistance needed in this thread:");
						foreach (string tag in tags.Distinct())
						{
							SocketRole? role = arg.Guild.Roles.FirstOrDefault(x => x.Name.Equals(tag, StringComparison.CurrentCultureIgnoreCase));
							if (role != null)
							{
								sb.AppendLine(MentionUtils.MentionRole(role.Id));
								rolesTagged = true;
							}
						}
						if (rolesTagged)
						{
							arg.SendMessageAsync(sb.ToString());
						}
					}
				}
			}
			return Task.CompletedTask;
		}
	}
}
