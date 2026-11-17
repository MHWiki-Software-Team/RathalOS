using Microsoft.EntityFrameworkCore;
using RathalOS.Data.Models;
using System.Configuration;

namespace RathalOS.Data.Context
{
	public class Wiki_DbContext : DbContext
	{
		public Wiki_DbContext()
		{
		}

		public Wiki_DbContext(DbContextOptions<Wiki_DbContext> options) : base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<WikiUser>().HasMany(x => x.Cards);
			base.OnModelCreating(modelBuilder);
		}

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			//If you want to use a different storage solution, change this to any of the other available DbContextOptionsBuilder methods for EFCore.
			optionsBuilder.UseSqlServer(ConfigurationManager.AppSettings.Get("DBConnString"));
			base.OnConfiguring(optionsBuilder);
		}
		public DbSet<WikiUser> WikiUsers { get; set; }
		public DbSet<MHHCard> MHHCards { get; set; }
		public DbSet<MHHCardStorage> MHHCardStorage { get; set; }
		public DbSet<MHHOpenTrade> MHHOpenTrades { get; set; }
		public DbSet<MHHEnvironmentVariables> MHHEnvironmentVariables { get; set; }
		private static MHHEnvironmentVariables? _environmentVariables;
		public static async Task<MHHEnvironmentVariables> GetEnvironmentVariables()
		{
			if (_environmentVariables == null)
			{
				using (Wiki_DbContext ctxt = new())
				{
					_environmentVariables = await ctxt.MHHEnvironmentVariables.FirstOrDefaultAsync();
					bool existed = _environmentVariables != null;
					_environmentVariables ??= new();
					if (!existed)
					{
						_environmentVariables.TotalPulls = 0;
						_environmentVariables.CurrentSpecialEdition = SpecialEditions.Metal;
						await ctxt.MHHEnvironmentVariables.AddAsync(_environmentVariables);
						await ctxt.SaveChangesAsync();
					}
				}
			}
			return _environmentVariables;
		}

		public static async Task UpdateEnvironmentVariables(MHHEnvironmentVariables var)
		{
			using (Wiki_DbContext ctxt = new())
			{
				bool existed = _environmentVariables != null;
				_environmentVariables ??= var;
				if (!existed)
				{
					_environmentVariables.TotalPulls = var.TotalPulls;
					_environmentVariables.CurrentSpecialEdition = var.CurrentSpecialEdition;
					_environmentVariables.Monkeys = var.Monkeys;
					await ctxt.MHHEnvironmentVariables.AddAsync(_environmentVariables);
				}
				else
				{
					ctxt.MHHEnvironmentVariables.Attach(var);
				}
				await ctxt.SaveChangesAsync();
			}
		}
	}
}
