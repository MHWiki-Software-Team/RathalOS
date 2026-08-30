using ClosedXML.Excel;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RathalOS.Data.Context;
using RathalOS.Data.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System.Text;

namespace RathalOS.Infra
{
	public class InteractionEngine : InteractionModuleBase
	{
		private static readonly Random _rand = new();
		private static readonly string[] _monkeyNames = ["Rajang", "※Rajang", "Attack of the Rajang", "The Rajang in the Snow", "※Emerald Congalala", "Congalala", "Green Congalala", "※Congalala", "A Horde of Congalalas", "※Blangonga", "Copper Blangonga", "※Copper Blangonga", "Blangonga", "Copper Blangonga", "※Blangonga", "Blangonga", "Blangonga [L]", "Blangonga [R]", "Blangonga", "Copper Blangonga", "Blangonga", "Copper Blangonga", "Blangonga【R】", "※Blangonga"];
		private static readonly string[] _validListOrders = ["title", "time", "lastupdated", "status"];
		public static Dictionary<ulong, Tuple<int, MHHCardPackage[]>> PaginationPages { get; set; } = [];
		public static Dictionary<ulong, Tuple<int, List<MHHCard[]>>> CardListPagination { get; set; } = [];
		public static Dictionary<ulong, ulong> CardUsers { get; set; } = [];
		public static Dictionary<ulong, List<int>> OpenRecycles { get; set; } = [];
		public static Dictionary<ulong, IUserMessage> RecycleMessages { get; set; } = [];
		public static Dictionary<ulong, DateTime> MessageStoreTime { get; set; } = [];

		public static async Task<WikiUser> GetUser(IUser discordUser, Wiki_DbContext? ctxt = null, bool addIfNotExists = true)
		{
			WikiUser? user = null;
			bool dispose = ctxt == null;
			ctxt ??= new Wiki_DbContext();
			user = await ctxt.WikiUsers.Include(x => x.Cards).FirstOrDefaultAsync(x => x.UserID == discordUser.Id);
			if (addIfNotExists)
			{
				if (user == null)
				{
					user = new WikiUser()
					{
						UserID = discordUser.Id,
						Username = discordUser.Username
					};
					await ctxt.WikiUsers.AddAsync(user);
					await ctxt.SaveChangesAsync();
				}
				if (string.IsNullOrEmpty(user.WikiUsername))
				{
					using (MHWikiClient client = new())
					{
						IGuildUser guildUser = Utilities.ToIGuildUser(discordUser);
						JToken? userRecords = await client.GetWikiUsername(guildUser);
						if (userRecords != null)
						{
							JToken? validUser = userRecords.Value<JObject>("query")!.Value<JArray>("users")!.FirstOrDefault(x => x.Value<int?>("userid") != null);
							if (validUser != null)
							{
								user.WikiUsername = validUser.Value<string>("name")!;
								user.WikiUserId = validUser.Value<int>("userid");
								await ctxt.SaveChangesAsync();
							}
						}
					}
				}
			}
			if (dispose)
			{
				ctxt.Dispose();
			}
			user ??= new WikiUser()
			{
				UserID = discordUser.Id,
				Username = discordUser.Username
			};
			return user;
		}

