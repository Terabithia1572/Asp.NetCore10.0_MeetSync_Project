"use strict";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/meetingHub")
    .withAutomaticReconnect()
    .build();

let localStream;
let peerConnections = {};
let isModerator = false;
let isRoomLocked = false;
const servers = { iceServers: [{ urls: "stun:stun.l.google.com:19302" }] };

// Recording state
let mediaRecorder = null;
let currentRecordingId = null;
let recordingStartTime = null;

// Speech Recognition (Web Speech API)
let speechRecognition = null;
let isCcEnabled = false;

// Global participant and lobby state
let allParticipants = [];
let waitingParticipants = [];
window.isModerator = false;
window.myConnectionId = null;

async function init() {
    // 1. Acquire local media stream safely with fallbacks
    try {
        const prefMic = localStorage.getItem("pref_mic") === "true";
        const prefCam = localStorage.getItem("pref_cam") === "true";

        localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
        localStream.getAudioTracks()[0].enabled = prefMic;
        localStream.getVideoTracks()[0].enabled = prefCam;

        updateBtnUI("btn-mic", prefMic, 'mic');
        updateBtnUI("btn-cam", prefCam, 'videocam');
        const localVidEl = document.getElementById("localVideo");
        if (localVidEl) localVidEl.srcObject = localStream;
    } catch (mediaErr) {
        console.warn("Full video+audio media acquisition failed:", mediaErr);
        try {
            localStream = await navigator.mediaDevices.getUserMedia({ audio: true });
            const localVidEl = document.getElementById("localVideo");
            if (localVidEl) localVidEl.srcObject = localStream;
        } catch (audioErr) {
            console.warn("Audio-only media acquisition failed. Fallback to empty MediaStream:", audioErr);
            localStream = new MediaStream();
        }
    }

    // 2. Ensure SignalR connection starts BEFORE calling JoinRoom
    try {
        if (connection.state === signalR.HubConnectionState.Disconnected) {
            await connection.start();
        }
        window.myConnectionId = connection.connectionId;
        console.log(`SignalR connection established successfully. ConnectionId: ${window.myConnectionId}`);

        const roomPassword = new URLSearchParams(window.location.search).get("pwd") || "";
        await connection.invoke("JoinRoom", roomName, userName, roomPassword);

        initSpeechRecognition();
    } catch (err) {
        console.error("Failed to start SignalR connection or JoinRoom:", err);
    }
}

function updateBtnUI(id, state, icon) {
    const btn = document.getElementById(id);
    if (btn) {
        btn.classList.toggle("is-off", !state);
        btn.classList.toggle("is-on", state);
        const span = btn.querySelector('span');
        if (span) span.textContent = state ? icon : icon + '_off';
    }
}

function createPeerConnection(remoteId, remoteName) {
    if (peerConnections[remoteId]) {
        return peerConnections[remoteId];
    }

    const pc = new RTCPeerConnection(servers);
    pc.iceCandidatesQueue = [];
    peerConnections[remoteId] = pc;

    if (localStream) {
        localStream.getTracks().forEach(track => pc.addTrack(track, localStream));
    }

    pc.onicecandidate = e => {
        if (e.candidate) {
            connection.invoke("SendIceCandidate", JSON.stringify(e.candidate), remoteId).catch(err => console.error("SendIceCandidate error:", err));
        }
    };
    pc.ontrack = e => addRemoteVideoUI(remoteId, remoteName, e.streams[0]);

    return pc;
}

