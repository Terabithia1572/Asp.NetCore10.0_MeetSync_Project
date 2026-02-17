"use strict";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/meetingHub")
    .withAutomaticReconnect()
    .build();

let localStream;
let peerConnections = {};
let isModerator = false;
const servers = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] };

async function init() {
    try {
        const prefMic = localStorage.getItem("pref_mic") === "true";
        const prefCam = localStorage.getItem("pref_cam") === "true";

        localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
        localStream.getAudioTracks()[0].enabled = prefMic;
        localStream.getVideoTracks()[0].enabled = prefCam;

        updateBtnUI("btn-mic", prefMic, 'mic');
        updateBtnUI("btn-cam", prefCam, 'videocam');

        document.getElementById("localVideo").srcObject = localStream;
        await connection.start();
        await connection.invoke("JoinRoom", roomName, userName);
    } catch (err) { console.error(err); }
}

function updateBtnUI(id, state, icon) {
    const btn = document.getElementById(id);
    if (btn) {
        btn.classList.toggle("bg-red-500/20", !state);
        btn.querySelector('span').textContent = state ? icon : icon + '_off';
    }
}

function createPeerConnection(remoteId, remoteName) {
    const pc = new RTCPeerConnection(servers);
    peerConnections[remoteId] = pc;
    localStream.getTracks().forEach(track => pc.addTrack(track, localStream));

    pc.onicecandidate = e => { if (e.candidate) connection.invoke("SendIceCandidate", JSON.stringify(e.candidate), remoteId); };
    pc.ontrack = e => addRemoteVideoUI(remoteId, remoteName, e.streams[0]);

    return pc;
}

function addRemoteVideoUI(remoteId, remoteName, stream) {
    if (document.getElementById(`container-${remoteId}`)) return;

    const grid = document.getElementById("video-grid");

    // Stitch AI Tasarımı: Container
    const container = document.createElement("div");
    container.id = `container-${remoteId}`;
    container.className = "relative group rounded-[2rem] overflow-hidden bg-zinc-900 border border-white/5 aspect-video shadow-2xl transition-all hover:border-primary/50 hover:scale-[1.02] cursor-pointer active-speaker-glow";
    container.onclick = () => container.requestFullscreen();

    // Video Elementi
    const video = document.createElement("video");
    video.id = `video-${remoteId}`;
    video.autoplay = true;
    video.playsinline = true;
    video.srcObject = stream;
    video.className = "w-full h-full object-cover transition-transform duration-700 group-hover:scale-105";

    // Stitch AI Tarzı İsim Etiketi (Glassmorphism)
    const label = document.createElement("div");
    label.className = "absolute bottom-6 left-6 flex items-center gap-3 px-4 py-2 rounded-2xl bg-black/40 backdrop-blur-xl border border-white/10 shadow-2xl";
    label.innerHTML = `
        <span class="material-symbols-outlined text-primary text-sm">equalizer</span>
        <span class="text-xs font-black tracking-tight text-white uppercase r-name-${remoteId}">${remoteName}</span>
    `;

    // Hover'da çıkacak aksiyon menüsü (8. fotodaki gibi gizli butonlar)
    const overlay = document.createElement("div");
    overlay.className = "absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-transparent opacity-0 group-hover:opacity-100 transition-opacity flex items-end justify-end p-6";
    overlay.innerHTML = `
        <div class="flex gap-2">
            <button class="size-10 rounded-xl bg-white/10 backdrop-blur-md hover:bg-primary transition-colors flex items-center justify-center">
                <span class="material-symbols-outlined text-sm">fullscreen</span>
            </button>
        </div>
    `;

    container.appendChild(video);
    container.appendChild(label);
    container.appendChild(overlay);
    grid.appendChild(container);

    updateParticipantList(remoteId, remoteName, true);
}
function updateParticipantList(id, name, isJoining) {
    const list = document.getElementById("participant-list");
    if (isJoining) {
        const li = document.createElement("li");
        li.id = `list-item-${id}`;
        li.className = "flex items-center justify-between bg-white/5 p-2 rounded-lg";
        li.innerHTML = `<span class="text-sm r-name-${id}">${name}</span>`;
        list.appendChild(li);
    } else {
        const el = document.getElementById(`list-item-${id}`);
        if (el) el.remove();
    }
    document.getElementById("participant-count").textContent = (Object.keys(peerConnections).length + 1) + " Participants";
}

