using Microsoft.EntityFrameworkCore;
using Quartz;
using RathalOS.Data.Context;
using RathalOS.Data.Models;

namespace RathalOS.Infra
{
	public class DailyTasksJob : IJob
	{
		public async Task Execute(IJobExecutionContext context)
		{
			List<ReleaseDates> releaseDates = [];
			List<WikiTask> tasks = [];
			using (Wiki_DbContext ctxt = new())
			{
				releaseDates = [.. ctxt.ReleaseDates.Where(x => x.ReleaseDate.Date == DateTime.Now.Date && !x.HasNotified)];
				tasks = [.. ctxt.WikiTasks
					.Include(x => x.Assigned)
					.ThenInclude(x => x.Assignee)
					.ToList().Where(x => (x.Recurring || x.Upcoming) && releaseDates.Any(y => x.TagsCSV.Split(",").Contains(y.Tag)))];
				foreach (WikiTask task in tasks)
				{
					task.Completed = false;
					task.CompletedOn = null;
					if (task.Title.StartsWith("(COMPLETED)"))
					{
						task.Title = task.Title[12..].Trim();
					}
				}
				foreach (ReleaseDates releaseDate in releaseDates)
				{
					releaseDate.HasNotified = true;
				}
				foreach (WikiUser user in ctxt.WikiUsers.Where(x => !string.IsNullOrEmpty(x.WikiUsername) && x.WikiUserId != null))
				{
					await user.CashInPulls();
				}
				ctxt.MHHOpenTrades.RemoveRange(ctxt.MHHOpenTrades.Include(x => x.RecipientRequest).Include(x => x.ExecutorOffer).Where(x => x.Expires <= DateTime.UtcNow));
				await ctxt.SaveChangesAsync();
			}
			await Utilities.PingRecurringTasks(releaseDates, tasks);
		}
	}
}