function addRemoteVideoUI(remoteId, remoteName, stream) {
    if (document.getElementById(`container-${remoteId}`)) return;

    const grid = document.getElementById("video-grid");

    const container = document.createElement("div");
    container.id = `container-${remoteId}`;
    container.className = "relative group rounded-[2rem] overflow-hidden bg-zinc-900 border border-white/5 aspect-video shadow-2xl transition-all hover:border-primary/50 hover:scale-[1.02] cursor-pointer active-speaker-glow";
    container.onclick = () => container.requestFullscreen();

    const video = document.createElement("video");
    video.id = `video-${remoteId}`;
    video.autoplay = true;
    video.playsinline = true;
    video.srcObject = stream;
    video.className = "w-full h-full object-cover transition-transform duration-700 group-hover:scale-105";

    const label = document.createElement("div");
    label.className = "absolute bottom-6 left-6 flex items-center gap-3 px-4 py-2 rounded-2xl bg-black/40 backdrop-blur-xl border border-white/10 shadow-2xl";
    label.innerHTML = `
        <span class="material-symbols-outlined text-primary text-sm">equalizer</span>
        <span class="text-xs font-black tracking-tight text-white uppercase r-name-${remoteId}">${remoteName}</span>
    `;

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
}

function updateParticipantListUI(participants) {
    allParticipants = participants;
    const ul = document.getElementById('participants-ul');
    const count = document.getElementById('participant-count');
    const panelCount = document.getElementById('participantsPanelCount');

    if (count) count.textContent = participants.length === 1 ? '1 Katılımcı' : `${participants.length} Katılımcı`;
    if (panelCount) panelCount.textContent = `Odadaki kişi sayısı: ${participants.length}`;

    if (!ul) return;

    ul.innerHTML = participants.map(p => {
        const isMe = p.connectionId === window.myConnectionId;
        const showControls = window.isModerator && !isMe;

        return `
        <li class="flex items-center justify-between p-4 rounded-2xl bg-white/5 border border-white/5 hover:border-primary/30 transition-all">
            <div class="flex items-center gap-4 flex-1">
                <div class="size-10 rounded-xl bg-primary/20 flex items-center justify-center">
                    <span class="material-symbols-outlined text-primary text-xl">person</span>
                </div>
                <div class="flex-1">
                    <div class="flex items-center gap-2">
                        <span class="text-sm font-bold text-white r-name-${p.connectionId}">${p.userName}</span>
                        ${p.isModerator ? '<span class="text-[9px] font-black tracking-wider bg-primary/20 text-primary px-2 py-0.5 rounded-full">MOD</span>' : ''}
                        ${isMe ? '<span class="text-[9px] font-black tracking-wider bg-green-500/20 text-green-500 px-2 py-0.5 rounded-full">SİZ</span>' : ''}
                    </div>
                </div>
            </div>

            ${showControls ? `
            <div class="flex items-center gap-1">
                <button onclick="muteParticipant('${p.connectionId}', 'audio')" title="Sesi Kapat" class="size-8 rounded-lg bg-white/10 hover:bg-red-500/20 text-slate-300 hover:text-red-400 flex items-center justify-center transition-all">
                    <span class="material-symbols-outlined text-sm">mic_off</span>
                </button>
                <button onclick="muteParticipant('${p.connectionId}', 'video')" title="Kamerayı Kapat" class="size-8 rounded-lg bg-white/10 hover:bg-red-500/20 text-slate-300 hover:text-red-400 flex items-center justify-center transition-all">
                    <span class="material-symbols-outlined text-sm">videocam_off</span>
                </button>
                <button onclick="kickParticipant('${p.connectionId}')" title="Odadan Çıkar" class="size-8 rounded-lg bg-red-600/20 hover:bg-red-600 text-red-400 hover:text-white flex items-center justify-center transition-all">
                    <span class="material-symbols-outlined text-sm">person_remove</span>
                </button>
            </div>
            ` : ''}
        </li>
        `;
    }).join('');
}

