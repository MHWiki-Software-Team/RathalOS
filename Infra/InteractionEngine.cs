using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Configuration;
using RathalOS.Data.Models;
using RathalOS.Data.Context;
using System.Text;

namespace RathalOS.Infra
{
	public class InteractionEngine(IServiceProvider services)
	{
		private DiscordSocketClient _client = services.GetRequiredService<DiscordSocketClient>();
		private ulong _validThread;
		private object _lock { get; set; } = new object();
		private static readonly List<ulong> _taskThreadIds = [];
		public static IUser? Owner { get; set; }

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "<Pending>")]
		public void Initialize(DiscordSocketClient client)
		{
			_client = client;
			//TODO: Register the commands as global commands instead of guild-specific
			//TODO: Remove global command registration once added so it only runs once
			SocketGuild testGuild = _client.Guilds.First(x => x.Id == 1311754186995666964);
			//TODO: Grab the appropriate forum id when the bot first runs.
			//In an ideal environment, this would be done via a setup command run by admins, but since this is a one-server bot, it really isn't worth the extra command
			SocketForumChannel taskForum = testGuild.ForumChannels.First(x => x.Id == 1438718944922828871);
			_validThread = taskForum.Id;
			_client.ThreadCreated += _client_ThreadCreated;
			_client.ThreadUpdated += _client_ThreadUpdated;
			_client.ThreadDeleted += _client_ThreadDeleted;
			_client.ThreadMemberJoined += _client_ThreadMemberJoined;
			_client.ThreadMemberLeft += _client_ThreadMemberLeft;
			_client.SlashCommandExecuted += _client_SlashCommandExecuted;
			_client.MessageReceived += _client_MessageReceived;
			//Uncomment to register commands
			//CreateCommands(testGuild);
			using (Wiki_DbContext ctxt = new())
			{
				_taskThreadIds.AddRange(ctxt.WikiTasks.Select(x => x.ChannelID));
			}
			Owner = testGuild.Users.First(x => x.Username.ToUpper() == ConfigurationManager.AppSettings.Get("BotOwner")!.ToUpper());
		}

		private void CreateCommands(SocketGuild guild)
		{
			SlashCommandOptionBuilder taskOption = new SlashCommandOptionBuilder()
				.WithName("task")
				.WithType(ApplicationCommandOptionType.Integer)
				.WithDescription("The task to target.")
				.WithRequired(false);
			using (Wiki_DbContext ctxt = new())
			{
				foreach (WikiTask task in ctxt.WikiTasks.Include(x => x.Creator).OrderBy(x => x.Title))
				{
					SocketGuildUser? usr = guild.Users.FirstOrDefault(x => x.Id == task.Creator.UserID);
					string userName = usr == null ? task.Creator.Username + " (no longer in server)" : usr!.DisplayName;
					taskOption.AddChoice($"{task.Title} - from {userName}", task.Id);
				}
			}
			SlashCommandBuilder[] cmds = [
				new SlashCommandBuilder()
					.WithName("update")
					.WithDescription("Updates task progress.")
					.AddOption("content", ApplicationCommandOptionType.String, "The content of the update.", isRequired: true),
				new SlashCommandBuilder()
					.WithName("view")
					.AddOption(taskOption)
					.WithDescription("Views task details for the specified thread. Default: current thread"),
				new SlashCommandBuilder()
					.WithName("list")
					.AddOption(new SlashCommandOptionBuilder()
						.WithName("order")
						.WithType(ApplicationCommandOptionType.String)
						.WithDescription("The column to order by (default: title).")
						.WithRequired(false)
						.AddChoice("time", "time")
						.AddChoice("title", "title")
						.AddChoice("lastupdated", "lastupdated")
						.AddChoice("status", "status"))
					.AddOption("descending", ApplicationCommandOptionType.Boolean, "Whether you'd like the order to be reversed (true) or not (false). Default is false.", isRequired: false)
					.AddOption("archived", ApplicationCommandOptionType.Boolean, "Whether you'd like to include archived tasks (true) or not (false). Default is false.", isRequired: false)
					.WithDescription("Lists all tasks."),
				new SlashCommandBuilder()
					.WithName("delete")
					.AddOption(taskOption)
					.WithDescription("Deletes task without deleting the thread."),
				new SlashCommandBuilder()
					.WithName("edit-description")
					.AddOption(taskOption)
					.AddOption("content", ApplicationCommandOptionType.String, "The content of the new description.", isRequired: true)
					.WithDescription("Updates the description of the specified task."),
				new SlashCommandBuilder()
					.WithName("export")
					.WithDescription("Exports all tasks, archived or otherwise, to an .xlsx file.")
			];
			try
			{
				IReadOnlyCollection<SocketApplicationCommand> installedCommands = _client.GetGlobalApplicationCommandsAsync().Result;
				foreach (SlashCommandBuilder cmd in cmds.Where(x => !installedCommands.Any(y => y.Name == x.Name)))
				{
					_client.CreateGlobalApplicationCommandAsync(cmd.Build());
				}
				foreach (SocketApplicationCommand command in installedCommands.Where(x => !cmds.Any(y => y.Name == x.Name)))
				{
					command.DeleteAsync().Wait();
				}
			}
			catch (HttpException e)
			{
				var json = JsonConvert.SerializeObject(e.Errors, Formatting.Indented);
				Console.WriteLine(json);
				EmbedBuilder builder = new()
				{
					Title = "An exception occurred within RathalOS!",
					Description = JsonConvert.SerializeObject(e, Formatting.Indented)
				};
				Owner.SendMessageAsync(embed: builder.Build()).Wait();
			}
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
				}
			}
		}

		private Task _client_ThreadMemberLeft(SocketThreadUser arg)
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

		private Task _client_ThreadMemberJoined(SocketThreadUser arg)
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

		private async Task _client_ThreadDeleted(Cacheable<SocketThreadChannel, ulong> arg)
		{
			await DeleteTask(arg.Id);
		}

		private async Task _client_ThreadUpdated(Cacheable<SocketThreadChannel, ulong> arg1, SocketThreadChannel arg2)
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
				if (task.Title != parsedName)
				{
					task.Title = parsedName;
					anyChanges = true;
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
				}
				if (!currentTags.SequenceEqual(originalTags))
				{
					bool rolesTagged = false;
					StringBuilder sb = new();
					sb.AppendLine("A tag has been added to this task! The following role has been added due to their potential interest and/or assistance needed in this thread:");
					string[] newTags = [..currentTags.Where(x => !originalTags.Contains(x))];
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

		private async Task _client_MessageReceived(SocketMessage arg)
		{
			if (_taskThreadIds.Contains(arg.Channel.Id)) 
			{
				using Wiki_DbContext ctxt = new();
				WikiTask task = await ctxt.WikiTasks.FirstAsync(x => x.ChannelID == arg.Channel.Id);
				task.LastActive = DateTime.UtcNow;
				await ctxt.SaveChangesAsync();
			}
		}

		private async Task _client_SlashCommandExecuted(SocketSlashCommand arg)
		{
			try
			{
				switch (arg.Data.Name)
				{
					case "update":
						await Commands.Update(arg);
						break;
					case "view":
						await Commands.View(arg);
						break;
					case "list":
						await Commands.List(arg);
						break;
					case "delete":
						await Commands.Delete(arg);
						break;
					case "export":
						await Commands.Export(arg);
						break;
					case "edit-description":
						await Commands.EditDescription(arg);
						break;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				EmbedBuilder builder = new()
				{
					Title = "An exception occurred within RathalOS!",
					Description = JsonConvert.SerializeObject(e, Formatting.Indented)
				};
				await Owner.SendMessageAsync(embed: builder.Build());
			}
		}

		private Task _client_ThreadCreated(SocketThreadChannel arg)
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
						ctxt.WikiTasks.Add(new WikiTask()
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
						});
						ctxt.SaveChanges();
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
