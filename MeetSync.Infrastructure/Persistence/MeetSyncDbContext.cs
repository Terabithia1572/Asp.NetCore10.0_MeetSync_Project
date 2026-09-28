using Microsoft.EntityFrameworkCore;
using MeetSync.Domain.Entities;

namespace MeetSync.Infrastructure.Persistence;

public class MeetSyncDbContext : DbContext
{
    public MeetSyncDbContext(DbContextOptions<MeetSyncDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomParticipant> Participants => Set<RoomParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MeetingRecording> Recordings => Set<MeetingRecording>();
    public DbSet<MeetingTranscript> Transcripts => Set<MeetingTranscript>();
    public DbSet<MeetingSummary> Summaries => Set<MeetingSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RoomParticipant>()
            .HasOne(rp => rp.Room)
            .WithMany(r => r.Participants)
            .HasForeignKey(rp => rp.RoomId);

        modelBuilder.Entity<RoomParticipant>()
            .HasOne(rp => rp.User)
            .WithMany(u => u.Participants)
            .HasForeignKey(rp => rp.UserId);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Room)
            .WithMany(r => r.Messages)
            .HasForeignKey(m => m.RoomId);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId);

        modelBuilder.Entity<MeetingRecording>()
            .HasOne(mr => mr.Room)
            .WithMany()
            .HasForeignKey(mr => mr.RoomId);

        modelBuilder.Entity<MeetingTranscript>()
            .HasOne(mt => mt.Room)
            .WithMany()
            .HasForeignKey(mt => mt.RoomId);

        modelBuilder.Entity<MeetingSummary>()
            .HasOne(ms => ms.Room)
            .WithMany()
            .HasForeignKey(ms => ms.RoomId);
    }
}