function updateWaitingListUI(waitingList) {
    waitingParticipants = waitingList;
    let waitingContainer = document.getElementById('waiting-room-section');

    if (!waitingContainer && window.isModerator && waitingList.length > 0) {
        const panel = document.getElementById('participants-panel');
        if (panel) {
            waitingContainer = document.createElement('div');
            waitingContainer.id = 'waiting-room-section';
            waitingContainer.className = 'p-6 border-b border-white/10 bg-amber-500/10';
            panel.insertBefore(waitingContainer, panel.children[1]);
        }
    }

    if (waitingContainer) {
        if (waitingList.length === 0) {
            waitingContainer.remove();
            return;
        }

        waitingContainer.innerHTML = `
            <div class="flex items-center justify-between mb-3">
                <h4 class="text-xs font-black tracking-wider text-amber-400 uppercase flex items-center gap-2">
                    <span class="material-symbols-outlined text-sm">hourglass_top</span>
                    Bekleme Odası (${waitingList.length})
                </h4>
            </div>
            <div class="space-y-2">
                ${waitingList.map(w => `
                    <div class="flex items-center justify-between bg-black/30 p-3 rounded-xl border border-amber-500/20">
                        <span class="text-xs font-bold text-white">${w.userName}</span>
                        <div class="flex gap-2">
                            <button onclick="approveParticipant('${w.connectionId}')" class="px-3 py-1 bg-green-600 hover:bg-green-500 text-white rounded-lg text-xs font-bold transition-all">Onayla</button>
                            <button onclick="rejectParticipant('${w.connectionId}')" class="px-3 py-1 bg-red-600 hover:bg-red-500 text-white rounded-lg text-xs font-bold transition-all">Reddet</button>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;
    }
}

// SignalR Events
connection.on("ModeratorStatus", status => {
    isModerator = status;
    window.isModerator = status;

    document.getElementById('btn-end-meeting')?.classList.toggle('hidden', !status);
    document.getElementById('btn-lock-room')?.classList.toggle('hidden', !status);
    document.getElementById('btn-rec')?.classList.toggle('hidden', !status);
    document.getElementById('btn-ai-summary')?.classList.toggle('hidden', !status);

    if (allParticipants.length > 0) {
        updateParticipantListUI(allParticipants);
    }
});

connection.on("UpdateParticipants", participants => {
    updateParticipantListUI(participants);
});

connection.on("UpdateWaitingList", waitingList => {
    updateWaitingListUI(waitingList);
});

connection.on("UserWaitingInLobby", (connectionId, userName) => {
    console.log(`User waiting in lobby: ${userName} (${connectionId})`);
});

connection.on("PlacedInWaitingRoom", () => {
    showWaitingRoomModal();
});

connection.on("LobbyApproved", async () => {
    hideWaitingRoomModal();
    const roomPassword = new URLSearchParams(window.location.search).get("pwd") || "";
    if (connection.state === signalR.HubConnectionState.Connected) {
        await connection.invoke("JoinRoom", roomName, userName, roomPassword);
    }
});

connection.on("LobbyRejected", () => {
    alert("Toplantı sahibi katılım isteğinizi reddetti.");
    window.location.href = "/Dashboard";
});

connection.on("RoomIsLocked", () => {
    alert("Bu oda moderatör tarafından kilitlenmiştir.");
    window.location.href = "/Dashboard";
});

connection.on("InvalidRoomPassword", () => {
    const pwd = prompt("Bu oda şifrelidir. Lütfen oda şifresini giriniz:");
    if (pwd) {
        const url = new URL(window.location.href);
        url.searchParams.set("pwd", pwd);
        window.location.href = url.toString();
    } else {
        window.location.href = "/Dashboard";
    }
});

// AI Live Captions & Summary Events
let captionTimeout = null;
connection.on("ReceiveLiveCaption", (speakerName, text) => {
    const overlay = document.getElementById("live-caption-overlay");
    const speakerEl = document.getElementById("caption-speaker");
    const textEl = document.getElementById("caption-text");

    if (overlay && speakerEl && textEl) {
        speakerEl.textContent = speakerName + ":";
        textEl.textContent = text;
        overlay.classList.remove("hidden");

        if (captionTimeout) clearTimeout(captionTimeout);
        captionTimeout = setTimeout(() => {
            overlay.classList.add("hidden");
        }, 5000);
    }
});

connection.on("MeetingSummaryReady", summary => {
    renderAiSummaryModal(summary);
});

