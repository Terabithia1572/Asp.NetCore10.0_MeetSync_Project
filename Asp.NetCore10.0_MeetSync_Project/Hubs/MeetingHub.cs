using Microsoft.AspNetCore.SignalR;

namespace Asp.NetCore10._0_MeetSync_Project.Hubs
{
    public class MeetingHub : Hub
    {
        // Oda bazlı moderatörleri ve kullanıcı isimlerini tutuyoruz
        private static readonly Dictionary<string, string> RoomModerators = new Dictionary<string, string>();

        public async Task JoinRoom(string roomName, string userName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

            // Eğer odada kimse yoksa, ilk giren moderatör olur
            bool isModerator = false;
            if (!RoomModerators.ContainsKey(roomName))
            {
                RoomModerators[roomName] = Context.ConnectionId;
                isModerator = true;
            }

            // Katılan kişiye moderatör olup olmadığını söyle
            await Clients.Caller.SendAsync("ModeratorStatus", isModerator);

            // Diğerlerine yeni birinin gerçek ismiyle geldiğini haber ver
            await Clients.OthersInGroup(roomName)
                .SendAsync("UserJoined", userName, Context.ConnectionId);
        }

        public async Task SendOffer(string offer, string targetId)
        {
            await Clients.Client(targetId).SendAsync("ReceiveOffer", offer, Context.ConnectionId);
        }

        public async Task SendAnswer(string answer, string targetId)
        {
            await Clients.Client(targetId).SendAsync("ReceiveAnswer", answer, Context.ConnectionId);
        }

        public async Task SendIceCandidate(string candidate, string targetId)
        {
            await Clients.Client(targetId).SendAsync("ReceiveIceCandidate", candidate, Context.ConnectionId);
        }

        public async Task SendMessage(string roomName, string userName, string message)
        {
            await Clients.Group(roomName).SendAsync("ReceiveMessage", userName, message);
        }

        // İsim Güncelleme
        public async Task UpdateUserName(string roomName, string newName)
        {
            await Clients.Group(roomName).SendAsync("UserNameUpdated", Context.ConnectionId, newName);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Eğer kopan kişi moderatörse odayı kapatma sinyali gönder
            var roomEntry = RoomModerators.FirstOrDefault(x => x.Value == Context.ConnectionId);
            if (!string.IsNullOrEmpty(roomEntry.Key))
            {
                await Clients.OthersInGroup(roomEntry.Key).SendAsync("RoomClosedByModerator");
                RoomModerators.Remove(roomEntry.Key);
            }

            await Clients.All.SendAsync("UserDisconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}