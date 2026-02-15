using MeetSync.Domain.Entities;
using MeetSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Asp.NetCore10._0_MeetSync_Project.Controllers
{
    public class RoomController : Controller
    {
        private readonly MeetSyncDbContext _context;

        public RoomController(MeetSyncDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Create(string name)
        {
            var room = new Room
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedBy = Guid.NewGuid()
            };

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            return Redirect($"/room/{room.Id}");
        }

        [Route("room/{id:guid}")]
        public IActionResult Index(Guid id)
        {
            ViewBag.RoomId = id;
            return View();
        }
    }
}
