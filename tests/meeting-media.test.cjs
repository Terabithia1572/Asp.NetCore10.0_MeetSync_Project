const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");
const source = fs.readFileSync(path.join(__dirname, "../Asp.NetCore10.0_MeetSync_Project/wwwroot/js/meeting-media.js"), "utf8");

function fixture() {
    const elements = new Map();
    function element(id) {
        if (!elements.has(id)) {
            const classes = new Set();
            elements.set(id, {
                value: "1080", textContent: "", disabled: false, srcObject: null,
                attributes: {}, setAttribute(k, v) { this.attributes[k] = v; },
                classList: {
                    add: c => classes.add(c), remove: c => classes.delete(c),
                    contains: c => classes.has(c),
                    toggle(c, state) { if (state) classes.add(c); else classes.delete(c); }
                }
            });
        }
        return elements.get(id);
    }
    const calls = [], events = {};
    const context = vm.createContext({
        console, Blob, Uint8Array, Promise, Map, Set, Date, Error, Math, Object, String,
        setTimeout, clearTimeout, setInterval, clearInterval,
        btoa: text => Buffer.from(text, "binary").toString("base64"),
        window: { myConnectionId: "owner", isModerator: true, addEventListener() {} },
        document: { getElementById: element },
        navigator: { mediaDevices: {} },
        isRoomLocked: false, localStream: null, peerConnections: {},
        getCleanRoomName: () => "Yazılım_Toplantısı",
        connection: {
            async invoke(...args) { calls.push(args); },
            on(name, callback) { events[name] = callback; },
            onreconnecting(callback) { events.reconnecting = callback; }
        }
    });
    vm.runInContext(source, context);
    return { context, calls, events, element, run: code => vm.runInContext(code, context) };
}

test("video blobs are split below the 32 KiB limit, in order, with identical bytes", async () => {
    const f = fixture(), input = Buffer.alloc(52000);
    for (let i = 0; i < input.length; i++) input[i] = i % 251;
    f.context.blob = new Blob([input]);
    await f.run('uploadRecordingBlob(blob, "recording")');
    assert.equal(f.calls.length, 5);
    assert.ok(f.calls.every(call => Buffer.byteLength(JSON.stringify(call)) < 32768));
    assert.deepEqual(Buffer.concat(f.calls.map(call => Buffer.from(call[3], "base64"))), input);
});

test("stop waits for the final dataavailable blob before finalizing the server file", async () => {
    const f = fixture();
    f.context.finalBlob = new Blob(["last webm bytes"]);
    f.run(`currentRecordingId = "recording"; recordingOwner = "owner"; recordingStartTime = Date.now();
        mediaRecorder = {
            state: "recording",
            addEventListener(name, callback) { this.stopped = callback; },
            stop() {
                this.state = "inactive";
                queueRecordingBlob(finalBlob, currentRecordingId);
                this.stopped();
            }
        };`);
    assert.equal(await f.run("finishRecording()"), true);
    assert.deepEqual(f.calls.map(call => call[0]), ["UploadRecordingChunk", "StopRecording"]);
    assert.equal(f.calls[1][4], false);
});

test("upload failure marks recording failed and never reports success", async () => {
    const f = fixture();
    f.context.connection.invoke = async (...args) => {
        f.calls.push(args);
        if (args[0] === "UploadRecordingChunk") throw new Error("disk full");
    };
    f.context.finalBlob = new Blob(["test"]);
    f.run(`currentRecordingId = "recording"; recordingStartTime = Date.now();
        mediaRecorder = { state: "inactive" };
        queueRecordingBlob(finalBlob, currentRecordingId);`);
    assert.equal(await f.run("finishRecording()"), false);
    assert.equal(f.calls.find(call => call[0] === "StopRecording")[4], true);
    assert.match(f.element("media-status").textContent, /tamamlanamadı/);
});

test("lock event updates text, icon and accessible pressed state; failed change preserves it", async () => {
    const f = fixture();
    f.run("initializeMeetingMedia()");
    f.events.RoomLockStatusChanged(true);
    assert.equal(f.element("lock-icon").textContent, "lock");
    assert.equal(f.element("lock-label").textContent, "Kilitli · Aç");
    assert.equal(f.element("btn-lock-room").attributes["aria-pressed"], "true");
    f.context.connection.invoke = async () => { throw new Error("offline"); };
    await f.run("toggleRoomLock()");
    assert.equal(f.element("lock-label").textContent, "Kilitli · Aç");
    assert.equal(f.element("btn-lock-room").disabled, false);
    f.events.RoomLockStatusChanged(false);
    assert.equal(f.element("lock-icon").textContent, "lock_open");
    assert.equal(f.element("lock-label").textContent, "Açık · Kilitle");
});

