import ws from 'k6/ws';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom K6 Performance Metrics
const errorRate = new Rate('meetsync_error_rate');
const connectionLatency = new Trend('meetsync_connection_latency_ms');
const messageRtt = new Trend('meetsync_message_rtt_ms');
const messagesSent = new Counter('meetsync_messages_sent');

export const options = {
    stages: [
        { duration: '30s', target: 10 },   // Warm-up to 10 VUs
        { duration: '1m', target: 100 },   // Ramp-up to 100 VUs
        { duration: '2m', target: 500 },   // Heavy load spike to 500 VUs
        { duration: '1m', target: 500 },   // Sustain 500 VUs
        { duration: '30s', target: 0 },    // Cool-down to 0 VUs
    ],
    thresholds: {
        'meetsync_error_rate': ['rate<0.01'],             // Error rate under 1%
        'meetsync_connection_latency_ms': ['p(95)<200'],   // 95% connections under 200ms
        'meetsync_message_rtt_ms': ['p(95)<100'],         // 95% message RTT under 100ms
    },
};

const BASE_URL = __ENV.TARGET_URL || 'ws://localhost:5252/meetingHub';

export default function () {
    const roomName = `LoadTestRoom_${Math.floor(__VU / 10)}`; // 10 users per room
    const userName = `LoadUser_${__VU}_${__ITER}`;
    const startTime = Date.now();

    const url = `${BASE_URL}?format=json`;

    const res = ws.connect(url, {}, function (socket) {
        connectionLatency.add(Date.now() - startTime);

        socket.on('open', () => {
            // SignalR Handshake protocol initialization
            socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\x1e');

            // 1. Join Meeting Room
            const joinPayload = {
                type: 1,
                target: 'JoinRoom',
                arguments: [roomName, userName, '']
            };
            socket.send(JSON.stringify(joinPayload) + '\x1e');
            messagesSent.add(1);

            // 2. Simulate WebRTC Signaling & Speech Streaming loop
            socket.setInterval(() => {
                const sendStart = Date.now();

                // Send WebRTC Offer / Candidate
                const offerPayload = {
                    type: 1,
                    target: 'SendOffer',
                    arguments: [JSON.stringify({ sdp: 'dummy-load-sdp-offer' }), `Target_${Math.random()}`]
                };
                socket.send(JSON.stringify(offerPayload) + '\x1e');
                messagesSent.add(1);

                // Stream Live Transcription Chunk
                const transcriptPayload = {
                    type: 1,
                    target: 'SendTranscriptionChunk',
                    arguments: [roomName, `Automated load test transcript from ${userName}`]
                };
                socket.send(JSON.stringify(transcriptPayload) + '\x1e');
                messagesSent.add(1);

                messageRtt.add(Date.now() - sendStart);
            }, 2000);

            // 3. Keep connection alive for 30 seconds
            socket.setTimeout(() => {
                // Graceful End Meeting / Leave
                const endPayload = {
                    type: 1,
                    target: 'SendMessage',
                    arguments: [roomName, userName, 'Disconnecting from load test session']
                };
                socket.send(JSON.stringify(endPayload) + '\x1e');
                socket.close();
            }, 30000);
        });

        socket.on('message', (data) => {
            const isOk = check(data, {
                'valid SignalR packet': (d) => d && d.length > 0,
            });
            errorRate.add(!isOk);
        });

        socket.on('error', (e) => {
            errorRate.add(1);
            console.error(`WebSocket Error [VU: ${__VU}]:`, e);
        });
    });

    check(res, { 'status is 101 Switching Protocols': (r) => r && r.status === 101 });
    sleep(1);
}