function initSpeechRecognition() {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
        console.warn("Web Speech API is not supported in this browser.");
        return;
    }

    speechRecognition = new SpeechRecognition();
    speechRecognition.continuous = true;
    speechRecognition.interimResults = false;
    speechRecognition.lang = "tr-TR"; // Default language Turkish (can be customized)

    speechRecognition.onresult = (event) => {
        for (let i = event.resultIndex; i < event.results.length; ++i) {
            if (event.results[i].isFinal) {
                const transcriptText = event.results[i][0].transcript;
                if (transcriptText && transcriptText.trim() !== "") {
                    connection.invoke("SendTranscriptionChunk", roomName, transcriptText.trim()).catch(err => console.error(err));
                }
            }
        }
    };

    speechRecognition.onerror = (e) => {
        console.warn("SpeechRecognition error:", e.error);
    };

    speechRecognition.onend = () => {
        if (isCcEnabled) {
            try { speechRecognition.start(); } catch { }
        }
    };
}

function toggleClosedCaptions() {
    isCcEnabled = !isCcEnabled;
    const btn = document.getElementById("btn-cc");
    if (btn) btn.classList.toggle("is-on", isCcEnabled);

    if (speechRecognition) {
        if (isCcEnabled) {
            try { speechRecognition.start(); } catch { }
            alert("Canlı Altyazı (CC) açıldı. Konuşmalarınız canlı altyazıya dönüştürülüyor.");
        } else {
            try { speechRecognition.stop(); } catch { }
            document.getElementById("live-caption-overlay")?.classList.add("hidden");
            alert("Canlı Altyazı (CC) kapatıldı.");
        }
    } else {
        alert("Tarayıcınız Web Speech API canlı altyazı özelliğini desteklemiyor.");
    }
}

function generateAiSummary() {
    if (!window.isModerator) return;
    alert("Yapay Zeka toplantı özeti ve aksiyon maddeleri oluşturuluyor...");
    connection.invoke("GenerateMeetingSummary", roomName).catch(err => console.error(err));
}

function renderAiSummaryModal(summary) {
    const modal = document.getElementById("aiSummaryModal");
    if (!modal) return;

    document.getElementById("aiSummaryTimestamp").textContent = "Oluşturuldu: " + new Date(summary.generatedAt).toLocaleTimeString();
    document.getElementById("aiSummaryText").textContent = summary.summaryText;

    const decisionsUl = document.getElementById("aiKeyDecisions");
    if (decisionsUl) {
        decisionsUl.innerHTML = (summary.keyDecisions || []).map(d => `
            <li class="flex items-start gap-2 bg-green-500/10 border border-green-500/20 p-3 rounded-xl">
                <span class="material-symbols-outlined text-green-400 text-sm mt-0.5">check_circle</span>
                <span>${d}</span>
            </li>
        `).join('');
    }

    const actionUl = document.getElementById("aiActionItems");
    if (actionUl) {
        actionUl.innerHTML = (summary.actionItems || []).map(a => `
            <li class="flex items-start gap-2 bg-blue-500/10 border border-blue-500/20 p-3 rounded-xl">
                <span class="material-symbols-outlined text-blue-400 text-sm mt-0.5">task_alt</span>
                <span>${a}</span>
            </li>
        `).join('');
    }

    modal.classList.remove("hidden");
}

function closeAiSummaryModal() {
    document.getElementById("aiSummaryModal")?.classList.add("hidden");
}

// Recording Events
connection.on("RecordingStarted", recordingId => {
    currentRecordingId = recordingId;
    recordingStartTime = Date.now();

    document.getElementById("rec-badge")?.classList.remove("hidden");
    const recBtn = document.getElementById("btn-rec");
    if (recBtn) recBtn.classList.add("is-on");

    if (window.isModerator) {
        startMediaRecorder(recordingId);
    }
});