test("all five screen profiles apply capture constraints and sender bitrate", async () => {
    for (const [quality, expected] of Object.entries({480: [854,480,1000000],720: [1280,720,2500000],1080: [1920,1080,5000000],1440: [2560,1440,8000000],2160: [3840,2160,16000000]})) {
        const f = fixture();
        let constraints, parameters;
        f.element("share-quality").value = quality;
        f.context.track = {
            kind: "video", getSettings: () => ({ width: expected[0], height: expected[1] }),
            async applyConstraints(value) { constraints = value; }
        };
        const sender = {
            track: f.context.track,
            getParameters: () => ({ encodings: [{}] }),
            async setParameters(value) { parameters = value; }
        };
        f.context.peerConnections.peer = { getSenders: () => [sender] };
        f.run("screenStream = { getVideoTracks: () => [track] }; isSharing = true;");
        await f.run("applyShareQuality()");
        assert.equal(constraints.width.max, expected[0]);
        assert.equal(constraints.height.max, expected[1]);
        assert.equal(parameters.encodings[0].maxBitrate, expected[2]);
        assert.match(f.element("share-status").textContent, new RegExp(String(expected[0])));
    }
});

test("screen cancellation restores controls and leaves sharing off", async () => {
    const f = fixture();
    f.context.navigator.mediaDevices.getDisplayMedia = async () => { const e = new Error(); e.name = "NotAllowedError"; throw e; };
    await f.run("toggleScreenShare()");
    assert.equal(f.run("isSharing"), false);
    assert.equal(f.element("btn-share").disabled, false);
    assert.equal(f.element("share-quality").disabled, false);
    assert.equal(f.element("btn-share").classList.contains("is-on"), false);
});

test("stopping screen sharing releases capture tracks even without a camera", async () => {
    const f = fixture();
    let stopped = false, replaced = "not called";
    const track = { kind: "video", stop() { stopped = true; }, onended() {} };
    f.context.capture = { getTracks: () => [track] };
    f.context.peerConnections.peer = {
        getSenders: () => [{
            track, async replaceTrack(value) { replaced = value; },
            getParameters: () => ({ encodings: [] })
        }]
    };
    f.run("screenStream = capture; isSharing = true;");
    await f.run("stopShare()");
    assert.equal(stopped, true);
    assert.equal(replaced, null);
    assert.equal(f.run("isSharing"), false);
});

test("failed or empty recordings do not expose a success download", () => {
    const f = fixture();
    f.element("recording-download").classList.add("hidden");
    f.run("initializeMeetingMedia()");
    f.events.RecordingStopped("recording", { status: 2, fileSize: 0 });
    assert.equal(f.element("recording-download").classList.contains("hidden"), true);
    assert.match(f.element("media-status").textContent, /tamamlanamadı/);
    f.events.RecordingStopped("recording", { status: 1, fileSize: 1024, durationSeconds: 10 });
    assert.equal(f.element("recording-download").classList.contains("hidden"), false);
    assert.equal(f.element("recording-download").href, "/api/recordings/recording/download");
});

test("media module and full WebRTC script load together with no duplicate globals or missing handlers", () => {
    const f = fixture();
    const connected = f.context.connection;
    f.context.signalR = {
        HubConnectionBuilder: class {
            withUrl() { return this; }
            withAutomaticReconnect() { return this; }
            build() { return connected; }
        }
    };
    f.context.roomName = "Yazılım_Toplantısı";
    f.context.userName = "Yunus";
    const webrtc = fs.readFileSync(path.join(__dirname, "../Asp.NetCore10.0_MeetSync_Project/wwwroot/js/webrtc.js"), "utf8")
        .replace(/\binit\(\);\s*$/, "");
    vm.runInContext(webrtc, f.context);
    assert.equal(typeof f.element("btn-share").onclick, "function");
    assert.equal(typeof f.events.RecordingStopped, "function");
    assert.equal(typeof f.events.RoomLockStatusChanged, "function");
});

test("moderator tools stay visible for both roles and only enable for the moderator", () => {
    const f = fixture();
    const ids = ["btn-rec", "btn-lock-room", "btn-ai-summary", "btn-end-meeting"];
    for (const moderator of [false, true, false]) {
        f.context.window.isModerator = moderator;
        f.run("updateModeratorControls()");
        for (const id of ids) {
            assert.equal(f.element(id).classList.contains("hidden"), false, id);
            assert.equal(f.element(id).disabled, !moderator, id);
            if (!moderator) assert.match(f.element(id).title, /Yalnızca moderatör/);
        }
        assert.equal(f.element("meeting-role").textContent, moderator ? "Siz: Moderatör" : "Siz: Katılımcı");
    }
});

test("recording and lock notifications do not enable participant-only controls", () => {
    const f = fixture();
    f.context.window.isModerator = false;
    f.run("initializeMeetingMedia()");
    f.events.RoomLockStatusChanged(true);
    f.events.RecordingStarted("recording", "other-user");
    f.events.RecordingStopped("recording", { status: 1, fileSize: 100, durationSeconds: 1 });
    assert.equal(f.element("btn-lock-room").disabled, true);
    assert.equal(f.element("btn-rec").disabled, true);
});
