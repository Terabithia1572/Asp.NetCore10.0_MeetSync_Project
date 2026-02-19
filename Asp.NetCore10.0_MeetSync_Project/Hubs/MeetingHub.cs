using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Asp.NetCore10._0_MeetSync_Project.Hubs
{
    public class MeetingHub : Hub
    {
        // Oda bazlı moderatörleri ve kullanıcı isimlerini tutuyoruz
       // private static readonly Dictionary<string, string> RoomModerators = new Dictionary<string, string>();
        // Global dictionary'ler — class seviyesinde tanımla
        private static readonly ConcurrentDictionary<string, List<ParticipantInfo>> RoomParticipants = new();
        private static readonly ConcurrentDictionary<string, string> RoomModerators = new(); // roomName -> moderator connectionId

        public class ParticipantInfo
        {
            public string ConnectionId { get; set; }
            public string UserName { get; set; }
            public bool IsModerator { get; set; }
        }

        // ✅ JoinRoom metodunu GÜNCELLE — moderatör tracking ekle
        public async Task JoinRoom(string roomName, string userName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

            // Participants listesini yönet
            if (!RoomParticipants.ContainsKey(roomName))
            {
                RoomParticipants[roomName] = new List<ParticipantInfo>();
            }

            var participants = RoomParticipants[roomName];

            // İlk katılan moderatör olsun
            bool isModerator = participants.Count == 0;
            if (isModerator)
            {
                RoomModerators[roomName] = Context.ConnectionId;
            }

            participants.Add(new ParticipantInfo
            {
                ConnectionId = Context.ConnectionId,
                UserName = userName,
                IsModerator = isModerator
            });

            // Kullanıcıya moderatör durumunu bildir
            await Clients.Caller.SendAsync("ModeratorStatus", isModerator);

            // Odadaki herkese katılımcı listesini gönder
            await Clients.Group(roomName).SendAsync("UpdateParticipants", participants);

            // Diğer kullanıcılara bildir (mevcut kod)
            await Clients.OthersInGroup(roomName).SendAsync("UserJoined", userName, Context.ConnectionId);
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
        //public async Task UpdateUserName(string roomName, string newName)
        //{
        //    await Clients.Group(roomName).SendAsync("UserNameUpdated", Context.ConnectionId, newName);
        //}

        // ✅ OnDisconnectedAsync GÜNCELLE — moderatör yönetimi ekle
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Kullanıcının hangi odadan çıktığını bul
            foreach (var room in RoomParticipants)
            {
                var participant = room.Value.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
                if (participant != null)
                {
                    room.Value.Remove(participant);

                    // Eğer moderatör çıktıysa ve oda boş değilse
                    if (participant.IsModerator && room.Value.Count > 0)
                    {
                        // İlk kullanıcıyı yeni moderatör yap
                        var newMod = room.Value.First();
                        newMod.IsModerator = true;
                        RoomModerators[room.Key] = newMod.ConnectionId;

                        await Clients.Group(room.Key).SendAsync("ModeratorChanged", newMod.ConnectionId);
                        await Clients.Client(newMod.ConnectionId).SendAsync("ModeratorStatus", true);
                    }

                    // Odadaki herkese güncellemeyi gönder
                    await Clients.Group(room.Key).SendAsync("UpdateParticipants", room.Value);
                    await Clients.Group(room.Key).SendAsync("UserDisconnected", Context.ConnectionId);

                    // Oda boşsa temizle
                    if (room.Value.Count == 0)
                    {
                        RoomParticipants.TryRemove(room.Key, out _);
                        RoomModerators.TryRemove(room.Key, out _);
                    }

                    break;
                }
            }

            await base.OnDisconnectedAsync(exception);
        }


        // ✅ YENİ METOD: İsim güncelleme
        public async Task UpdateUserName(string roomName, string newUserName)
        {
            if (RoomParticipants.TryGetValue(roomName, out var participants))
            {
                var participant = participants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
                if (participant != null)
                {
                    participant.UserName = newUserName;

                    // Odadaki herkese isim değişikliğini bildir
                    await Clients.Group(roomName).SendAsync("UserNameUpdated", Context.ConnectionId, newUserName);
                    await Clients.Group(roomName).SendAsync("UpdateParticipants", participants);
                }
            }
        }

        // ✅ YENİ METOD: Moderatör atama
        public async Task AssignModerator(string roomName, string newModeratorConnectionId)
        {
            if (RoomParticipants.TryGetValue(roomName, out var participants))
            {
                // Eski moderatörü bul
                var oldMod = participants.FirstOrDefault(p => p.IsModerator);
                if (oldMod != null)
                {
                    oldMod.IsModerator = false;
                }

                // Yeni moderatörü ata
                var newMod = participants.FirstOrDefault(p => p.ConnectionId == newModeratorConnectionId);
                if (newMod != null)
                {
                    newMod.IsModerator = true;
                    RoomModerators[roomName] = newModeratorConnectionId;

                    // Herkese bildir
                    await Clients.Group(roomName).SendAsync("ModeratorChanged", newModeratorConnectionId);
                    await Clients.Group(roomName).SendAsync("UpdateParticipants", participants);

                    // Yeni moderatöre özel mesaj
                    await Clients.Client(newModeratorConnectionId).SendAsync("ModeratorStatus", true);
                }
            }
        }

        // ✅ YENİ METOD: Toplantıyı bitir
        public async Task EndMeeting(string roomName)
        {
            if (RoomParticipants.TryGetValue(roomName, out var participants))
            {
                var moderator = participants.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);

                if (moderator != null && moderator.IsModerator)
                {
                    // Herkese toplantı bitti mesajı gönder
                    await Clients.Group(roomName).SendAsync("RoomClosedByModerator");

                    // Oda verilerini temizle
                    RoomParticipants.TryRemove(roomName, out _);
                    RoomModerators.TryRemove(roomName, out _);
                }
            }
        }
    }
}