"use strict";

const shareProfiles = {
    "480": { width: 854, height: 480, bitrate: 1000000 },
    "720": { width: 1280, height: 720, bitrate: 2500000 },
    "1080": { width: 1920, height: 1080, bitrate: 5000000 },
    "1440": { width: 2560, height: 1440, bitrate: 8000000 },
    "2160": { width: 3840, height: 2160, bitrate: 16000000 }
};
let screenStream = null, isSharing = false, shareBusy = false;
let mediaRecorder = null, currentRecordingId = null, recordingStartTime = null;
let recordingOwner = null, recordingBusy = false, recordingCleanup = null;
let recordingQueue = Promise.resolve(), recordingError = null, queuedRecordingBytes = 0;
let lockBusy = false;

function mediaNotice(message, error = false) {
    const el = document.getElementById("media-status");
    el.textContent = message;
    el.classList.toggle("media-error", error);
}

function updateModeratorControls() {
    const moderator = !!window.isModerator;
    const restricted = "Yalnızca moderatör kullanabilir";
    for (const [id, title] of [["btn-end-meeting", "Toplantıyı herkes için bitir"], ["btn-ai-summary", "AI toplantı özeti oluştur"]]) {
        const button = document.getElementById(id);
        button.classList.remove("hidden");
        button.disabled = !moderator;
        button.title = moderator ? title : title + " — " + restricted;
    }
    document.getElementById("meeting-role").textContent = moderator ? "Siz: Moderatör" : "Siz: Katılımcı";
    renderRoomLock(isRoomLocked);
    renderRecordingState(!!currentRecordingId);
}

function renderRoomLock(locked) {
    isRoomLocked = !!locked;
    const button = document.getElementById("btn-lock-room");
    button.classList.toggle("room-locked", isRoomLocked);
    button.classList.toggle("room-open", !isRoomLocked);
    button.setAttribute("aria-pressed", String(isRoomLocked));
    button.title = isRoomLocked ? "Oda kilitli — kilidi kaldır" : "Oda açık — odayı kilitle";
    button.classList.remove("hidden");
    button.disabled = lockBusy || !window.isModerator;
    if (!window.isModerator) button.title += " — Yalnızca moderatör kullanabilir";
    button.setAttribute("aria-label", button.title);
    document.getElementById("lock-icon").textContent = isRoomLocked ? "lock" : "lock_open";
    document.getElementById("lock-label").textContent = isRoomLocked ? "Kilitli · Aç" : "Açık · Kilitle";
    const badge = document.getElementById("room-lock-status");
    badge.textContent = isRoomLocked ? "Oda kilitli" : "Oda açık";
    badge.classList.toggle("room-locked", isRoomLocked);
}

async function toggleRoomLock() {
    if (lockBusy || !window.isModerator) return;
    lockBusy = true;
    const button = document.getElementById("btn-lock-room");
    button.disabled = true;
    try {
        await connection.invoke("SetRoomLock", getCleanRoomName(), !isRoomLocked);
    } catch (error) { mediaNotice("Oda kilidi değiştirilemedi: " + error.message, true); }
    finally { lockBusy = false; renderRoomLock(isRoomLocked); }
}

function selectedShareProfile() {
    return shareProfiles[document.getElementById("share-quality").value] || shareProfiles["1080"];
}

function videoSender(pc) {
    return pc.getSenders().find(sender => sender.track?.kind === "video") ||
        pc.getTransceivers().find(transceiver => transceiver.receiver.track.kind === "video")?.sender;
}

async function applySenderQuality(pc) {
    const sender = videoSender(pc);
    if (!sender) return;
    const parameters = sender.getParameters();
    if (!parameters.encodings?.length) return;
    parameters.encodings.forEach(encoding => {
        if (isSharing) {
            encoding.maxBitrate = selectedShareProfile().bitrate;
            encoding.maxFramerate = 30;
        } else {
            delete encoding.maxBitrate;
            delete encoding.maxFramerate;
        }
    });
    await sender.setParameters(parameters);
}

function renderShareState() {
    const button = document.getElementById("btn-share");
    button.classList.toggle("is-on", isSharing);
    button.setAttribute("aria-pressed", String(isSharing));
    button.title = isSharing ? "Ekran paylaşımını durdur" : "Seçilen kalitede ekran paylaş";
    document.getElementById("share-icon").textContent = isSharing ? "cancel_presentation" : "present_to_all";
    const settings = screenStream?.getVideoTracks()[0]?.getSettings();
    document.getElementById("share-status").textContent = isSharing && settings
        ? "Yakalama: " + settings.width + " × " + settings.height
        : "Paylaşım kapalı";
}

