using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Asp.NetCore10._0_MeetSync_Project.Hubs
{
    public class MeetHub
    {
        public async Task JoinRoom(Guid roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
        }

    }
}
