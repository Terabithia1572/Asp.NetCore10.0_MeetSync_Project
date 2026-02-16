let localStream;
let peerConnections = {}; // Herkesin connection'ını ID ile burada tutacağız
const servers = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] };

// 1. Kamerayı Aç
async function startMedia() {
    try {
        localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
        document.getElementById("localVideo").srcObject = localStream;
    } catch (err) {
        console.error("Kamera açılmadı:", err);
    }
}

// 2. Yeni Biri Geldiğinde Bağlantı Başlat
connection.on("UserJoined", async (userName, remoteId) => {
    console.log(userName + " odaya katıldı. El sıkışma başlıyor...");
    const pc = createPeerConnection(remoteId);

    // Teklifi (Offer) oluştur ve gönder
    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);

    connection.invoke("SendOffer", JSON.stringify(offer), remoteId);
});

// 3. PeerConnection Oluşturucu (Fabrika)
function createPeerConnection(remoteId) {
    const pc = new RTCPeerConnection(servers);
    peerConnections[remoteId] = pc;

    // Kendi görüntümüzü bu bağlantıya ekle
    localStream.getTracks().forEach(track => pc.addTrack(track, localStream));

    // Karşı tarafın görüntüsü geldiğinde
    pc.ontrack = event => {
        addRemoteVideo(remoteId, event.streams[0]);
    };

    // ICE Candidate (Ağ bilgisi) üretildiğinde
    pc.onicecandidate = event => {
        if (event.candidate) {
            connection.invoke("SendIceCandidate", JSON.stringify(event.candidate), remoteId);
        }
    };

    return pc;
}

// 4. Dinamik Video Kutusu Ekleme (Grid Yapısı İçin)
function addRemoteVideo(remoteId, stream) {
    if (document.getElementById(`video-${remoteId}`)) return;

    const videoGrid = document.getElementById("video-grid"); // HTML'deki kapsayıcı
    const video = document.createElement("video");
    video.id = `video-${remoteId}`;
    video.srcObject = stream;
    video.autoplay = true;
    video.className = "video-item"; // Senin CSS'indeki class
    videoGrid.appendChild(video);
}