connection.on("RecordingStopped", (recordingId, summary) => {
    document.getElementById("rec-badge")?.classList.add("hidden");
    const recBtn = document.getElementById("btn-rec");
    if (recBtn) recBtn.classList.remove("is-on");

    if (window.isModerator && mediaRecorder && mediaRecorder.state !== "inactive") {
        mediaRecorder.stop();
    }
    currentRecordingId = null;
    alert(`Toplantı kaydı tamamlandı. Süre: ${summary?.durationSeconds || 0} saniye.`);
});

function startMediaRecorder(recordingId) {
    try {
        const stream = document.getElementById("localVideo")?.srcObject || localStream;
        if (!stream) return;

        const mimeType = MediaRecorder.isTypeSupported("video/webm;codecs=vp8,opus")
            ? "video/webm;codecs=vp8,opus"
            : "video/webm";

        mediaRecorder = new MediaRecorder(stream, { mimeType });

        mediaRecorder.ondataavailable = async (e) => {
            if (e.data && e.data.size > 0 && currentRecordingId) {
                const reader = new FileReader();
                reader.onloadend = () => {
                    const base64data = reader.result.split(',')[1];
                    connection.invoke("UploadRecordingChunk", roomName, recordingId, base64data).catch(err => console.error(err));
                };
                reader.readAsDataURL(e.data);
            }
        };

        mediaRecorder.start(3000);
    } catch (err) {
        console.error("MediaRecorder start error:", err);
    }
}

function toggleRecording() {
    if (!currentRecordingId) {
        connection.invoke("StartRecording", roomName).catch(err => console.error(err));
    } else {
        const duration = Math.round((Date.now() - (recordingStartTime || Date.now())) / 1000);
        connection.invoke("StopRecording", roomName, currentRecordingId, duration).catch(err => console.error(err));
    }
}

connection.on("UserJoined", async (name, id) => {
    console.log(`UserJoined event received for ${name} (${id})`);
    try {
        const pc = createPeerConnection(id, name);
        const offer = await pc.createOffer();
        await pc.setLocalDescription(offer);
        await connection.invoke("SendOffer", JSON.stringify(offer), id);
    } catch (err) {
        console.error("Error creating offer for new user:", err);
    }
});

connection.on("ReceiveOffer", async (offer, id) => {
    console.log(`ReceiveOffer event received from ${id}`);
    try {
        const remoteParticipant = allParticipants.find(p => p.connectionId === id);
        const remoteName = remoteParticipant ? remoteParticipant.userName : "Katılımcı";

        const pc = createPeerConnection(id, remoteName);
        await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(offer)));

        // Flush any ICE candidates queued before remote description was set
        if (pc.iceCandidatesQueue && pc.iceCandidatesQueue.length > 0) {
            for (const cand of pc.iceCandidatesQueue) {
                await pc.addIceCandidate(cand).catch(e => console.warn("Queued ICE candidate error:", e));
            }
            pc.iceCandidatesQueue = [];
        }

        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        await connection.invoke("SendAnswer", JSON.stringify(answer), id);
    } catch (err) {
        console.error("Error handling ReceiveOffer:", err);
    }
});

connection.on("ReceiveAnswer", async (ans, id) => {
    console.log(`ReceiveAnswer event received from ${id}`);
    try {
        const pc = peerConnections[id];
        if (pc) {
            await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(ans)));

            // Flush any ICE candidates queued before remote description was set
            if (pc.iceCandidatesQueue && pc.iceCandidatesQueue.length > 0) {
                for (const cand of pc.iceCandidatesQueue) {
                    await pc.addIceCandidate(cand).catch(e => console.warn("Queued ICE candidate error:", e));
                }
                pc.iceCandidatesQueue = [];
            }
        }
    } catch (err) {
        console.error("Error handling ReceiveAnswer:", err);
    }
});

connection.on("ReceiveIceCandidate", async (can, id) => {
    try {
        const candidate = new RTCIceCandidate(JSON.parse(can));
        const pc = peerConnections[id];
        if (pc && pc.remoteDescription && pc.remoteDescription.type) {
            await pc.addIceCandidate(candidate);
        } else if (pc) {
            if (!pc.iceCandidatesQueue) pc.iceCandidatesQueue = [];
            pc.iceCandidatesQueue.push(candidate);
        }
    } catch (err) {
        console.error("Error adding ICE candidate:", err);
    }
});

