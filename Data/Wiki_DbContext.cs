using Microsoft.EntityFrameworkCore;
using System.Configuration;

namespace GraviOS.Data
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
			modelBuilder.Entity<WikiTask>().HasOne(x => x.Creator);
			modelBuilder.Entity<WikiUser>().HasMany(x => x.UserAssignments);
			modelBuilder.Entity<WikiTask>().HasMany(x => x.Assigned);
			modelBuilder.Entity<WikiUser>().HasMany(x => x.CreatedTasks)
				.WithOne(x => x.Creator);
			modelBuilder.Entity<WikiTask>().HasMany(x => x.Updates)
				.WithOne(x => x.Task);
			base.OnModelCreating(modelBuilder);
		}

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			//If you want to use a different storage solution, change this to any of the other available DbContextOptionsBuilder methods for EFCore.
			optionsBuilder.UseSqlServer(ConfigurationManager.AppSettings.Get("DBConnString"));
			base.OnConfiguring(optionsBuilder);
		}

		public DbSet<AssignedTask> AssignedTasks { get; set; }
		public DbSet<WikiTask> WikiTasks { get; set; }
		public DbSet<WikiUser> WikiUsers { get; set; }
		public DbSet<WikiTaskUpdate> WikiTaskUpdates { get; set; }
	}
}
