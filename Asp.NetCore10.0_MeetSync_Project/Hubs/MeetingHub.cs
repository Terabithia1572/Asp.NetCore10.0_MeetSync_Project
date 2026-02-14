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
        public async Task SendOffer(string room, string offer)
        {
            await Clients.OthersInGroup(room)
                .SendAsync("ReceiveOffer", offer);
        }

        public async Task SendAnswer(string room, string answer)
        {
            await Clients.OthersInGroup(room)
                .SendAsync("ReceiveAnswer", answer);
        }

        public async Task SendIceCandidate(string room, string candidate)
        {
            await Clients.OthersInGroup(room)
                .SendAsync("ReceiveIceCandidate", candidate);
        }

    }
}