		[SlashCommand("view", "Views task details for the specified thread. Default: current")]
		public async Task ViewCommand([Summary("task", "The task to target."), Autocomplete(typeof(TaskAutocomplete))] int? taskId = null)
		{
			using Wiki_DbContext ctxt = new();
			WikiTask? task = ctxt.WikiTasks
				.Include(x => x.Creator)
				.Include(x => x.Updates)
					.ThenInclude(x => x.Creator)
				.Include(x => x.Assigned)
					.ThenInclude(x => x.Assignee)
				.FirstOrDefault(x => taskId != null ? x.Id == taskId.Value : x.ChannelID == Context.Channel.Id);
			if (task != null)
			{
				StringBuilder sb = new();
				string activity = !task.Stale && !task.Completed && !task.NeedsUpdate && !task.OnHold ? " 💬 Active" : "";
				sb.AppendLine(@$"__*Description:*__ 
{task.Description}

__*Updates:*__");
				foreach (WikiTaskUpdate update in task.Updates.OrderBy(x => x.TimeStamp))
				{
					sb.AppendLine($"* {TimestampTag.FormatFromDateTime(update.TimeStamp, TimestampTagStyles.ShortDateTime)} - {update.Update} [{MentionUtils.MentionUser(update.Creator!.UserID)}]");
				}
				sb.AppendLine($@"
__*Status:*__{(task.Stale ? "\r\n💤	Stale" : "")}{(task.Completed ? "\r\n✅	Completed" : "")}{(task.NeedsUpdate ? "\r\n📋	Needs Update" : "")}{(task.OnHold ? "\r\n⏸️	On Hold" : "")}{activity}

__*Assignees:*__
{string.Join(", ", task.Assigned.Where(x => x.Assignee != null).Select(x => MentionUtils.MentionUser(x.Assignee!.UserID)))}");
				string channelMention = task.Title;
				bool exists = await Utilities.ChannelExists(task.ChannelID);
				if (exists)
				{
					channelMention = MentionUtils.MentionChannel(task.ChannelID);
				}
				EmbedBuilder builder = new()
				{
					Title = $"{channelMention}",
					Description = sb.ToString()
				};
				await RespondAsync(embed: builder.Build(), ephemeral: true);
			}
			else
			{
				await RespondAsync("The specified thread is not a valid forum thread, or no task exists!", ephemeral: true);
			}
		}

		[SlashCommand("update", "Updates task progress.")]
		public async Task UpdateCommand([Summary("content", "The content of the update.")] string content)
		{
			using Wiki_DbContext ctxt = new();
			WikiTask? task = await ctxt.WikiTasks.Include(x => x.Updates).FirstOrDefaultAsync(x => x.ChannelID == Context.Channel.Id);
			if (task != null)
			{
				WikiUser? user = await ctxt.WikiUsers.FirstOrDefaultAsync(x => x.UserID == Context.User.Id);
				user ??= new()
				{
					UserID = Context.User.Id,
					Username = Context.User.Username
				};
				task.Updates.Add(new WikiTaskUpdate()
				{
					Creator = user,
					TimeStamp = DateTime.UtcNow,
					Update = content
				});
				task.LastUpdate = DateTime.UtcNow;
				ctxt.Update(task);
				await ctxt.SaveChangesAsync();
				await RespondAsync("Task updated! Update: " + content);
			}
			else
			{
				await RespondAsync("The channel you're in is not a valid forum thread, or no task exists!", ephemeral: true);
			}
		}

		[SlashCommand("edit-description", "Updates the description of the specified task. Default: current")]
		public async Task EditDescription([Summary("content", "The content of the new description.")] string content, [Summary("task", "The task to target."), Autocomplete(typeof(TaskAutocomplete))] int? taskId = null)
		{
			using Wiki_DbContext ctxt = new();
			WikiTask? task = await ctxt.WikiTasks.FirstOrDefaultAsync(x => taskId != null ? x.Id == taskId.Value : x.ChannelID == Context.Channel.Id);
			if (task != null)
			{
				task.Description = content;
				await ctxt.SaveChangesAsync();
				await RespondAsync("Description updated! New description: " + content);
			}
			else
			{
				await RespondAsync("The specified thread is not a valid forum thread, or no task exists!", ephemeral: true);
			}
		}

		[SlashCommand("export", "Exports all tasks, archived or otherwise, to an .xlsx file.")]
		public async Task Export()
		{
			await DeferAsync(true);
			using Wiki_DbContext ctxt = new();
			using MemoryStream stream = new();
			using (XLWorkbook workbook = new())
			{
				IXLWorksheet current = workbook.AddWorksheet("Current");
				IXLWorksheet archived = workbook.AddWorksheet("Archived");
				IXLWorksheet all = workbook.AddWorksheet("All");
				List<WikiTask> allTasks = [..ctxt.WikiTasks.Include(x => x.Creator)
							.Include(x => x.Updates)
								.ThenInclude(x => x.Creator)
							.Include(x => x.Assigned)
								.ThenInclude(x => x.Assignee)];
				DateTime archiveIgnore = DateTime.UtcNow.AddDays(-30);
				string[] headers = ["Title", "Creator", "Status", "Created", "Last Active", "Last Updated On", "Completed On", "Description", "Last Update", "Tags CSV", "Assigned Users CSV"];
				for (int i = 1; i <= headers.Length; i++)
				{
					current.Cell(1, i).SetValue(headers[i - 1]);
					archived.Cell(1, i).SetValue(headers[i - 1]);
					all.Cell(1, i).SetValue(headers[i - 1]);
				}
				int rowCnt = 2;
				foreach (WikiTask task in allTasks.Where(x => !x.Archived && (x.CompletedOn == null || (x.CompletedOn != null && x.CompletedOn.Value > archiveIgnore))))
				{
					current.Cell(rowCnt, 1).SetValue(task.Title);
					current.Cell(rowCnt, 2).SetValue(task.Creator?.Username ?? "");
					current.Cell(rowCnt, 3).SetValue(task.Archived ? "Archived" : task.Completed ? "Completed" : task.OnHold ? "On Hold" : task.Stale ? "Stale" : task.NeedsUpdate ? "Needs Update" : "Active");
					current.Cell(rowCnt, 4).SetValue(task.TimeStamp.ToString("G"));
					current.Cell(rowCnt, 5).SetValue(task.LastActive.ToString("G"));
					current.Cell(rowCnt, 6).SetValue(task.LastUpdate.ToString("G"));
					current.Cell(rowCnt, 7).SetValue(task.CompletedOn?.ToString("G") ?? "");
					current.Cell(rowCnt, 8).SetValue(task.Description);
					current.Cell(rowCnt, 9).SetValue(task.Updates.OrderByDescending(x => x.TimeStamp).FirstOrDefault()?.Update ?? "");
					current.Cell(rowCnt, 10).SetValue(task.TagsCSV);
					current.Cell(rowCnt, 11).SetValue(string.Join(",", task.Assigned.Select(x => x.Assignee!.Username)));
					rowCnt++;
				}
				rowCnt = 2;
				foreach (WikiTask task in allTasks.Where(x => x.Archived || (x.CompletedOn != null && x.CompletedOn.Value < archiveIgnore)))
				{
					archived.Cell(rowCnt, 1).SetValue(task.Title);
					archived.Cell(rowCnt, 2).SetValue(task.Creator?.Username ?? "");
					archived.Cell(rowCnt, 3).SetValue("Archived");
					archived.Cell(rowCnt, 4).SetValue(task.TimeStamp.ToString("G"));
					archived.Cell(rowCnt, 5).SetValue(task.LastActive.ToString("G"));
					archived.Cell(rowCnt, 6).SetValue(task.LastUpdate.ToString("G"));
					archived.Cell(rowCnt, 7).SetValue(task.CompletedOn?.ToString("G") ?? "");
					archived.Cell(rowCnt, 8).SetValue(task.Description);
					archived.Cell(rowCnt, 9).SetValue(task.Updates.OrderByDescending(x => x.TimeStamp).FirstOrDefault()?.Update ?? "");
					archived.Cell(rowCnt, 10).SetValue(task.TagsCSV);
					archived.Cell(rowCnt, 11).SetValue(string.Join(",", task.Assigned.Select(x => x.Assignee!.Username)));
					rowCnt++;
				}
				rowCnt = 2;
				foreach (WikiTask task in allTasks)
				{
					all.Cell(rowCnt, 1).SetValue(task.Title);
					all.Cell(rowCnt, 2).SetValue(task.Creator?.Username ?? "");
					all.Cell(rowCnt, 3).SetValue(task.Archived ? "Archived" : task.Completed ? "Completed" : task.OnHold ? "On Hold" : task.Stale ? "Stale" : task.NeedsUpdate ? "Needs Update" : "Active");
					all.Cell(rowCnt, 4).SetValue(task.TimeStamp.ToString("G"));
					all.Cell(rowCnt, 5).SetValue(task.LastActive.ToString("G"));
					all.Cell(rowCnt, 6).SetValue(task.LastUpdate.ToString("G"));
					all.Cell(rowCnt, 7).SetValue(task.CompletedOn?.ToString("G") ?? "");
					all.Cell(rowCnt, 8).SetValue(task.Description);
					all.Cell(rowCnt, 9).SetValue(task.Updates.OrderByDescending(x => x.TimeStamp).FirstOrDefault()?.Update ?? "");
					all.Cell(rowCnt, 10).SetValue(task.TagsCSV);
					all.Cell(rowCnt, 11).SetValue(string.Join(",", task.Assigned.Select(x => x.Assignee!.Username)));
					rowCnt++;
				}
				current.Row(1).Style.Font.Bold = true;
				current.Columns().AdjustToContents();
				archived.Row(1).Style.Font.Bold = true;
				archived.Columns().AdjustToContents();
				all.Row(1).Style.Font.Bold = true;
				all.Columns().AdjustToContents();
				workbook.SaveAs(stream);
			}
			using FileAttachment attachment = new(stream, "MHWiki_Tasks_Export.xlsx");
			await DeleteOriginalResponseAsync();
			await FollowupWithFileAsync(attachment, ephemeral: true);
		}

		[SlashCommand("delete", "Deletes task without deleting the thread.")]
		public async Task Delete([Summary("task", "The task to target."), Autocomplete(typeof(TaskAutocomplete))] int? taskId = null)
		{
			using Wiki_DbContext ctxt = new();
			WikiTask? task = ctxt.WikiTasks.Include(x => x.Creator).Include(x => x.Updates).ThenInclude(x => x.Creator)
				.FirstOrDefault(x => taskId != null ? x.Id == taskId.Value : x.ChannelID == Context.Channel.Id);
			if (task != null)
			{
				await Utilities.DeleteTask(task.ChannelID);
				await RespondAsync("Task deleted!", ephemeral: true);
			}
			else
			{
				await RespondAsync("The specified thread is not a valid forum thread, or no task exists!", ephemeral: true);
			}
		}

		[SlashCommand("add-release", "Adds a release date on which to ping assignees in recurring/upcoming tasks with given tag.")]
		public async Task AddRelease(
			[Summary("tag", "The tag to search for."), Autocomplete(typeof(TagAutocomplete))] string tag,
			[Summary("date", "The release date to fire pings on.")] DateTime date)
		{
			using Wiki_DbContext ctxt = new();
			await ctxt.ReleaseDates.AddAsync(new ReleaseDates() { ReleaseDate = date, Tag = tag });
			await ctxt.SaveChangesAsync();
			await RespondAsync($"Release date added; tasks tagged with both Upcoming/Recurring and {tag} will have all assignees pinged in the appropriate thread on {date.ToShortDateString()} at noon Eastern Time.", ephemeral: true);
		}

		[SlashCommand("set-special-edition", "Set the current special edition.")]
		public async Task SetSpecialEdition([Summary("Edition", "What special edition to use."),
				Choice("Metal", (int)SpecialEditions.Metal),
				Choice("Crystal", (int)SpecialEditions.Crystal)] int edition = 0)
		{
			await DeferAsync(ephemeral: true);
			try
			{
				MHHEnvironmentVariables var = await Wiki_DbContext.GetEnvironmentVariables();
				var.CurrentSpecialEdition = (SpecialEditions)edition;
				await Wiki_DbContext.UpdateEnvironmentVariables(var);
				await FollowupAsync("Special edition changed! The selected edition will now be available for pulls and all others will be unavailable.", ephemeral: true);
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("set-event", "Set the current event.")]
		public async Task SetEvent([Summary("Event", "What event to set."),
				Choice("None", (int)Events.None),
				Choice("Double Rare Card Chance", (int)Events.DoubleRare),
				Choice("Double Special Edition Card Chance", (int)Events.DoubleSpecial),
				Choice("Double Holo Card Chance", (int)Events.DoubleHolo),
				Choice("Double Pull Addition", (int)Events.DoublePull),
				Choice("Double Booster Addition", (int)Events.DoubleBooster)] int evnt = 0)
		{
			await DeferAsync(ephemeral: true);
			try
			{
				MHHEnvironmentVariables var = await Wiki_DbContext.GetEnvironmentVariables();
				var.CurrentEvent = (Events)evnt;
				await Wiki_DbContext.UpdateEnvironmentVariables(var);
				await FollowupAsync("Event changed! The selected event will now be active and all others will be inactive.", ephemeral: true);
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("mhhc-info", "Displays MHHC server stats, currently active events and special editions, and the developer credits.")]
		public async Task MHHCInfo()
		{
			await DeferAsync(ephemeral: true);
			try
			{
				MHHEnvironmentVariables var = await Wiki_DbContext.GetEnvironmentVariables();
				await FollowupAsync(embed: new EmbedBuilder()
				{
					Title = "",
					Description = @$"***Info***
**Currently-Available Special Edition:** {var.CurrentSpecialEdition.GetDescription()}
**Current Event:** {var.CurrentEvent.GetDescription()}
**Total Pulls:** {var.TotalPulls}
**Last Holo Pull:** <t:{var.LastHolo.ToUnixTimeSeconds()}:f>
**Last Special Edition Pull:** <t:{var.LastSpecial.ToUnixTimeSeconds()}:f>
**Last Rare Pull:** <t:{var.LastRare.ToUnixTimeSeconds()}:f>
==============================================
***Credits***
Great people whose work the card functions of this bot would be worthless without!
Cards collected and scanned by: {MentionUtils.MentionUser(155723385723158528)} ([GitHub repository](https://github.com/GrenderG/MHHC_Archive))
Cards translated by: {MentionUtils.MentionUser(283414458590822401)},  {MentionUtils.MentionUser(928801586338209924)}, and {MentionUtils.MentionUser(156115426739355651)}
Testing, workshopping, spitballing, brainstorming, and other miscellaneous bot functionality assistance provided by: {MentionUtils.MentionUser(90619531428397056)}

-# Ping {MentionUtils.MentionUser(338081040134307840)} for any help/to report a bug."
				}.Build(), ephemeral: true);
				//In order: grender, mir, yuwika, mand, deck, rampagerobot
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("grant", "Grants either pulls or booster packs to a user.")]
		public async Task Grant([Summary("User", "The user to grant the pull/booster to.")] SocketGuildUser guildUser,
			[Summary("Type", "Whether to grant a pull or a booster."), Choice("Pull", "pull"), Choice("Booster", "booster")] string type,
			[Summary("Quantity", "How many to grant. Default: 1"), MinValue(1)] int qty = 1)
		{
			await DeferAsync(ephemeral: true);
			try
			{
				if (guildUser.Id == Context.User.Id)
				{
					await FollowupAsync($"You can't grant yourself pulls or boosters! Ask a different game moderator.", ephemeral: true);
					return;
				}
				using (Wiki_DbContext ctxt = new())
				{
					MHHEnvironmentVariables var = await Wiki_DbContext.GetEnvironmentVariables();
					if ((var.CurrentEvent == Events.DoublePull && type == "pull") || (var.CurrentEvent == Events.DoubleBooster && type == "booster"))
					{
						qty *= 2;
					}
					WikiUser? user = await ctxt.WikiUsers.FirstOrDefaultAsync(x => x.UserID == guildUser.Id);
					if (user != null)
					{
						switch (type)
						{
							case "pull":
								user.Pulls += qty;
								break;
							case "booster":
								user.Boosters += qty;
								break;
						}
						await ctxt.SaveChangesAsync();
						await guildUser.SendMessageAsync($"{MentionUtils.MentionUser(Context.User.Id)} has granted you {qty} {type}{(qty > 1 ? "s" : "")}!{(var.CurrentEvent == Events.DoublePull || var.CurrentEvent == Events.DoubleBooster ? " Double " + type + "s have been granted due to the current " + var.CurrentEvent.GetDescription() + " Event." : "")}");
						await FollowupAsync($"You have granted {qty} {type}{(qty > 1 ? "s" : "")} to {MentionUtils.MentionUser(guildUser.Id)}!{(var.CurrentEvent == Events.DoublePull || var.CurrentEvent == Events.DoubleBooster ? " Double " + type + "s have been granted due to the current " + var.CurrentEvent.GetDescription() + " Event." : "")}", ephemeral: true);
					}
					else
					{
						await FollowupAsync($"This user doesn't have their Wiki account linked yet! Have them run </link-user:{Utilities.GetCommandId("link-user")}> first.", ephemeral: true);
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("link-user", "Link your Discord account to your Wiki account if it was not done automatically. Case-sensitive.")]
		public async Task LinkUser([Summary("username", "Your wiki username.")] string username)
		{
			await DeferAsync(ephemeral: true);
			try
			{
				bool userFound = false;
				using (MHWikiClient client = new())
				{
					JToken? userRecords = await client.GetWikiUsername(username);
					if (userRecords != null)
					{
						JToken? validUser = userRecords.Value<JObject>("query")!.Value<JArray>("users")!.FirstOrDefault(x => x.Value<int?>("userid") != null);
						if (validUser != null)
						{
							using (Wiki_DbContext ctxt = new())
							{
								WikiUser? user = await ctxt.WikiUsers.FirstOrDefaultAsync(x => x.UserID == Context.User.Id);
								if (user == null)
								{
									user = new WikiUser()
									{
										UserID = Context.User.Id,
										Username = Context.User.Username
									};
									await ctxt.WikiUsers.AddAsync(user);
									await ctxt.SaveChangesAsync();
								}
								user.WikiUsername = validUser.Value<string>("name")!;
								user.WikiUserId = validUser.Value<int>("userid");
								await ctxt.SaveChangesAsync();
							}
							userFound = true;
						}
					}
				}
				if (userFound)
				{
					await FollowupAsync("Accounts linked! You may pull cards now.", ephemeral: true);
				}
				else
				{
					await FollowupAsync("Account not found! Please try again with the proper username.", ephemeral: true);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("trade", "Begins a trade dialogue with a chosen user. Add cards to your trade list by viewing them in /cards.")]
		public async Task Trade([Summary("User", "Which user to begin trading with.")] IUser recipientUser)
		{
			try
			{
				if (Context.User.Id == recipientUser.Id)
				{
					await RespondAsync($"You can't trade with yourself!", ephemeral: true);
					return;
				}
				await DeferAsync();
				WikiUser executor = await GetUser(Context.User);
				if (string.IsNullOrEmpty(executor.WikiUsername))
				{
					await FollowupAsync($"Your Wiki username and ID could not be linked using your Discord account! Please either change your nickname or display name in the server to match your Wiki username, or run </link-user:{Utilities.GetCommandId("link-user")}> before trying to pull cards.", ephemeral: true);
					return;
				}
				WikiUser recipient = await GetUser(recipientUser, addIfNotExists: false);
				if (string.IsNullOrEmpty(recipient.WikiUsername))
				{
					await FollowupAsync($"This user doesn't have their Wiki account linked yet! Have them run </link-user:{Utilities.GetCommandId("link-user")}> first.", ephemeral: true);
					return;
				}
				MHHOpenTrade trade = new()
				{
					Expires = DateTimeOffset.Now.AddHours(6)
				};
				using (Wiki_DbContext ctxt = new())
				{
					trade.Executor = await ctxt.WikiUsers.FirstAsync(x => x.Id == executor.Id);
					trade.Recipient = await ctxt.WikiUsers.FirstAsync(x => x.Id == recipient.Id);
					await ctxt.MHHOpenTrades.AddAsync(trade);
					await ctxt.SaveChangesAsync();
				}
				ComponentBuilderV2 builder = new ComponentBuilderV2()
					.WithTextDisplay($"## Your Trade List")
					.WithTextDisplay($"-# First, select the cards from your Trade List that you want to offer. Click the \"View\" button next to the card(s) you want to add, and then click \"Add to Trade\" on the card display. When you're ready to move on to the next step of the trade, click \"Continue\".");
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
				IUserMessage msg = await FollowupAsync(components: builder.Build(), ephemeral: false);
				if (executor.Cards != null && executor.Cards.Count > 10)
				{
					CardListPagination.Add(msg.Id, new Tuple<int, List<MHHCard[]>>(0, cardSets));
				}
				CardUsers.Add(msg.Id, Context.User.Id);
				MessageStoreTime.Add(msg.Id, DateTime.Now);
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("profile", "Displays MHHC user profiles. From here you can view stats, Fav Cards, Trade List, and Recycling Bin.")]
		public async Task Profile([Summary("User", "What user to view the profile of. Default: you.")] IUser? guildUser = null)
		{
			await DeferAsync();
			try
			{
				guildUser ??= Context.User;
				WikiUser user = await GetUser(guildUser);
				string displayName = Context.Guild.GetUserAsync(guildUser.Id).Result.DisplayName;
				if (string.IsNullOrEmpty(user.WikiUsername))
				{
					await FollowupAsync($"Your Wiki username and ID could not be linked using your Discord account! Please either change your nickname or display name in the server to match your Wiki username, or run {Utilities.GetCommandId("link-user")} before trying to pull cards.", ephemeral: true);
					return;
				}
				List<IMessageComponentBuilder> profileComponents = [
					new SectionBuilder()
					{
						Accessory = new ThumbnailBuilder(new UnfurledMediaItemProperties(guildUser.GetAvatarUrl()), "User Profile Picture"),
						Components = [
							new TextDisplayBuilder() { Content = @$"## {displayName}'s Profile
**Wiki Username:** [{user.WikiUsername}](https://monsterhunterwiki.org/wiki/User:{user.WikiUsername})
**Total Cards:** {user.Cards?.Count ?? 0}
**Current Pulls:** {user.Pulls}
**Current Boosters:** {user.Boosters}
**Lifetime Opened Pulls:** {user.LifetimePulls}
**Lifetime Opened Boosters:** {user.LifetimeBoosters}" }
						]
					},
					new SeparatorBuilder(true),
					new TextDisplayBuilder("*Favorite Cards:*")
				];
				if (!string.IsNullOrEmpty(user.FavoriteCardJson) && user.FavoriteCardJson != "[]")
				{
					profileComponents.AddRange(JsonConvert.DeserializeObject<JArray>(user.FavoriteCardJson)!
						.Select(x => x.Value<int>())
						.Select(x => user.Cards?.FirstOrDefault(y => y.Id == x))
						.Where(x => x != null)
						.Select(x => new SectionBuilder()
						{
							Accessory = new ButtonBuilder() { CustomId = $"FireCardCommandID-{x!.Id}", Label = "View", Emote = new Emoji("🃏"), Style = ButtonStyle.Primary },
							Components = [
								new TextDisplayBuilder() { Content = $"**{x.CardName}{(x.Decoration == CardDeco.Normal ? "" : " - " + x.Decoration.GetDescription())}** [{x.Rarity}]" }
							]
						}));
				}
				else
				{

					profileComponents.Add(new TextDisplayBuilder("-# This user has no favorite cards!"));
				}
				profileComponents.AddRange([
					new SeparatorBuilder(true),
					new ActionRowBuilder([
						new ButtonBuilder("View Trade List", "ViewTradeListID-" + user.Id, emote: new Emoji("🔃")),
						new ButtonBuilder("View Recycling Bin", "ViewRecyclingBinID-" + user.Id, emote: new Emoji("🗑️")),
						new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
					])
				]);
				await FollowupAsync(components: new ComponentBuilderV2()
					.WithContainer(new ContainerBuilder()
						.WithComponents(profileComponents)
						.WithAccentColor(Discord.Color.LightOrange))
					.Build());
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("booster", "Open a Booster Pack of 10 cards, provided you have one. Use /profile to see # of available boosters.")]
		public async Task Booster([Summary("Series", "What series the cards should be in. Default: Any"), MaxValue(8),
			Choice("Any", 0),
			Choice("Series 1", 1),
			Choice("Series 2", 2),
			Choice("Series 3", 3),
			Choice("Series 4", 4),
			Choice("Series 5", 5),
			Choice("Series 6", 6),
			Choice("Series 7", 7),
			Choice("Series 8", 8),] int series = 0)
		{
			await DeferAsync();
			try
			{
				WikiUser user = await GetUser(Context.User);
				if (string.IsNullOrEmpty(user.WikiUsername))
				{
					await FollowupAsync($"Your Wiki username and ID could not be linked using your Discord account! Please either change your nickname or display name in the server to match your Wiki username, or run {Utilities.GetCommandId("link-user")} before trying to pull cards.", ephemeral: true);
					return;
				}
#if DEBUG == false
				if (user.Boosters < 1)
				{
					await FollowupAsync($"You don't have enough boosters! Currently, you have {user.Boosters} boosters.");
					return;
				}
#endif
				if (series == 0)
				{
					series = _rand.Next(1, 9);
				}
				MHHCardPackage[] pkgs = new MHHCardPackage[10];
				MHHEnvironmentVariables var = await Wiki_DbContext.GetEnvironmentVariables();
				for (int i = 0; i < 10; i++)
				{
					//max of 8 for base or starter
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
					BoosterRarity rarity = BoosterRarity.Common;
					if (i > 3 && i < 7)
					{
						rarity = BoosterRarity.Uncommon;
					}
					else if (i >= 7)
					{
						rarity = BoosterRarity.Foil;
					}
					MHHCardPackage pkg = await MHHCardPackage.BuildCardPackage(true, series, MHHCardPackage.RollCardId(series, boosterRarity: rarity), boosterRarity: rarity);
					if (_monkeyNames.Contains(pkg.Card.CardName))
					{
						var.Monkeys++;
						await Utilities.SetMonkeys(var.Monkeys);
					}
					var.TotalPulls++;
					switch (pkg.Card.Decoration)
					{
						case CardDeco.Normal: break;
						case CardDeco.Holo:
							var.LastHolo = DateTimeOffset.Now;
							break;
						case CardDeco.Negative:
						case CardDeco.Grayscale:
						case CardDeco.Sepia:
							var.LastRare = DateTimeOffset.Now;
							break;
						case (CardDeco deco) when deco >= CardDeco.Iron && deco < CardDeco.Unused_NegativeTrophy:
							var.LastSpecial = DateTimeOffset.Now;
							break;
					}
					pkgs[i] = pkg;
				}
				using (Wiki_DbContext ctxt = new())
				{
					MHHEnvironmentVariables dbVar = await ctxt.MHHEnvironmentVariables.FirstAsync();
					dbVar.TotalPulls = var.TotalPulls;
					dbVar.Monkeys = var.Monkeys;
					dbVar.LastHolo = var.LastHolo;
					dbVar.LastRare = var.LastRare;
					dbVar.LastSpecial = var.LastSpecial;
					WikiUser dbUser = await ctxt.WikiUsers.FirstAsync(x => x.Id == user.Id);
#if DEBUG == false
					dbUser.Boosters--;
#endif
					dbUser.LifetimePulls += 10;
					dbUser.LifetimeBoosters++;
					dbUser.Cards ??= [];
					dbUser.Cards.AddRange(pkgs.Select(x => x.Card));
					await ctxt.SaveChangesAsync();
					foreach (MHHCardPackage pkg in pkgs)
					{
						MHHCardPackage.TryAddCardToCache(pkg.Card.Guid, pkg);
					}
				}
				byte[] booster = [];
				switch (series)
				{
					case 1:
						booster = CardResources.B01gif;
						break;
					case 2:
						booster = CardResources.B02gif;
						break;
					case 3:
						booster = CardResources.B03gif;
						break;
					case 4:
						booster = CardResources.B04gif;
						break;
					case 5:
						booster = CardResources.B05gif;
						break;
					case 6:
						booster = CardResources.B06gif;
						break;
					case 7:
						booster = CardResources.B07gif;
						break;
					case 8:
						booster = CardResources.B08gif;
						break;
				}
				using (MemoryStream stream = new(booster))
				{
					ulong cmdId = Utilities.GetCommandId("mhhc-info");
					IUserMessage msg = await FollowupWithFileAsync(attachment: new(stream, $"booster.gif"), components: new ComponentBuilderV2()
						.WithTextDisplay($"## SERIES {series} Booster Pack")
						.WithTextDisplay("-# You can close this at any time; your cards have already been added by the time you see this message.")
						.WithMediaGallery([new MediaGalleryItemProperties() { Description = "it is a mysteryyyy", Media = new UnfurledMediaItemProperties($"attachment://booster.gif") }])
						.WithTextDisplay($"-# Scans by Grender; TL by Mir, Yuwika, and MandL27. </mhhc-info:{cmdId}> for their links!")
						.WithActionRow(components: new List<ButtonBuilder>()
						{
							new() {
								CustomId = "NextCardButton",
								Emote = new Emoji("🔓"),
								Label = "Open",
								Style = ButtonStyle.Primary
							},
							new() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
						}).Build(), ephemeral: false);
					PaginationPages.Add(msg.Id, new Tuple<int, MHHCardPackage[]>(-1, pkgs));
					CardUsers.Add(msg.Id, Context.User.Id);
					MessageStoreTime.Add(msg.Id, DateTime.Now);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("search-cards", "Searches your card inventory for cards matching the specified criteria. Can combine criteria.")]
		public async Task SearchCards(
			[Summary("Name", "Return cards whose name is like this name.")] string? name = null,
			[Summary("Type", "Return cards of this type."),
				Choice("Guild Monster", "Guild Monster"),
				Choice("Target Monster", "Target Monster"),
				Choice("Hunter", "Hunter"),
				Choice("Event", "Event"),
				Choice("Quest", "Quest"),
				Choice("Palico", "Palico"),
				Choice("Buddy", "Buddy")] string? type = null,
			[Summary("Rank", "Return cards with this rank."),
				Choice("None", "None"),
				Choice("1★", "1★"),
				Choice("2★", "2★"),
				Choice("3★", "3★"),
				Choice("4★", "4★"),
				Choice("5★", "5★"),
				Choice("6★", "6★"),
				Choice("7★", "7★"),
				Choice("8★", "8★"),
				Choice("9★", "9★"),] string? rank = null,
			[Summary("Series", "Return cards of this series.")] int? series = null,
			[Summary("Number", "Return cards with this #. Example: Card ID = B07-[04]. The # in brackets is the card # you specify.")] int? cardNumber = null,
			[Summary("Rarity", "Return cards with this rarity."),
				Choice("Common", (int)CardRarity.Common),
				Choice("Uncommon", (int)CardRarity.Common),
				Choice("Rare", (int)CardRarity.Common),
				Choice("Ultra Rare", (int)CardRarity.Common)] int? rarity = null,
			[Summary("Decoration", "Return cards with this decoration."),
				Choice("Normal", (int)CardDeco.Normal),
				Choice("🌈 Holographic 🌈", (int)CardDeco.Holo),
				Choice("🔳 Negative 🔳", (int)CardDeco.Negative),
				Choice("🔲 Grayscale 🔲", (int)CardDeco.Grayscale),
				Choice("🌅 Sepia 🌅", (int)CardDeco.Sepia),
				Choice("🏆 Trophy 🏆", (int)CardDeco.Trophy),
				Choice("⚙️ Iron ⚙️", (int)CardDeco.Iron),
				Choice("⚙️ Dreamcore ⚙️", (int)CardDeco.Dreamcore),
				Choice("⚙️ Artian ⚙️", (int)CardDeco.Artian),
				Choice("⚙️ Aquacore ⚙️", (int)CardDeco.Aquacore),
				Choice("⚙️ Eltalite ⚙️", (int)CardDeco.Eltalite),
				Choice("⚙️ Dragoncore ⚙️", (int)CardDeco.Dragoncore),
				Choice("💎 Firecell 💎", (int)CardDeco.Firecell),
				Choice("💎 Machalite 💎", (int)CardDeco.Machalite),
				Choice("💎 Dragonite 💎", (int)CardDeco.Dragonite),
				Choice("💎 Icium 💎", (int)CardDeco.Icium),
				Choice("💎 Deepsea 💎", (int)CardDeco.Deepsea),
				Choice("💎 Shadowcore 💎", (int)CardDeco.Shadowcore)] int? deco = null)
		{
			await DeferAsync();
			try
			{
				WikiUser user = await GetUser(Context.User);
				if (string.IsNullOrEmpty(user.WikiUsername))
				{
					await FollowupAsync($"Your Wiki username and ID could not be linked using your Discord account! Please either change your nickname or display name in the server to match your Wiki username, or run {Utilities.GetCommandId("link-user")} before trying to pull cards.", ephemeral: true);
					return;
				}
				List<MHHCard> cards = [];
				ComponentBuilderV2 builder = new ComponentBuilderV2()
					.WithTextDisplay($"## Card Inventory");
				if (user.Cards != null)
				{
					cards = [..user.Cards.OrderBy(x => x.CardName).ThenBy(x => x.Rarity).ThenBy(x => x.Decoration).Where(x =>
						(name != null && (x.CardName.Contains(name, StringComparison.CurrentCultureIgnoreCase) || name.Contains(x.CardName, StringComparison.CurrentCultureIgnoreCase))) ||
						(type != null && x.CardType == type) ||
						(rank != null && (string.IsNullOrEmpty(x.Rank) && rank == "None" || x.Rank == rank)) ||
						(series != null && x.CardId.StartsWith($"B{series:00}")) ||
						(cardNumber != null && x.CardId.EndsWith(cardNumber.Value.ToString("00"))) ||
						(rarity != null && x.Rarity == (CardRarity)rarity.Value) ||
						(deco != null && x.Decoration == (CardDeco)deco)
					)];
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
						components.Add(new TextDisplayBuilder("You don't own any cards matching these criteria."));
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
				IUserMessage msg = await FollowupAsync(components: builder.Build(), ephemeral: false);
				CardListPagination.Add(msg.Id, new Tuple<int, List<MHHCard[]>>(0, cardSets));
				MessageStoreTime.Add(msg.Id, DateTime.Now);
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("card", "Display a single card.")]
		public async Task Card([Summary("id", "The card's ID.")] int id)
		{
			await DeferAsync();
			try
			{
				MHHCard? card = null;
				if (card == null)
				{
					await FollowupAsync("You don't have a card with this ID!", ephemeral: true);
					return;
				}
				bool isUsersCard = false;
				bool isFavorite = false;
				bool isTrading = false;
				bool isRecycling = false;
				using (Wiki_DbContext ctxt = new())
				{
					card = await ctxt.MHHCards.FirstAsync(x => x.Id == id);
					WikiUser? user = await ctxt.WikiUsers.FirstOrDefaultAsync(x => x.UserID == Context.User.Id && x.Cards != null && x.Cards.Any(y => y.Id == id));
					isUsersCard = user != null;
					if (user != null)
					{
						isFavorite = !string.IsNullOrEmpty(user.FavoriteCardJson) && user.FavoriteCardJson != "[]" && JsonConvert.DeserializeObject<JArray>(user.FavoriteCardJson)!.Any(x => x.Value<int>() == id);
						isTrading = !string.IsNullOrEmpty(user.TradeInventoryJson) && user.TradeInventoryJson != "[]" && JsonConvert.DeserializeObject<JArray>(user.TradeInventoryJson)!.Any(x => x.Value<int>() == id);
						isRecycling = !string.IsNullOrEmpty(user.RecyclingBinJson) && user.RecyclingBinJson != "[]" && JsonConvert.DeserializeObject<JArray>(user.RecyclingBinJson)!.Any(x => x.Value<int>() == id);
					}
				}
				MHHCardPackage pkg = await MHHCardPackage.BuildCardPackage(card);
				using (MemoryStream stream = new(pkg.CardBytes))
				{
					ulong cmdId = Utilities.GetCommandId("mhhc-info");
					string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
					string title = $"{card.CardName}{deco}";
					ComponentBuilderV2 builder = new ComponentBuilderV2()
						.WithTextDisplay($"## {title}")
						.WithMediaGallery([new MediaGalleryItemProperties()
						{
							Description = card.CardId,
							Media = new UnfurledMediaItemProperties($"attachment://{card.CardId}.png")
						}])
						.WithTextDisplay($"**Name**: {card.CardName}\r\n**JP Name**: {card.CardNameJP}\r\n**ID**: {card.CardId}\r\n**Type**: {card.CardType}\r\n**Rarity**: {card.Rarity}\r\n**Decoration**: {card.Decoration.GetDescription()}\r\n**Description**: {card.CardDescription}")
						.WithTextDisplay($"-# Scans by Grender; TL by Mir, Yuwika, and MandL27. </mhhc-info:{cmdId}> for their links!");
					if (isUsersCard)
					{
						builder = builder.WithActionRow([
							new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"{(isFavorite ? "Unfavorite" : "Favorite") + "CardID-" + id}", Emote = new Emoji(isFavorite ? "❌" : "❤️"), Label = $"{(isFavorite ? "Unfavorite" : "Favorite")}" },
							new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"{(isTrading ? "RemoveTrade" : "AddTrade") + "CardID-" + id}", Emote = new Emoji(isTrading ? "📉" : "📈"), Label = $"{(isTrading ? "Remove from Trade List" : "Add to Trade List")}" },
							new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"{(isRecycling ? "RemoveRecycle" : "AddRecycle") + "CardID-" + id}", Emote = new Emoji(isRecycling ? "❌" : "🗑️"), Label = $"{(isRecycling ? "Remove from Recycling Bin" : "Add to Recycling Bin")}" },
							new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
						]);
					}
					IUserMessage msg = await FollowupWithFileAsync(attachment: new(stream, $"{card.CardId}.gif"), components: builder.Build(), ephemeral: false);
					CardUsers.Add(msg.Id, Context.User.Id);
					MessageStoreTime.Add(msg.Id, DateTime.Now);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("cards", "Lists your entire card inventory. View cards to Favorite, Add to Trade List, or Add to Recycling Bin")]
		public async Task Cards()
		{
			await DeferAsync();
			try
			{
				WikiUser user = await GetUser(Context.User)!;
				if (string.IsNullOrEmpty(user.WikiUsername))
				{
					await FollowupAsync($"Your Wiki username and ID could not be linked using your Discord account! Please either change your nickname or display name in the server to match your Wiki username, or run {Utilities.GetCommandId("link-user")} before trying to pull cards.", ephemeral: true);
					return;
				}
				ComponentBuilderV2 builder = new ComponentBuilderV2()
					.WithTextDisplay($"## Card Inventory");
				if (user.Cards != null)
				{
					List<IMessageComponentBuilder> components = [];
					foreach (MHHCard card in user.Cards.OrderBy(x => x.CardName).ThenBy(x => x.Rarity).ThenBy(x => x.Decoration).Take(10))
					{
						string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
						components.Add(new SectionBuilder()
						{
							Accessory = new ButtonBuilder() { CustomId = $"FireCardCommandID-{card.Id}", Label = "View", Emote = new Emoji("🃏"), Style = ButtonStyle.Primary },
							Components = [
								new TextDisplayBuilder() { Content = $"**{card.CardName}{deco}** [{card.Rarity}]" }
							]
						});
					}
					if (components.Count == 0)
					{
						components.Add(new TextDisplayBuilder("You don't own any cards."));
					}
					builder = builder.WithContainer(new ContainerBuilder(components) { AccentColor = Discord.Color.LightOrange });
					if (user.Cards.Count > 10)
					{
						builder = builder.WithActionRow([
							new ButtonBuilder() {
							CustomId = "NextListButton",
							Emote = new Emoji("➡️"),
							Label = "Next",
							Style = ButtonStyle.Primary
						},
						new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }]);
					}
					else
					{
						builder = builder.WithActionRow([
							new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }]);
					}
				}
				List<MHHCard[]> cardSets = [];
				if (user.Cards != null && user.Cards.Count > 10)
				{
					int cntr = 0;
					while (cntr <= user.Cards.Count)
					{
						MHHCard[] toAdd = [.. user.Cards.OrderBy(x => x.CardName).ThenBy(x => x.Rarity).ThenBy(x => x.Decoration).Skip(cntr).Take(10)];
						if (toAdd.Length > 0)
						{
							cardSets.Add(toAdd);
						}
						cntr += 10;
					}
				}
				builder.WithTextDisplay($"-# Page 1/{(cardSets.Count == 0 ? 1 : cardSets.Count)}");
				IUserMessage msg = await FollowupAsync(components: builder.Build(), ephemeral: false);
				if (user.Cards != null && user.Cards.Count > 10)
				{
					CardListPagination.Add(msg.Id, new Tuple<int, List<MHHCard[]>>(0, cardSets));
				}
				CardUsers.Add(msg.Id, Context.User.Id);
				MessageStoreTime.Add(msg.Id, DateTime.Now);
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("super-pull", "Redeem 10 available pulls for a single, guaranteed Foil card. ALL 10 PULLS WILL BE LOST INSTANTLY.")]
		public async Task SuperPull([Summary("Quantity", "The # of Foil cards to pull. Each Foil requires redeeming 10 pulls and occurs instantly. Default: 1."), MinValue(1), MaxValue(10)] int qty = 1)
		{
			await DeferAsync();
			try
			{
				WikiUser user = await GetUser(Context.User);
				if (string.IsNullOrEmpty(user.WikiUsername))
				{
					await FollowupAsync($"Your Wiki username and ID could not be linked using your Discord account! Please either change your nickname or display name in the server to match your Wiki username, or run {Utilities.GetCommandId("link-user")} before trying to pull cards.", ephemeral: true);
					return;
				}
#if DEBUG == false
				if (user.Pulls < qty * 10)
				{
					await FollowupAsync($"You don't have enough pulls! Currently, you have {user.Pulls} pulls.");
					return;
				}
#endif
				await PullCards(qty, user, true);
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		[SlashCommand("pull", "Pull a random MHHC card, provided you have pulls to use. Use /profile to see # of available pulls.")]
		public async Task Pull([Summary("Quantity", "How many cards to pull. Default: 1"), MinValue(1), MaxValue(10)] int qty = 1)
		{
			await DeferAsync();
			try
			{
				WikiUser user = await GetUser(Context.User);
				if (string.IsNullOrEmpty(user.WikiUsername))
				{
					await FollowupAsync($"Your Wiki username and ID could not be linked using your Discord account! Please either change your nickname or display name in the server to match your Wiki username, or run {Utilities.GetCommandId("link-user")} before trying to pull cards.", ephemeral: true);
					return;
				}
#if DEBUG == false
				if (user.Pulls < qty)
				{
					await FollowupAsync($"You don't have enough pulls! Currently, you have {user.Pulls} pulls.");
					return;
				}
#endif
				await PullCards(qty, user);
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		public static async Task RunRecycle(IDiscordInteraction arg)
		{
			await arg.DeferAsync();
			List<int> cardIds = OpenRecycles[arg.User.Id];
			if (cardIds.Count < 5)
			{
				await arg.FollowupAsync("You must select 5 cards to recycle!", ephemeral: true);
				return;
			}
			WikiUser? user = null;
			using (Wiki_DbContext ctxt = new())
			{
				user = await ctxt.WikiUsers.Include(x => x.Cards).FirstAsync(x => x.UserID == arg.User.Id);
			}
			List<MHHCard> cards = [];
			if (user.Cards != null)
			{
				cards = [.. user.Cards.Where(x => cardIds.Contains(x.Id))];
			}
			MHHCard template = cards[_rand.Next(0, cards.Count)];
			MHHCardPackage? pkg = null;
			int[] allSeries = [..cards.Select(x => Convert.ToInt32(x.CardId[1..x.CardId.IndexOf('-')]))];
			int series = allSeries[_rand.Next(0, allSeries.Length)];
			if ((_rand.Next(0, 3) == 0 || template.Rarity == CardRarity.Ultra) && template.Decoration != CardDeco.Holo)
			{
				pkg = await MHHCardPackage.BuildCardPackage(series, cardDeco: template.Decoration);
			}
			else
			{
				pkg = await MHHCardPackage.BuildCardPackage(series, cardRarity: template.Rarity);
			}
			MHHEnvironmentVariables var = await Wiki_DbContext.GetEnvironmentVariables(); 
			var.TotalPulls++;
			if (_monkeyNames.Contains(pkg.Card.CardName))
			{
				var.Monkeys++;
				await Utilities.SetMonkeys(var.Monkeys);
			}
			switch (pkg.Card.Decoration)
			{
				case CardDeco.Normal: break;
				case CardDeco.Holo:
					var.LastHolo = DateTimeOffset.Now;
					break;
				case CardDeco.Negative:
				case CardDeco.Grayscale:
				case CardDeco.Sepia:
					var.LastRare = DateTimeOffset.Now;
					break;
				case (CardDeco deco) when deco >= CardDeco.Iron && deco < CardDeco.Unused_NegativeTrophy:
					var.LastSpecial = DateTimeOffset.Now;
					break;
			}
			using (Wiki_DbContext ctxt = new())
			{
				MHHEnvironmentVariables dbVar = await ctxt.MHHEnvironmentVariables.FirstAsync();
				dbVar.TotalPulls = var.TotalPulls;
				dbVar.Monkeys = var.Monkeys;
				dbVar.LastHolo = var.LastHolo;
				dbVar.LastRare = var.LastRare;
				dbVar.LastSpecial = var.LastSpecial;
				dbVar.Monkeys = var.Monkeys;
				WikiUser dbUser = await ctxt.WikiUsers.Include(x => x.Cards).FirstAsync(x => x.Id == user.Id);
				dbUser.LifetimePulls += 1;
				dbUser.Cards ??= [];
				int[] recycleIds = [];
				if (!string.IsNullOrEmpty(user.RecyclingBinJson) && user.RecyclingBinJson != "[]")
				{
					recycleIds = [.. JsonConvert.DeserializeObject<JArray>(user.RecyclingBinJson)!.Select(x => x.Value<int>())];
				}
				dbUser.RecyclingBinJson = JsonConvert.SerializeObject(recycleIds.Where(x => !cardIds.Contains(x)).ToArray());
				int[] favoriteIds = [];
				if (!string.IsNullOrEmpty(user.FavoriteCardJson) && user.FavoriteCardJson != "[]")
				{
					favoriteIds = [.. JsonConvert.DeserializeObject<JArray>(user.FavoriteCardJson)!.Select(x => x.Value<int>())];
				}
				dbUser.FavoriteCardJson = JsonConvert.SerializeObject(favoriteIds.Where(x => !cardIds.Contains(x)).ToArray());
				int[] tradeIds = [];
				if (!string.IsNullOrEmpty(user.TradeInventoryJson) && user.TradeInventoryJson != "[]")
				{
					tradeIds = [.. JsonConvert.DeserializeObject<JArray>(user.TradeInventoryJson)!.Select(x => x.Value<int>())];
				}
				dbUser.TradeInventoryJson = JsonConvert.SerializeObject(tradeIds.Where(x => !cardIds.Contains(x)).ToArray());
				dbUser.Cards = [..dbUser.Cards.Where(x => !cardIds.Contains(x.Id))];
				dbUser.Cards.Add(pkg.Card);
				await ctxt.SaveChangesAsync();
			}
			using (MemoryStream stream = new(CardResources.pullgif))
			{
				ulong cmdId = Utilities.GetCommandId("mhhc-info");
				IUserMessage msg = await arg.FollowupWithFileAsync(attachment: new(stream, $"pull.gif"), components: new ComponentBuilderV2()
					.WithTextDisplay($"## What will you pull?")
					.WithTextDisplay("-# You can close this at any time; your cards have already been added by the time you see this message.")
					.WithMediaGallery([new MediaGalleryItemProperties() { Description = "it is a mysteryyyy", Media = new UnfurledMediaItemProperties($"attachment://pull.gif") }])
					.WithTextDisplay($"-# Scans by Grender; TL by Mir, Yuwika, and MandL27. </mhhc-info:{cmdId}> for their links!")
					.WithActionRow(components: new List<ButtonBuilder>()
					{
						new() {
							CustomId = "NextCardButton",
							Emote = new Emoji("🔓"),
							Label = "Open",
							Style = ButtonStyle.Primary
						},
						new() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
					}).Build(), ephemeral: false);
				PaginationPages.Add(msg.Id, new Tuple<int, MHHCardPackage[]>(-1, [pkg]));
				CardUsers.Add(msg.Id, arg.User.Id);
				MessageStoreTime.Add(msg.Id, DateTime.Now);
				RecycleMessages.Remove(arg.User.Id);
				OpenRecycles.Remove(arg.User.Id);
			}
		}

		[SlashCommand("recycle", "Opens your Recycling Bin. From here, select 5 cards to recycle for a single, rarer card.")]
		public async Task Recycle([Summary("Random", "Instantly recycles 5 random cards from your bin when specifying a True parameter. Default: False.")] bool random = false)
		{
			try
			{
				WikiUser user = await GetUser(Context.User);
				if (string.IsNullOrEmpty(user.WikiUsername))
				{
					await RespondAsync($"Your Wiki username and ID could not be linked using your Discord account! Please either change your nickname or display name in the server to match your Wiki username, or run </link-user:{Utilities.GetCommandId("link-user")}> before trying to pull cards.", ephemeral: true);
					return;
				}
				int[] recycleIds = [];
				if (!string.IsNullOrEmpty(user.RecyclingBinJson) && user.RecyclingBinJson != "[]")
				{
					recycleIds = [.. JsonConvert.DeserializeObject<JArray>(user.RecyclingBinJson)!.Select(x => x.Value<int>())];
				}
				if (random && recycleIds.Length < 5)
				{
					await RespondAsync($"You need to add at least 5 cards to your Recycling Bin first.", ephemeral: true);
					return;
				}
				else if (random)
				{
					List<int> toRecycle = [];
					for (int i = 0; i < 5; i++)
					{
						int selectedId = recycleIds[_rand.Next(0, recycleIds.Length)];
						toRecycle.Add(selectedId);
						recycleIds = [.. recycleIds.Where(x => x != selectedId)];
					}
					OpenRecycles[Context.User.Id] = toRecycle;
					await RunRecycle(Context.Interaction);
					return;
				}
				await DeferAsync();
				ComponentBuilderV2 builder = new ComponentBuilderV2()
					.WithTextDisplay($"## Your Recycling Bin")
					.WithTextDisplay($"-# First, select the cards from your Recycling Bin that you want to trade in. You **must** select 5 cards to recycle. A card will be randomly selected from the group of 5 to be the template for the recycle. The resulting card will be one rarity higher, one decoration level higher, or both one rarity and one decoration level higher, than the template card. Thus, a higher average rarity and decoration level of your bin will increase your odds for receiving a card of the next highest rarity and/or decoration level. The resulting card will *always* be an upgrade from the lowest rarity card provided. When all 5 cards are selected, click the \"Recycle\" button to receive your card.");
				List<MHHCard> recycleCards = [];
				if (user.Cards != null)
				{
					recycleCards = [.. user.Cards.OrderBy(x => x.CardName).ThenBy(x => x.Rarity).ThenBy(x => x.Decoration).Where(x => recycleIds.Contains(x.Id))];
					List<IMessageComponentBuilder> components = [];
					foreach (MHHCard card in recycleCards.Take(10))
					{
						string deco = card.Decoration == CardDeco.Normal ? "" : " - " + card.Decoration.GetDescription();
						components.Add(new SectionBuilder()
						{
							Accessory = new ButtonBuilder() { CustomId = $"FireRecycleCardCommandID-{card.Id}", Label = "View", Emote = new Emoji("🃏"), Style = ButtonStyle.Primary },
							Components = [
								new TextDisplayBuilder() { Content = $"**{card.CardName}{deco}** [{card.Rarity}]" }
							]
						});
					}
					if (components.Count == 0)
					{
						components.Add(new TextDisplayBuilder("You don't have any cards in your Recycling Bin."));
					}
					builder = builder.WithContainer(new ContainerBuilder(components) { AccentColor = Discord.Color.LightOrange });
					if (recycleCards.Count > 10)
					{
						builder = builder.WithActionRow([
							new ButtonBuilder() {
								CustomId = "NextRecycleListButton",
								Emote = new Emoji("➡️"),
								Label = "Next",
								Style = ButtonStyle.Primary
							},
							new ButtonBuilder() {
								CustomId = "RecycleButton",
								Emote = new Emoji("🗑️"),
								Label = "Recycle",
								Style = ButtonStyle.Primary,
							},
							new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"DismissRecycle", Emote = new Emoji("🔚"), Label = $"Close",  }
						]);
					}
					else
					{
						builder = builder.WithActionRow([
							new ButtonBuilder() {
								CustomId = "RecycleButton",
								Emote = new Emoji("🗑️"),
								Label = "Recycle",
								Style = ButtonStyle.Primary
							},
							new ButtonBuilder() { Style = ButtonStyle.Primary, CustomId = $"DismissRecycle", Emote = new Emoji("🔚"), Label = $"Close" }
						]);
					}
				}
				List<MHHCard[]> cardSets = [];
				if (recycleCards != null && recycleCards.Count > 10)
				{
					int cntr = 0;
					while (cntr <= recycleCards.Count)
					{
						MHHCard[] toAdd = [.. recycleCards.Skip(cntr).Take(10)];
						if (toAdd.Length > 0)
						{
							cardSets.Add(toAdd);
						}
						cntr += 10;
					}
				}
				builder.WithTextDisplay($"-# Page 1/{(cardSets.Count == 0 ? 1 : cardSets.Count)}");
				IUserMessage msg = await FollowupAsync(components: builder.Build());
				if (user.Cards != null && user.Cards.Count > 10)
				{
					CardListPagination.Add(msg.Id, new Tuple<int, List<MHHCard[]>>(0, cardSets));
				}
				CardUsers.Add(msg.Id, Context.User.Id);
				MessageStoreTime.Add(msg.Id, DateTime.Now);
				if (!OpenRecycles.TryAdd(Context.User.Id, []))
				{
					OpenRecycles[Context.User.Id] = [];
				}
				if (!RecycleMessages.TryAdd(Context.User.Id, msg))
				{
					RecycleMessages[Context.User.Id] = msg;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}

		public async Task PullCards(int qty, WikiUser user, bool forceFoil = false)
		{
			MHHCardPackage[] pkgs = new MHHCardPackage[qty];
			MHHEnvironmentVariables var = await Wiki_DbContext.GetEnvironmentVariables();
			for (int i = 0; i < qty; i++)
			{
				//max of 8 for base or starter
				int series = _rand.Next(1, 9);
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
				MHHCardPackage pkg = await MHHCardPackage.BuildCardPackage(true, series, MHHCardPackage.RollCardId(series, boosterRarity: forceFoil ? BoosterRarity.Foil : null), boosterRarity: forceFoil ? BoosterRarity.Foil : null);
				var.TotalPulls++;
				if (_monkeyNames.Contains(pkg.Card.CardName))
				{
					var.Monkeys++;
					await Utilities.SetMonkeys(var.Monkeys);
				}
				switch (pkg.Card.Decoration)
				{
					case CardDeco.Normal: break;
					case CardDeco.Holo:
						var.LastHolo = DateTimeOffset.Now;
						break;
					case CardDeco.Negative:
					case CardDeco.Grayscale:
					case CardDeco.Sepia:
						var.LastRare = DateTimeOffset.Now;
						break;
					case (CardDeco deco) when deco >= CardDeco.Iron && deco < CardDeco.Unused_NegativeTrophy:
						var.LastSpecial = DateTimeOffset.Now;
						break;
				}
				pkgs[i] = pkg;
			}
			using (Wiki_DbContext ctxt = new())
			{
				MHHEnvironmentVariables dbVar = await ctxt.MHHEnvironmentVariables.FirstAsync();
				dbVar.TotalPulls = var.TotalPulls;
				dbVar.Monkeys = var.Monkeys;
				dbVar.LastHolo = var.LastHolo;
				dbVar.LastRare = var.LastRare;
				dbVar.LastSpecial = var.LastSpecial;
				dbVar.Monkeys = var.Monkeys;
				WikiUser dbUser = await ctxt.WikiUsers.FirstAsync(x => x.Id == user.Id);
#if DEBUG == false
					dbUser.Pulls -= qty * (forceFoil ? 10 : 1);
#endif
				dbUser.LifetimePulls += qty;
				dbUser.Cards ??= [];
				dbUser.Cards.AddRange(pkgs.Select(x => x.Card));
				await ctxt.SaveChangesAsync();
			}
			using (MemoryStream finalStream = new())
			using (MemoryStream stream = new(CardResources.pullgif))
			{
				if (forceFoil)
				{
					SixLabors.ImageSharp.Image baseImage = SixLabors.ImageSharp.Image.Load(stream);
					using (SixLabors.ImageSharp.Image holoBmp = _rand.Next(1, 5) switch
					{
						1 => SixLabors.ImageSharp.Image.Load(CardResources.holo1),
						2 => SixLabors.ImageSharp.Image.Load(CardResources.holo2),
						3 => SixLabors.ImageSharp.Image.Load(CardResources.holo3),
						4 => SixLabors.ImageSharp.Image.Load(CardResources.holo4),
						_ => SixLabors.ImageSharp.Image.Load(CardResources.holo1),
					})
					{
						holoBmp.Mutate(x => x.Resize(baseImage.Width, baseImage.Height));
						baseImage.Mutate(x => x.DrawImage(holoBmp, .95f));
					}
					baseImage.SaveAsGif(finalStream);
				}
				else
				{
					await stream.CopyToAsync(finalStream);
				}
				ulong cmdId = Utilities.GetCommandId("mhhc-info");
				IUserMessage msg = await FollowupWithFileAsync(attachment: new(finalStream, $"pull.gif"), components: new ComponentBuilderV2()
					.WithTextDisplay($"## What will you pull?")
					.WithTextDisplay("-# You can close this at any time; your cards have already been added by the time you see this message.")
					.WithMediaGallery([new MediaGalleryItemProperties() { Description = "it is a mysteryyyy", Media = new UnfurledMediaItemProperties($"attachment://pull.gif") }])
					.WithTextDisplay($"-# Scans by Grender; TL by Mir, Yuwika, and MandL27. </mhhc-info:{cmdId}> for their links!")
					.WithActionRow(components: new List<ButtonBuilder>()
					{
							new() {
								CustomId = "NextCardButton",
								Emote = new Emoji("🔓"),
								Label = "Open",
								Style = ButtonStyle.Primary
							},
							new() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
					}).Build(), ephemeral: false);
				PaginationPages.Add(msg.Id, new Tuple<int, MHHCardPackage[]>(-1, pkgs));
				CardUsers.Add(msg.Id, Context.User.Id);
				MessageStoreTime.Add(msg.Id, DateTime.Now);
			}
		}

#if DEBUG
		[SlashCommand("test-card", "Displays a test card as if randomly pulled.")]
		public async Task TestCard(
			[Summary("effect", "Generate a card effect?"),
				Choice("Normal", (int)CardDeco.Normal),
				Choice("🌈 Holographic 🌈", (int)CardDeco.Holo),
				Choice("🔳 Negative 🔳", (int)CardDeco.Negative),
				Choice("🔲 Grayscale 🔲", (int)CardDeco.Grayscale),
				Choice("🌅 Sepia 🌅", (int)CardDeco.Sepia),
				Choice("🏆 Trophy 🏆", (int)CardDeco.Trophy),
				Choice("⚙️ Iron ⚙️", (int)CardDeco.Iron),
				Choice("⚙️ Dreamcore ⚙️", (int)CardDeco.Dreamcore),
				Choice("⚙️ Artian ⚙️", (int)CardDeco.Artian),
				Choice("⚙️ Aquacore ⚙️", (int)CardDeco.Aquacore),
				Choice("⚙️ Eltalite ⚙️", (int)CardDeco.Eltalite),
				Choice("⚙️ Dragoncore ⚙️", (int)CardDeco.Dragoncore),
				Choice("💎 Firecell 💎", (int)CardDeco.Firecell),
				Choice("💎 Machalite 💎", (int)CardDeco.Machalite),
				Choice("💎 Dragonite 💎", (int)CardDeco.Dragonite),
				Choice("💎 Icium 💎", (int)CardDeco.Icium),
				Choice("💎 Deepsea 💎", (int)CardDeco.Deepsea),
				Choice("💎 Shadowcore 💎", (int)CardDeco.Shadowcore)] int deco = 0, [Summary("Quantity", "How many cards to generate. Default: 1"), MinValue(1), MaxValue(10)] int qty = 1)
		{
			await DeferAsync();
			List<FileAttachment> attachments = [];
			List<Embed> embeds = [];
			List<FileStream> streams = [];
			try
			{
				for (int i = 0; i < qty; i++)
				{
					//max of 8 for base or starter
					int series = _rand.Next(1, 9);
					int[] seriesCnts = [];
					//starters have a weird border
					bool isBase = true;// _rand.Next(0, 2) == 0;
					if (isBase)
					{
						seriesCnts = [90, 77, 77, 77, 90, 77, 75, 75];
					}
					else
					{
						seriesCnts = [17, 24, 22, 3, 27, 28, 24, 26];
					}
					int cardNo = _rand.Next(1, seriesCnts[series - 1] + 1);
					MHHCardPackage pkg = await MHHCardPackage.BuildCardPackage(true, series, cardNo, (CardDeco)deco);
					using (MemoryStream stream = new(CardResources.pullgif))
					{
						ulong cmdId = Utilities.GetCommandId("mhhc-info");
						IUserMessage msg = await FollowupWithFileAsync(attachment: new(stream, $"pull.gif"), components: new ComponentBuilderV2()
							.WithTextDisplay($"## What will you pull?")
							.WithTextDisplay("-# You can close this at any time; your cards have already been added by the time you see this message.")
							.WithMediaGallery([new MediaGalleryItemProperties() { Description = "it is a mysteryyyy", Media = new UnfurledMediaItemProperties($"attachment://pull.gif") }])
							.WithTextDisplay($"-# Scans by Grender; TL by Mir, Yuwika, and MandL27. </mhhc-info:{cmdId}> for their links!")
							.WithActionRow(components: new List<ButtonBuilder>()
							{
								new() {
									CustomId = "NextCardButton",
									Emote = new Emoji("🔓"),
									Label = "Open",
									Style = ButtonStyle.Primary
								},
								new() { Style = ButtonStyle.Primary, CustomId = $"Dismiss", Emote = new Emoji("🔚"), Label = $"Close" }
							}).Build(), ephemeral: false);
						PaginationPages.Add(msg.Id, new Tuple<int, MHHCardPackage[]>(-1, [pkg]));
						MessageStoreTime.Add(msg.Id, DateTime.Now);
					}
				}
				if (qty > 1)
				{
					int cntr = 1;
					attachments.ForEach(x => x.FileName = $"card{cntr++}.png");
					await FollowupWithFilesAsync([.. attachments], $"Here's your test card! Shhh, don't tell anyone about this yet.", embed: embeds[0], ephemeral: false);
				}
				else
				{
					await FollowupWithFileAsync(attachment: attachments.First(), $"Here's your test card! Shhh, don't tell anyone about this yet.", embed: embeds[0], ephemeral: false);
				}
				foreach (FileStream str in streams)
				{
					str.Dispose();
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"[{DateTime.Now:MM/dd/yyyy hh:mm t}] - EXCEPTION - {JsonConvert.SerializeObject(e)}");
			}
		}
#endif

		[SlashCommand("list", "Lists all tasks.")]
		public async Task List(
			[Summary("order", "The column to order by (default: title)."), Choice("time", "time"), Choice("title", "title"), Choice("lastupdated", "lastupdated"), Choice("status", "status")]
			string order = "title",
			[Summary("descending", "Whether you'd like the order to be reversed (true) or not (false). Default is false.")]
			bool desc = false,
			[Summary("archived", "Whether you'd like to include archived tasks (true) or not (false). Default is false.")]
			bool includeArchived = false)
		{
			if (_validListOrders.Contains(order))
			{
				using Wiki_DbContext ctxt = new();
				StringBuilder sb = new();
				sb.AppendLine($"|- 💬 Active -|- ✅ Completed -|- ⏸️ On Hold -|- 📋 Needs Update -|- 💤 Stale -|{(includeArchived ? "- 🔒 Archived -|" : "")}");
				DateTime archiveIgnore = DateTime.UtcNow.AddDays(-30);
				List<WikiTask> tasks = [..ctxt.WikiTasks
						.Include(x => x.Creator)
						.Where(x => (!x.Archived && (x.CompletedOn == null || (x.CompletedOn != null && x.CompletedOn.Value > archiveIgnore))) || includeArchived)];
				switch (order.ToLower())
				{
					case "title":
						if (desc)
						{
							tasks = [.. tasks.OrderByDescending(x => x.Title)];
						}
						else
						{
							tasks = [.. tasks.OrderBy(x => x.Title)];
						}
						break;
					case "time":
						if (desc)
						{
							tasks = [.. tasks.OrderByDescending(x => x.TimeStamp)];
						}
						else
						{
							tasks = [.. tasks.OrderBy(x => x.TimeStamp)];
						}
						break;
					case "status":
						if (desc)
						{
							tasks = [.. tasks.OrderByDescending(x => x.Completed ? 1 : x.NeedsUpdate ? 2 : x.Stale ? 3 : x.OnHold ? 4 : 5)];
						}
						else
						{
							tasks = [.. tasks.OrderBy(x => x.Completed ? 1 : x.NeedsUpdate ? 2 : x.Stale ? 3 : x.OnHold ? 4 : 5)];
						}
						break;
					case "lastupdated":
						if (desc)
						{
							tasks = [.. tasks.OrderByDescending(x => x.LastUpdate)];
						}
						else
						{
							tasks = [.. tasks.OrderBy(x => x.LastUpdate)];
						}
						break;
					default:
						break;
				}
				foreach (WikiTask task in tasks)
				{
					string channelMention = task.Title;
					bool exists = await Utilities.ChannelExists(task.ChannelID);
					if (exists)
					{
						channelMention = MentionUtils.MentionChannel(task.ChannelID);
					}
					string activity = !task.Stale && !task.Completed && !task.NeedsUpdate && !task.OnHold && !task.Archived ? " 💬" : "";
					sb.AppendLine(@$"* **{(task.Archived ? " 🔒" : "")}{(task.Stale ? " 💤" : "")}{(task.Completed ? " ✅" : "")}{(task.NeedsUpdate ? " 📋" : "")}{(task.OnHold ? " ⏸️" : "")}{activity} - {channelMention}**");
				}
				EmbedBuilder builder = new()
				{
					Title = "Current Tasks",
					Description = sb.ToString()
				};
				await RespondAsync(embed: builder.Build(), ephemeral: true);
			}
			else
			{
				await RespondAsync("Your sort order is not valid!", ephemeral: true);
			}
		}
	}

	public class TaskAutocomplete : AutocompleteHandler
	{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		{
			return AutocompletionResult.FromSuccess(Utilities.TaskResults.Take(25));
		}
	}

	public class TagAutocomplete : AutocompleteHandler
	{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		{
			return AutocompletionResult.FromSuccess(Utilities.Forum!.Tags.Select(x => new AutocompleteResult(x.Name, x.Name)).OrderBy(x => x.Name).Take(25));
		}
	}

	public static class IPathExtensions
	{
		private static IImageProcessingContext ApplyRoundedCorners(this IImageProcessingContext ctxt, float cornerRadius)
		{
			PathCollection corners = BuildCorners(ctxt.GetCurrentSize(), cornerRadius);
			return ctxt.Fill(new DrawingOptions() { GraphicsOptions = new GraphicsOptions() { Antialias = true } }, SixLabors.ImageSharp.Color.Transparent, corners);
		}

		private static PathCollection BuildCorners(Size size, float cornerRadius)
		{
			var rect = new RectangularPolygon(-0.5f, -0.5f, cornerRadius, cornerRadius);

			IPath cornerToptLeft = rect.Clip(new EllipsePolygon(cornerRadius - 0.5f, cornerRadius - 0.5f, cornerRadius));

			float rightPos = size.Width - cornerToptLeft.Bounds.Width + 1;
			float bottomPos = size.Height - cornerToptLeft.Bounds.Height + 1;

			IPath cornerTopRight = cornerToptLeft.RotateDegree(90).Translate(rightPos, 0);
			IPath cornerBottomLeft = cornerToptLeft.RotateDegree(-90).Translate(0, bottomPos);
			IPath cornerBottomRight = cornerToptLeft.RotateDegree(180).Translate(rightPos, bottomPos);

			return new PathCollection(cornerToptLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight);
		}

		public static IImageProcessingContext RoundCorners(this IImageProcessingContext processingContext, float cornerRadius)
		{
			return processingContext.Resize(new ResizeOptions
			{
				Size = processingContext.GetCurrentSize(),
				Mode = ResizeMode.Crop
			}).ApplyRoundedCorners(cornerRadius);
		}
	}
}
