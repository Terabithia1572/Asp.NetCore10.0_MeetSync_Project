"use strict";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/meetingHub")
    .withAutomaticReconnect()
    .build();

let localStream;
let peerConnections = {};
const servers = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] };

// 1. Başlatma
async function init() {
    try {
        // Kamerayı ve Mikrofonu al
        localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
        document.getElementById("localVideo").srcObject = localStream;

        // SignalR Bağlantısını Başlat
        await connection.start();
        console.log("SignalR Connected.");

        // Odaya Katıl
        await connection.invoke("JoinRoom", roomName, userName);
    } catch (err) {
        console.error("Başlatma hatası:", err);
        alert("Kamera/Mikrofon erişimi engellendi!");
    }
}

// 2. PeerConnection Fabrikası
function createPeerConnection(remoteId, remoteName) {
    const pc = new RTCPeerConnection(servers);
    peerConnections[remoteId] = pc;

    // Kendi stream'imizi ekle
    localStream.getTracks().forEach(track => pc.addTrack(track, localStream));

    // ICE Candidate gönderimi
    pc.onicecandidate = event => {
        if (event.candidate) {
            connection.invoke("SendIceCandidate", JSON.stringify(event.candidate), remoteId);
        }
    };

    // Karşı tarafın görüntüsü geldiğinde
    pc.ontrack = event => {
        console.log("Remote track received from:", remoteId);
        addRemoteVideoUI(remoteId, remoteName, event.streams[0]);
    };

    return pc;
}

// 3. UI'a Video Kutusu Ekleme (Senin Tasarımınla Birebir)
function addRemoteVideoUI(remoteId, remoteName, stream) {
    if (document.getElementById(`container-${remoteId}`)) {
        document.getElementById(`video-${remoteId}`).srcObject = stream;
        return;
    }

    const grid = document.getElementById("video-grid");

    const container = document.createElement("div");
    container.id = `container-${remoteId}`;
    container.className = "relative rounded-xl overflow-hidden bg-zinc-900 aspect-video shadow-2xl border border-white/5";

    const video = document.createElement("video");
    video.id = `video-${remoteId}`;
    video.autoplay = true;
    video.playsinline = true;
    video.srcObject = stream;
    video.className = "w-full h-full object-cover bg-black";

    const label = document.createElement("div");
    label.className = "absolute bottom-4 left-4 flex items-center gap-2 px-3 py-1.5 rounded-lg bg-black/60 backdrop-blur-md border border-white/10";
    label.innerHTML = `<span class="text-xs font-semibold text-white">${remoteName}</span>`;

    container.appendChild(video);
    container.appendChild(label);
    grid.appendChild(container);
}

// 4. SignalR Eventleri (El Sıkışma)
connection.on("UserJoined", async (joinedUser, remoteId) => {
    console.log("User joined:", joinedUser);
    const pc = createPeerConnection(remoteId, joinedUser);
    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);
    connection.invoke("SendOffer", JSON.stringify(offer), remoteId);
});

connection.on("ReceiveOffer", async (offer, remoteId) => {
    const pc = createPeerConnection(remoteId, "Remote User");
    await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(offer)));
    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    connection.invoke("SendAnswer", JSON.stringify(answer), remoteId);
});

connection.on("ReceiveAnswer", async (answer, remoteId) => {
    await peerConnections[remoteId].setRemoteDescription(new RTCSessionDescription(JSON.parse(answer)));
});

connection.on("ReceiveIceCandidate", async (candidate, remoteId) => {
    await peerConnections[remoteId].addIceCandidate(new RTCIceCandidate(JSON.parse(candidate)));
});

// 5. Chat Sistemi
document.getElementById("btn-send-chat").onclick = () => {
    const input = document.getElementById("chat-input");
    if (input.value) {
        connection.invoke("SendMessage", roomName, userName, input.value);
        input.value = "";
    }
};

connection.on("ReceiveMessage", (user, message) => {
    const msgDiv = document.getElementById("chat-messages");
    const isMe = user === userName;

    const html = `
        <div class="flex flex-col ${isMe ? 'items-end' : 'items-start'} gap-1">
            <span class="text-[10px] text-white/40">${user}</span>
            <div class="${isMe ? 'bg-primary' : 'bg-zinc-800'} rounded-lg p-2 max-w-[80%]">
                ${message}
            </div>
        </div>
    `;
    msgDiv.innerHTML += html;
    msgDiv.scrollTop = msgDiv.scrollHeight;
});

// Çalıştır kral!
init();