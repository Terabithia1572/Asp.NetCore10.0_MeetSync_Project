using Microsoft.AspNetCore.SignalR;

namespace MeetSync.Web.Hubs
{
    public class MeetingHub : Hub
    {
        public async Task JoinRoom(Guid roomId, string userName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());

            await Clients.Group(roomId.ToString())
                .SendAsync("UserJoined", userName);
        }

        public async Task SendMessage(Guid roomId, string userName, string message)
        {
            await Clients.Group(roomId.ToString())
                .SendAsync("ReceiveMessage", userName, message);
        }

        public async Task SendOffer(Guid roomId, string offer)
        {
            await Clients.OthersInGroup(roomId.ToString())
                .SendAsync("ReceiveOffer", offer);
        }

        public async Task SendAnswer(Guid roomId, string answer)
        {
            await Clients.OthersInGroup(roomId.ToString())
                .SendAsync("ReceiveAnswer", answer);
        }

        public async Task SendIceCandidate(Guid roomId, string candidate)
        {
            await Clients.OthersInGroup(roomId.ToString())
                .SendAsync("ReceiveIceCandidate", candidate);
        }
    }
}
