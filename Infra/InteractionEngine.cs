using ClosedXML.Excel;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using RathalOS.Data.Context;
using RathalOS.Data.Models;
using System.Text;

namespace RathalOS.Infra
{
	public class InteractionEngine : InteractionModuleBase
	{
		private static readonly string[] _validListOrders =  ["title", "time", "lastupdated", "status"];

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
			WikiTask? task = ctxt.WikiTasks.Include(x => x.Updates).FirstOrDefault(x => x.ChannelID == Context.Channel.Id);
			if (task != null)
			{
				WikiUser? user = ctxt.WikiUsers.FirstOrDefault(x => x.UserID == Context.User.Id);
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
				await RespondAsync("Task updated!", ephemeral: true);
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
			WikiTask? task = ctxt.WikiTasks.FirstOrDefault(x => taskId != null ? x.Id == taskId.Value : x.ChannelID == Context.Channel.Id);
			if (task != null)
			{
				task.Description = content;
				await ctxt.SaveChangesAsync();
				await RespondAsync("Description updated!", ephemeral: true);
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
}