async function applyShareQuality() {
    const track = screenStream?.getVideoTracks()[0];
    if (!track) return;
    const profile = selectedShareProfile();
    await track.applyConstraints({
        width: { ideal: profile.width, max: profile.width },
        height: { ideal: profile.height, max: profile.height },
        frameRate: { ideal: 30, max: 30 }
    });
    await Promise.all(Object.values(peerConnections).map(applySenderQuality));
    renderShareState();
}

async function stopShare() {
    const captured = screenStream;
    screenStream = null;
    isSharing = false;
    const camera = localStream?.getVideoTracks()[0] || null;
    const results = await Promise.allSettled(Object.values(peerConnections).map(async pc => {
        await videoSender(pc)?.replaceTrack(camera);
        await applySenderQuality(pc);
    }));
    captured?.getTracks().forEach(track => { track.onended = null; track.stop(); });
    document.getElementById("localVideo").srcObject = localStream;
    renderShareState();
    if (results.some(result => result.status === "rejected"))
        mediaNotice("Paylaşım durdu; bazı bağlantılarda kameraya dönüş başarısız oldu.", true);
}

async function toggleScreenShare() {
    if (shareBusy) return;
    shareBusy = true;
    const button = document.getElementById("btn-share");
    button.disabled = true;
    document.getElementById("share-quality").disabled = true;
    try {
        if (isSharing) { await stopShare(); return; }
        if (!navigator.mediaDevices?.getDisplayMedia)
            throw new Error("Bu tarayıcıda ekran paylaşımı desteklenmiyor. HTTPS veya localhost kullanın.");
        const profile = selectedShareProfile();
        screenStream = await navigator.mediaDevices.getDisplayMedia({
            video: { width: { ideal: profile.width }, height: { ideal: profile.height }, frameRate: { ideal: 30 } },
            audio: false
        });
        const track = screenStream.getVideoTracks()[0];
        track.contentHint = "detail";
        isSharing = true;
        await applyShareQuality();
        await Promise.all(Object.values(peerConnections).map(pc => videoSender(pc)?.replaceTrack(track)));
        if (track.readyState === "ended") throw new Error("Ekran paylaşımı kapatıldı.");
        document.getElementById("localVideo").srcObject = screenStream;
        track.onended = () => stopShare().catch(error => mediaNotice(error.message, true));
        renderShareState();
        mediaNotice("Ekran paylaşılıyor. Alıcı kalitesi bağlantı hızına göre değişebilir.");
    } catch (error) {
        await stopShare();
        mediaNotice(error.name === "NotAllowedError" ? "Ekran paylaşımı iptal edildi veya izin verilmedi." : error.message, error.name !== "NotAllowedError");
    } finally {
        shareBusy = false;
        button.disabled = false;
        document.getElementById("share-quality").disabled = false;
    }
}

// Stable canvas/audio tracks allow sharing and participant changes during recording.
// Recording uses a 720p meeting grid; screen broadcast quality is selected separately.
async function createRecordingStream() {
    const canvas = document.createElement("canvas");
    canvas.width = 1280; canvas.height = 720;
    const ctx = canvas.getContext("2d");
    if (!canvas.captureStream || !ctx) throw new Error("Tarayıcı toplantı kaydını desteklemiyor.");
    const audioContext = new (window.AudioContext || window.webkitAudioContext)();
    const output = audioContext.createMediaStreamDestination();
    const sources = new Map();
    const stream = canvas.captureStream(24);
    output.stream.getAudioTracks().forEach(track => stream.addTrack(track));
    let timer;
    const cleanup = () => {
        clearInterval(timer);
        sources.forEach(source => source.disconnect());
        stream.getTracks().forEach(track => track.stop());
        audioContext.close().catch(() => {});
    };
    try {
        await audioContext.resume();
        const draw = () => {
            const videos = [...document.querySelectorAll("#video-grid video")];
            const tracks = new Set([
                ...(localStream?.getAudioTracks() || []),
                ...videos.filter(video => video.id !== "localVideo").flatMap(video => video.srcObject?.getAudioTracks() || [])
            ].filter(track => track.readyState === "live"));
            for (const [track, source] of sources) {
                if (!tracks.has(track)) { source.disconnect(); sources.delete(track); }
            }
            for (const track of tracks) {
                if (!sources.has(track)) {
                    const source = audioContext.createMediaStreamSource(new MediaStream([track]));
                    source.connect(output); sources.set(track, source);
                }
            }
            ctx.fillStyle = "#09090b"; ctx.fillRect(0, 0, 1280, 720);
            const columns = Math.ceil(Math.sqrt(videos.length || 1));
            const rows = Math.ceil(videos.length / columns) || 1;
            videos.forEach((video, index) => {
                if (video.readyState < 2 || !video.videoWidth) return;
                const width = 1280 / columns, height = 720 / rows;
                const scale = Math.min(width / video.videoWidth, height / video.videoHeight);
                const w = video.videoWidth * scale, h = video.videoHeight * scale;
                ctx.drawImage(video, (index % columns) * width + (width - w) / 2,
                    Math.floor(index / columns) * height + (height - h) / 2, w, h);
            });
        };
        draw();
        timer = setInterval(draw, 1000 / 24);
        return { stream, cleanup };
    } catch (error) { cleanup(); throw error; }
}

