using System.Net;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:6969");


var app = builder.Build();
app.UseWebSockets();
var connections = new List<WebSocket>();

app.Map("/ws", async context => {
    if(context.WebSockets.IsWebSocketRequest) {

        var currentName = context.Request.Query["name"];

        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        connections.Add(ws);

        await Broadcast($"{currentName} joined the room");
        await Broadcast($"{connections.Count} users connected");
        await ReceiveMessage(ws,
            async (result, buffer) => {
                if(result.MessageType == WebSocketMessageType.Text) {

                    string message = Encoding.UTF8.GetString(buffer, 0 , result.Count);
                    await Broadcast(currentName + ": " + message);
                }
                else if(result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted) {
                    connections.Remove(ws);
                    await Broadcast($"{currentName} left the room");
                    await Broadcast($"{connections.Count} users connected");
                    await ws.CloseAsync(result.CloseStatus.Value,
                    result.CloseStatusDescription, CancellationToken.None);
                }
        });
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

async Task ReceiveMessage(WebSocket socket, 
Action<WebSocketReceiveResult, byte[]> handleMessage) {

    var builder = new byte[1024 * 4];

    while(socket.State == WebSocketState.Open) {

        var result = await socket.ReceiveAsync(new ArraySegment<byte>(builder), 
        CancellationToken.None);
        handleMessage(result, builder);
    }
}

async Task Broadcast(string message) {

    var bytes = Encoding.UTF8.GetBytes(message);

    foreach(var socket in connections) {

        if(socket.State == WebSocketState.Open) {

            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
            
            await socket.SendAsync(arraySegment, 
                                    WebSocketMessageType.Text, 
                                    true, 
                                    CancellationToken.None);
        }
    }
}

await app.RunAsync();