connection.on("UserDisconnected", id => {
    document.getElementById(`container-${id}`)?.remove();
    delete peerConnections[id];
});

connection.on("RoomClosedByModerator", () => {
    alert("Moderatör toplantıyı sonlandırdı.");
    window.location.href = "/Dashboard";
});

connection.on("UserNameUpdated", (id, name) => {
    document.querySelectorAll(`.r-name-${id}`).forEach(el => el.textContent = name);
});

connection.on("ModeratorChanged", newModConnectionId => {
    if (newModConnectionId === window.myConnectionId) {
        window.isModerator = true;
        isModerator = true;
        document.getElementById('btn-end-meeting')?.classList.remove('hidden');
        document.getElementById('btn-lock-room')?.classList.remove('hidden');
        document.getElementById('btn-rec')?.classList.remove('hidden');
        document.getElementById('btn-ai-summary')?.classList.remove('hidden');
        alert("Toplantının yeni moderatörü sizsiniz.");
    }
});

// Remote Moderation Events
connection.on("ParticipantMuted", (targetId, mediaType) => {
    if (mediaType === "audio") {
        localStream.getAudioTracks()[0].enabled = false;
        updateBtnUI("btn-mic", false, 'mic');
        alert("Mikrofonunuz moderatör tarafından kapatıldı.");
    } else if (mediaType === "video") {
        localStream.getVideoTracks()[0].enabled = false;
        updateBtnUI("btn-cam", false, 'videocam');
        alert("Kameranız moderatör tarafından kapatıldı.");
    }
});

connection.on("AllParticipantsMuted", mediaType => {
    if (!window.isModerator) {
        if (mediaType === "audio") {
            localStream.getAudioTracks()[0].enabled = false;
            updateBtnUI("btn-mic", false, 'mic');
            alert("Tüm katılımcıların mikrofonu kapatıldı.");
        } else if (mediaType === "video") {
            localStream.getVideoTracks()[0].enabled = false;
            updateBtnUI("btn-cam", false, 'videocam');
            alert("Tüm katılımcıların kamerası kapatıldı.");
        }
    }
});

connection.on("ForceKicked", reason => {
    alert(`Toplantıdan çıkarıldınız. Sebep: ${reason}`);
    window.location.href = "/Dashboard";
});

connection.on("RoomLockStatusChanged", isLocked => {
    isRoomLocked = isLocked;
    const lockBtn = document.getElementById("btn-lock-room");
    if (lockBtn) {
        lockBtn.classList.toggle("is-on", isLocked);
        lockBtn.classList.toggle("is-off", !isLocked);
        lockBtn.title = isLocked ? "Oda Kilitli (Açmak için tıklayın)" : "Oda Kilitsiz (Kilitlemek için tıklayın)";
    }
    alert(isLocked ? "Oda moderatör tarafından kilitlendi." : "Oda kilidi kaldırıldı.");
});

// Moderator Actions
function muteParticipant(targetId, mediaType) {
    connection.invoke("MuteParticipant", roomName, targetId, mediaType).catch(err => console.error(err));
}

function kickParticipant(targetId) {
    const reason = prompt("Çıkarma sebebini belirtin:", "Moderatör kararı.");
    if (reason !== null) {
        connection.invoke("KickParticipant", roomName, targetId, reason).catch(err => console.error(err));
    }
}

function approveParticipant(targetId) {
    connection.invoke("ApproveParticipant", roomName, targetId).catch(err => console.error(err));
}

function rejectParticipant(targetId) {
    connection.invoke("RejectParticipant", roomName, targetId).catch(err => console.error(err));
}

function toggleRoomLock() {
    const newLockState = !isRoomLocked;
    connection.invoke("SetRoomLock", roomName, newLockState).catch(err => console.error(err));
}