function renderRecordingState(active) {
    document.getElementById("rec-badge").classList.toggle("hidden", !active);
    const button = document.getElementById("btn-rec");
    button.classList.toggle("is-on", active);
    button.title = active ? "Kaydı durdur ve kaydet" : "Toplantıyı kaydet (720p)";
    button.classList.remove("hidden");
    button.disabled = !window.isModerator || recordingBusy || (active && recordingOwner !== window.myConnectionId);
    if (!window.isModerator) button.title += " — Yalnızca moderatör kullanabilir";
    document.getElementById("rec-icon").textContent = active ? "stop_circle" : "fiber_manual_record";
}

async function uploadRecordingBlob(blob, recordingId) {
    const bytes = new Uint8Array(await blob.arrayBuffer());
    for (let offset = 0; offset < bytes.length; offset += 12 * 1024) {
        const slice = bytes.subarray(offset, offset + 12 * 1024);
        const encoded = btoa(String.fromCharCode(...slice));
        await connection.invoke("UploadRecordingChunk", getCleanRoomName(), recordingId, encoded);
    }
}

function queueRecordingBlob(blob, recordingId) {
    if (!blob.size || recordingError) return;
    queuedRecordingBytes += blob.size;
    if (queuedRecordingBytes > 16 * 1024 * 1024) {
        recordingError = new Error("Yükleme bağlantısı kayıt hızına yetişemiyor.");
        void finishRecording(true);
        return;
    }
    recordingQueue = recordingQueue.then(async () => {
        if (!recordingError) await uploadRecordingBlob(blob, recordingId);
    }).catch(error => {
        recordingError = error;
        // Do not await finish here: it waits for this queue.
        setTimeout(() => { void finishRecording(true); }, 0);
    }).finally(() => { queuedRecordingBytes -= blob.size; });
}

async function finishRecording(failed = false) {
    if (recordingBusy || !mediaRecorder) return false;
    recordingBusy = true;
    renderRecordingState(true);
    const id = currentRecordingId;
    const stoppedAt = Date.now();
    try {
        mediaNotice("Kayıt sunucuya kaydediliyor…");
        if (mediaRecorder.state !== "inactive") {
            await new Promise(resolve => {
                mediaRecorder.addEventListener("stop", resolve, { once: true });
                mediaRecorder.stop();
            });
        }
        // The final dataavailable fires BEFORE stop, so the final blob is in this queue.
        await recordingQueue;
        await connection.invoke("StopRecording", getCleanRoomName(), id,
            Math.round((stoppedAt - recordingStartTime) / 1000), failed || !!recordingError);
        if (recordingError) throw recordingError;
        return !failed;
    } catch (error) {
        mediaNotice("Kayıt tamamlanamadı: " + error.message, true);
        return false;
    } finally {
        recordingCleanup?.(); recordingCleanup = null;
        mediaRecorder = null; currentRecordingId = null; recordingOwner = null;
        recordingBusy = false; renderRecordingState(false);
    }
}

