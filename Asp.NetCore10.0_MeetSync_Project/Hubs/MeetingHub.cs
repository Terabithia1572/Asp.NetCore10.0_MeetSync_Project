using Microsoft.AspNetCore.SignalR;

namespace Asp.NetCore10._0_MeetSync_Project.Hubs
{
    public class MeetingHub : Hub
    {
        // Odaya katılım - ConnectionId'yi geri gönderiyoruz ki el sıkışma başlasın
        public async Task JoinRoom(string roomName, string userName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

            // Odadaki diğerlerine "Ben geldim, ID'm bu" mesajı gider
            await Clients.OthersInGroup(roomName)
                .SendAsync("UserJoined", userName, Context.ConnectionId);
        }

        // --- WEBRTC SİNYALLEŞME (NOKTA ATIŞI GÖNDERİM) ---

        // Teklifi sadece belirli bir kişiye (targetId) gönderiyoruz
        public async Task SendOffer(string offer, string targetId)
        {
            await Clients.Client(targetId)
                .SendAsync("ReceiveOffer", offer, Context.ConnectionId);
        }

        // Cevabı sadece teklifi gönderen kişiye iletiyoruz
        public async Task SendAnswer(string answer, string targetId)
        {
            await Clients.Client(targetId)
                .SendAsync("ReceiveAnswer", answer, Context.ConnectionId);
        }

        // ICE Candidate bilgilerini hedefe iletiyoruz
        public async Task SendIceCandidate(string candidate, string targetId)
        {
            await Clients.Client(targetId)
                .SendAsync("ReceiveIceCandidate", candidate, Context.ConnectionId);
        }

        // --- CHAT SİSTEMİ ---
        public async Task SendMessage(string roomName, string userName, string message)
        {
            await Clients.Group(roomName)
                .SendAsync("ReceiveMessage", userName, message);
        }

        public async Task LeaveRoom(string roomName, string userName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
            await Clients.Group(roomName).SendAsync("UserLeft", userName, Context.ConnectionId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Kullanıcı koptuğunda odadaki diğerleri onun video kutusunu kapatabilsin diye
            await Clients.All.SendAsync("UserDisconnected", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}