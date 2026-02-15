const connection = new signalR.HubConnectionBuilder()
    .withUrl("/meetingHub")
    .build();

let peerConnection;
let localStream;
let roomName;
let userName;

const servers = {
    iceServers: [{ urls: "stun:stun.l.google.com:19302" }]
};

connection.start();

async function joinRoom() {
    roomName = document.getElementById("roomName").value;
    userName = document.getElementById("userName").value;

    await startMedia();
    createPeerConnection();

    connection.invoke("JoinRoom", roomName, userName);
}

function sendMessage() {
    const message = document.getElementById("messageInput").value;
    connection.invoke("SendMessage", roomName, userName, message);
}

connection.on("UserJoined", user => {
    const li = document.createElement("li");
    li.textContent = user + " joined";
    document.getElementById("messages").appendChild(li);
});

connection.on("ReceiveMessage", (user, message) => {
    const li = document.createElement("li");
    li.textContent = user + ": " + message;
    document.getElementById("messages").appendChild(li);
});

async function startMedia() {
    localStream = await navigator.mediaDevices.getUserMedia({
        video: true,
        audio: true
    });

    document.getElementById("localVideo").srcObject = localStream;
}

function createPeerConnection() {
    peerConnection = new RTCPeerConnection(servers);

    localStream.getTracks().forEach(track => {
        peerConnection.addTrack(track, localStream);
    });

    peerConnection.ontrack = event => {
        document.getElementById("remoteVideo").srcObject = event.streams[0];
    };

    peerConnection.onicecandidate = event => {
        if (event.candidate) {
            connection.invoke("SendIceCandidate", roomName, JSON.stringify(event.candidate));
        }
    };
}

async function startConnection() {
    const offer = await peerConnection.createOffer();
    await peerConnection.setLocalDescription(offer);

    connection.invoke("SendOffer", roomName, JSON.stringify(offer));
}

connection.on("ReceiveOffer", async offer => {
    await peerConnection.setRemoteDescription(new RTCSessionDescription(JSON.parse(offer)));

    const answer = await peerConnection.createAnswer();
    await peerConnection.setLocalDescription(answer);

    connection.invoke("SendAnswer", roomName, JSON.stringify(answer));
});

connection.on("ReceiveAnswer", async answer => {
    await peerConnection.setRemoteDescription(new RTCSessionDescription(JSON.parse(answer)));
});

connection.on("ReceiveIceCandidate", async candidate => {
    await peerConnection.addIceCandidate(new RTCIceCandidate(JSON.parse(candidate)));
});

/* 🎛 TOGGLE CAMERA */

function toggleCamera() {
    const videoTrack = localStream.getVideoTracks()[0];
    videoTrack.enabled = !videoTrack.enabled;
}

/* 🎛 TOGGLE MIC */

function toggleMic() {
    const audioTrack = localStream.getAudioTracks()[0];
    audioTrack.enabled = !audioTrack.enabled;
}
