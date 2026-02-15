using Microsoft.AspNetCore.SignalR;

namespace Asp.NetCore10._0_MeetSync_Project.Hubs
{
    public class MeetingHub : Hub
    {
        public async Task JoinRoom(string roomName, string userName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

            await Clients.Group(roomName)
                .SendAsync("UserJoined", userName);
        }

        public async Task SendMessage(string roomName, string userName, string message)
        {
            await Clients.Group(roomName)
                .SendAsync("ReceiveMessage", userName, message);
        }

        public async Task LeaveRoom(string roomName, string userName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);

            await Clients.Group(roomName)
                .SendAsync("UserLeft", userName);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // later participant tracking eklenecek
            await base.OnDisconnectedAsync(exception);
        }
    }
}