function changeMyName() {
    const newName = prompt("Yeni isminizi giriniz:", userName);
    if (newName && newName.trim() !== "") {
        userName = newName.trim();
        document.getElementById("local-name-label").textContent = userName.toUpperCase() + " (SİZ)";
        connection.invoke("UpdateUserName", roomName, userName).catch(err => console.error(err));
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
        <div class="${isMe ? 'bg-primary' : 'bg-zinc-800'} rounded-lg p-2 max-w-[80%] text-sm text-white">${msg}</div>
    </div>`;
    box.scrollTop = box.scrollHeight;
    if (!isMe && typeof showChatBadge === "function") showChatBadge();
});

// Waiting Room Modal Helpers
function showWaitingRoomModal() {
    let modal = document.getElementById("waitingRoomModal");
    if (!modal) {
        modal = document.createElement("div");
        modal.id = "waitingRoomModal";
        modal.className = "fixed inset-0 bg-black/90 backdrop-blur-md z-[200] flex items-center justify-center p-6";
        modal.innerHTML = `
            <div class="bg-zinc-900 border border-white/10 rounded-3xl p-10 max-w-md w-full text-center shadow-2xl">
                <div class="size-16 bg-amber-500/10 rounded-2xl flex items-center justify-center mx-auto mb-4">
                    <span class="material-symbols-outlined text-amber-500 text-4xl animate-spin">hourglass_top</span>
                </div>
                <h3 class="text-2xl font-black text-white mb-2">Bekleme Odasındasınız</h3>
                <p class="text-slate-400 text-sm">Toplantı sahibi katılım isteğinizi inceledikten sonra odaya alınacaksınız. Lütfen bekleyiniz...</p>
            </div>
        `;
        document.body.appendChild(modal);
    }
    modal.classList.remove("hidden");
}

function hideWaitingRoomModal() {
    document.getElementById("waitingRoomModal")?.classList.add("hidden");
}

// Mic / Cam Toggles
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

function endMeetingConfirm() {
    if (confirm("Toplantıyı herkes için sonlandırmak istediğinize emin misiniz?")) {
        connection.invoke("EndMeeting", roomName).catch(err => console.error(err));
        window.location.href = '/Dashboard';
    }
}

function leaveMeeting() {
    if (window.isModerator && allParticipants.length > 1) {
        showModeratorModal(allParticipants);
    } else {
        window.location.href = '/Dashboard';
    }
}

function showModeratorModal(participants) {
    const modal = document.getElementById('moderatorModal');
    const candidates = document.getElementById('moderatorCandidates');
    const others = participants.filter(p => p.connectionId !== window.myConnectionId);

    if (others.length === 0) {
        connection.invoke("EndMeeting", roomName).catch(err => console.error(err));
        window.location.href = '/Dashboard';
        return;
    }

    candidates.innerHTML = others.map(p => `
        <button onclick="assignModerator('${p.connectionId}')" 
                class="w-full flex items-center gap-4 p-4 rounded-2xl bg-white/5 hover:bg-primary/10 border border-white/5 hover:border-primary/30 transition-all text-left">
            <div class="size-10 rounded-xl bg-primary/20 flex items-center justify-center">
                <span class="material-symbols-outlined text-primary">person</span>
            </div>
            <div class="flex-1">
                <span class="text-sm font-bold text-white">${p.userName}</span>
                <p class="text-xs text-slate-500">Moderatör olarak ata</p>
            </div>
            <span class="material-symbols-outlined text-slate-500">arrow_forward</span>
        </button>
    `).join('');

    modal.classList.remove('hidden');
}

function assignModerator(connectionId) {
    connection.invoke("AssignModerator", roomName, connectionId).catch(err => console.error(err));
    document.getElementById('moderatorModal').classList.add('hidden');
    window.location.href = '/Dashboard';
}

function cancelLeave() {
    document.getElementById('moderatorModal').classList.add('hidden');
}

init();