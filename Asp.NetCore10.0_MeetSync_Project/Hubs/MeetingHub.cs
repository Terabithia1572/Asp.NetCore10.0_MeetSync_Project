using Microsoft.AspNetCore.SignalR;

namespace MeetSync.Web.Hubs;

public class MeetingHub : Hub
{
    public async Task JoinRoom(string roomName, string userName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

        await Clients.Group(roomName)
            .SendAsync("UserJoined", userName);
    }

    public async Task LeaveRoom(string roomName, string userName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);

        await Clients.Group(roomName)
            .SendAsync("UserLeft", userName);
    }

    public async Task SendMessage(string roomName, string userName, string message)
    {
        await Clients.Group(roomName)
            .SendAsync("ReceiveMessage", userName, message);
    }
    public async Task SendOffer(string roomName, string offer)
    {
        await Clients.OthersInGroup(roomName)
            .SendAsync("ReceiveOffer", offer);
    }

    public async Task SendAnswer(string roomName, string answer)
    {
        await Clients.OthersInGroup(roomName)
            .SendAsync("ReceiveAnswer", answer);
    }

    public async Task SendIceCandidate(string roomName, string candidate)
    {
        await Clients.OthersInGroup(roomName)
            .SendAsync("ReceiveIceCandidate", candidate);
    }

}
