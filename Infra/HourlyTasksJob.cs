using Discord;
using Microsoft.EntityFrameworkCore;
using Quartz;
using RathalOS.Data.Context;
using RathalOS.Data.Models;

namespace RathalOS.Infra
{
	public class HourlyTasksJob : IJob
	{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
		public async Task Execute(IJobExecutionContext context)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		{
			ulong[] expiredMsgs = [..InteractionEngine.MessageStoreTime.Where(x => DateTime.Now - x.Value > new TimeSpan(1, 0, 0)).Select(x => x.Key)];
			InteractionEngine.PaginationPages = InteractionEngine.PaginationPages.Where(x => !expiredMsgs.Contains(x.Key)).ToDictionary();
			InteractionEngine.CardListPagination = InteractionEngine.CardListPagination.Where(x => !expiredMsgs.Contains(x.Key)).ToDictionary();			
		}
	}
}
