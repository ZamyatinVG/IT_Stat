using System;
using Microsoft.EntityFrameworkCore;

namespace IT_Stat.Models
{
    public partial class ServiceDesk : DbContext
    {
        public ServiceDesk(DbContextOptions<ServiceDesk> options) : base(options) { }
        public virtual DbSet<Reaction> Reaction { get; set; }
        public virtual DbSet<Fact> Fact { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Reaction>(builder => { builder.HasNoKey(); });
            modelBuilder.Entity<Fact>(builder => { builder.HasNoKey(); });
        }
    }
    public partial class Reaction
    {
        public int? RequestID { get; set; }
        public string FIO { get; set; }
        public DateTime? AppointDate { get; set; }
        public DateTime? UserDate { get; set; }
        public string ReactionWorkTime { get; set; }
        public string ReactionWorkTimeFree { get; set; }
    }
    public partial class Fact
    {
        public string FIO { get; set; }
        public string NoDoc { get; set; }
        public string ResolvedTime { get; set; }
        public int StoryPoints { get; set; }
        public int StoryPointsRequest { get; set; }
        public int StoryPointsLinkedRequest { get; set; }
        public int ToJira { get; set; }
        public double? PercentToJira { get; set; }
    }
}