const connection = new signalR.HubConnectionBuilder()
    .withUrl("/meetingHub")
    .build();

connection.start()
    .then(() => console.log("SignalR connected"))
    .catch(err => console.error(err));

function joinRoom() {
    const room = document.getElementById("roomName").value;
    const user = document.getElementById("userName").value;

    connection.invoke("JoinRoom", room, user);
}

function sendMessage() {
    const room = document.getElementById("roomName").value;
    const user = document.getElementById("userName").value;
    const message = document.getElementById("messageInput").value;

    connection.invoke("SendMessage", room, user, message);
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
