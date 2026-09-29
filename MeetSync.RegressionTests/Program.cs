using MeetSync.Application.DTOs.Room;
using MeetSync.Application.Validators;
using MeetSync.Domain.Entities;
using MeetSync.Infrastructure.Persistence;
using MeetSync.Infrastructure.Services;
using MeetSync.Infrastructure.Services.State;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

Check(RoomName.Key(" Yazılım_Toplantısı ") == RoomName.Key("YAZILIM_TOPLANTISI"), "Turkish casing and whitespace");
Check(RoomName.Key("İLETİŞİM") == RoomName.Key("iletişim"), "Dotted Turkish I");
Check(RoomName.Key("oda_bir") != RoomName.Key("odaXbir"), "Underscore remains literal");
Check(RoomName.Clean("oda%20bir") == "oda%20bir", "No repeated URL decoding");
var store = new InMemoryMeetingStateStore();
Check(await store.AddParticipantAsync("Yazılım_Toplantısı", "yunus-connection", "Yunus"), "First participant is moderator");
Check(!await store.AddParticipantAsync(" YAZILIM_TOPLANTISI ", "ahmet-connection", "Ahmet"), "Second browser is not a second moderator");
Check((await store.GetParticipantsAsync("yazılım_toplantısı")).Count == 2, "Both users share one participant list");

if (!args.Contains("--sql"))
{
    Console.WriteLine("SKIP: SQL integration. Run with --sql against LocalDB, or set MEETSYNC_TEST_SERVER.");
    return;
}

// Only creates and deletes a uniquely named TEST database. Never reads appsettings.
var database = "MeetSync_Regression_" + Guid.NewGuid().ToString("N");
var server = Environment.GetEnvironmentVariable("MEETSYNC_TEST_SERVER") ?? @"(localdb)\MSSQLLocalDB";
var builder = new SqlConnectionStringBuilder
{
    DataSource = server, InitialCatalog = database, IntegratedSecurity = true,
    Encrypt = false, TrustServerCertificate = true, ConnectTimeout = 5
};
var options = new DbContextOptionsBuilder<MeetSyncDbContext>().UseSqlServer(builder.ConnectionString).Options;
MeetSyncDbContext Context() => new(options);
RoomService Service(MeetSyncDbContext context) => new(context, new CreateRoomRequestValidator());
await using var setup = Context();
var created = false;
try
{
    await setup.Database.EnsureCreatedAsync();
    created = true;
    var yunus = Guid.NewGuid();
    var ahmet = Guid.NewGuid();
    Guid originalId;
    await using (var db = Context())
    {
        var first = await Service(db).CreateRoomAsync(new("Yazılım_Toplantısı", yunus));
        originalId = first.Id;
    }
    await using (var db = Context())
    {
        var second = await Service(db).GetOrCreateRoomAsync(" YAZILIM_TOPLANTISI ", ahmet);
        Check(second.Id == originalId && second.CreatedBy == yunus, "Second user reuses room ID and owner");
        Check(await db.Rooms.CountAsync() == 1, "One database row for both browsers");
        var literal = await Service(db).CreateRoomAsync(new("YazılımXToplantısı", ahmet));
        Check(literal.Id != originalId, "SQL underscore is not a wildcard");
    }
    var concurrent = await Task.WhenAll(Enumerable.Range(0, 12).Select(async index =>
    {
        await using var db = Context();
        return await Service(db).CreateRoomAsync(new(index % 2 == 0 ? "Eşzamanlı_Oda" : "EŞZAMANLI_ODA", yunus));
    }));
    Check(concurrent.Select(r => r.Id).Distinct().Count() == 1, "12 concurrent requests return one room");
    await using (var db = Context())
    {
        Check(await db.Rooms.CountAsync() == 3, "Concurrent creation inserts exactly once");
        await Service(db).DeactivateRoomAsync(originalId);
        var fresh = await Service(db).GetOrCreateRoomAsync("Yazılım_Toplantısı", ahmet);
        Check(fresh.Id != originalId && fresh.CreatedBy == ahmet, "Inactive room allows a new meeting");
        db.Rooms.Add(new Room { Id = Guid.NewGuid(), Name = fresh.Name, CreatedBy = yunus,
            CreatedAt = fresh.CreatedAt.AddMinutes(1), IsActive = true });
        await db.SaveChangesAsync();
        Check((await Service(db).GetRoomByNameAsync(fresh.Name))!.Id == fresh.Id, "Legacy duplicates resolve consistently");
    }
}
finally
{
    if (created) await setup.Database.EnsureDeletedAsync();
}