// SignalR Events
connection.on("ModeratorStatus", status => isModerator = status);
connection.on("UserJoined", async (name, id) => {
    const pc = createPeerConnection(id, name);
    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);
    connection.invoke("SendOffer", JSON.stringify(offer), id);
});
connection.on("ReceiveOffer", async (offer, id) => {
    const pc = createPeerConnection(id, "User");
    await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(offer)));
    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    connection.invoke("SendAnswer", JSON.stringify(answer), id);
});
connection.on("ReceiveAnswer", async (ans, id) => await peerConnections[id].setRemoteDescription(new RTCSessionDescription(JSON.parse(ans))));
connection.on("ReceiveIceCandidate", async (can, id) => await peerConnections[id].addIceCandidate(new RTCIceCandidate(JSON.parse(can))));
connection.on("UserDisconnected", id => {
    document.getElementById(`container-${id}`)?.remove();
    updateParticipantList(id, "", false);
    delete peerConnections[id];
});
connection.on("RoomClosedByModerator", () => { alert("Moderator ended the meeting."); window.location.href = "/Dashboard"; });
connection.on("UserNameUpdated", (id, name) => {
    document.querySelectorAll(`.r-name-${id}`).forEach(el => el.textContent = name);
});

// Actions
function changeMyName() {
    const newName = prompt("Change your display name:", userName);
    if (newName) {
        document.getElementById("local-name-label").textContent = newName + " (You)";
        connection.invoke("UpdateUserName", roomName, newName);
    }
}

document.getElementById("btn-send-chat").onclick = () => {
    const inp = document.getElementById("chat-input");
    if (inp.value) { connection.invoke("SendMessage", roomName, userName, inp.value); inp.value = ""; }
};

connection.on("ReceiveMessage", (user, msg) => {
    const box = document.getElementById("chat-messages");
    const isMe = user === userName;
    box.innerHTML += `<div class="flex flex-col ${isMe ? 'items-end' : 'items-start'} gap-1">
        <span class="text-[10px] text-white/40">${user}</span>
        <div class="${isMe ? 'bg-primary' : 'bg-zinc-800'} rounded-lg p-2 max-w-[80%]">${msg}</div>
    </div>`;
    box.scrollTop = box.scrollHeight;
});

// Mic/Cam Toggles
document.getElementById("btn-mic").onclick = function () {
    const state = !localStream.getAudioTracks()[0].enabled;
    localStream.getAudioTracks()[0].enabled = state;
    updateBtnUI("btn-mic", state, 'mic');
};
document.getElementById("btn-cam").onclick = function () {
    const state = !localStream.getVideoTracks()[0].enabled;
    localStream.getVideoTracks()[0].enabled = state;
    updateBtnUI("btn-cam", state, 'videocam');
};

// Screen Share
let isSharing = false;
document.getElementById("btn-share").onclick = async function () {
    if (!isSharing) {
        const stream = await navigator.mediaDevices.getDisplayMedia({ video: true });
        const track = stream.getVideoTracks()[0];
        for (let id in peerConnections) peerConnections[id].getSenders().find(s => s.track.kind === 'video').replaceTrack(track);
        document.getElementById("localVideo").srcObject = stream;
        track.onended = () => stopShare();
        isSharing = true;
    } else { stopShare(); }
};
function stopShare() {
    const track = localStream.getVideoTracks()[0];
    for (let id in peerConnections) peerConnections[id].getSenders().find(s => s.track.kind === 'video').replaceTrack(track);
    document.getElementById("localVideo").srcObject = localStream;
    isSharing = false;
}

init();