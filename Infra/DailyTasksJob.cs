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
				foreach (WikiUser user in ctxt.WikiUsers.Where(x => !string.IsNullOrEmpty(x.WikiUsername) && x.WikiUserId != null))
				{
					int? editsSince = await user.CashInPulls();
					if (editsSince != null && editsSince >= 25)
					{
						user.Boosters++;
						user.LastActiveCheck = DateTime.Now;
					}
					user.GrantPulls = 5;
				}
				ctxt.MHHOpenTrades.RemoveRange(ctxt.MHHOpenTrades.Include(x => x.RecipientRequest).Include(x => x.ExecutorOffer).Where(x => x.Expires <= DateTime.UtcNow));
				await ctxt.SaveChangesAsync();
			}
		}
	}
}