async function toggleRecording() {
    if (recordingBusy || !window.isModerator) return;
    if (mediaRecorder) { await finishRecording(); return; }
    if (currentRecordingId) { mediaNotice("Kayıt başka bir bağlantı tarafından yürütülüyor.", true); return; }
    recordingBusy = true;
    renderRecordingState(false);
    let id;
    try {
        if (typeof MediaRecorder === "undefined") throw new Error("Bu tarayıcı kayıt desteklemiyor.");
        const mimeType = ["video/webm;codecs=vp8,opus", "video/webm"].find(type => MediaRecorder.isTypeSupported(type));
        if (!mimeType) throw new Error("WebM kaydı için güncel Chrome, Edge veya Firefox kullanın.");
        const capture = await createRecordingStream();
        recordingCleanup = capture.cleanup;
        mediaRecorder = new MediaRecorder(capture.stream, { mimeType, videoBitsPerSecond: 2000000, audioBitsPerSecond: 128000 });
        recordingQueue = Promise.resolve(); recordingError = null; queuedRecordingBytes = 0;
        id = await connection.invoke("StartRecording", getCleanRoomName());
        currentRecordingId = id;
        recordingOwner = window.myConnectionId;
        recordingStartTime = Date.now();
        mediaRecorder.ondataavailable = event => queueRecordingBlob(event.data, id);
        mediaRecorder.onerror = event => {
            recordingError = event.error || new Error("Tarayıcı kaydı durdurdu.");
            void finishRecording(true);
        };
        mediaRecorder.start(1000);
        document.getElementById("recording-download").classList.add("hidden");
        mediaNotice("Toplantı kaydediliyor (720p): katılımcı görüntüleri ve toplantı sesleri.");
    } catch (error) {
        if (id) await connection.invoke("StopRecording", getCleanRoomName(), id, 0, true).catch(() => {});
        recordingCleanup?.(); recordingCleanup = null; mediaRecorder = null;
        currentRecordingId = null; recordingOwner = null;
        mediaNotice("Kayıt başlatılamadı: " + error.message, true);
    } finally { recordingBusy = false; renderRecordingState(!!currentRecordingId); }
}

async function finishRecordingBeforeLeaving() {
    if (recordingBusy) { mediaNotice("Kaydın tamamlanmasını bekleyin."); return false; }
    return mediaRecorder ? await finishRecording() : true;
}

function initializeMeetingMedia() {
    updateModeratorControls();
    document.getElementById("btn-share").onclick = toggleScreenShare;
    let previousQuality = document.getElementById("share-quality").value;
    document.getElementById("share-quality").onchange = async function () {
        this.disabled = true;
        try { await applyShareQuality(); previousQuality = this.value; }
        catch (error) {
            this.value = previousQuality;
            await applyShareQuality().catch(() => {});
            mediaNotice("Kalite değiştirilemedi: " + error.message, true);
        } finally { this.disabled = false; }
    };
    if (typeof initializeQualityPicker === "function") initializeQualityPicker();
    connection.on("RoomLockStatusChanged", locked => {
        renderRoomLock(locked);
        mediaNotice(locked ? "Oda kilitli. Yeni katılımcı kabul edilmiyor." : "Oda açık. Yeni katılımcılar katılabilir.");
    });
    connection.on("RecordingStarted", (id, owner) => {
        currentRecordingId = id; recordingOwner = owner; renderRecordingState(true);
    });
    connection.on("RecordingStopped", (id, summary) => {
        if (currentRecordingId && id !== currentRecordingId) return;
        if (!mediaRecorder) { currentRecordingId = null; recordingOwner = null; }
        renderRecordingState(false);
        if (summary?.status === 1 && summary.fileSize > 0) {
            const link = document.getElementById("recording-download");
            link.href = "/api/recordings/" + encodeURIComponent(id) + "/download";
            link.classList.remove("hidden");
            mediaNotice("Kayıt hazır · " + summary.durationSeconds + " saniye. İndirebilirsiniz.");
        } else mediaNotice("Kayıt tamamlanamadı. Bağlantıyı ve sunucunun kayıt klasörünü kontrol edin.", true);
    });
    connection.onreconnecting(() => {
        if (mediaRecorder) {
            recordingError = new Error("Sunucu bağlantısı kesildi. Kayıt kesintiye uğradı.");
            void finishRecording(true);
        }
    });
    window.addEventListener("beforeunload", event => {
        if (mediaRecorder || recordingBusy) { event.preventDefault(); event.returnValue = ""; }
    });
}
