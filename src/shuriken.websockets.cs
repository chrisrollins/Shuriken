namespace Shuriken
{
    using System;
    using System.Net;
    using System.IO;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Reflection;
    using System.Text;
    using System.Collections.Specialized;
    using System.Net.WebSockets;
    
    public static class WebSockets
    {
        public static void Enable(string subprotocol = null)
        {
            Server.WebSockets.Enable(subprotocol);
        }

        public static void Disable()
        {
            Server.WebSockets.Disable();
        }

        public static void FastEvent(byte id, Action<byte[]> callback)
        {
            Server.WebSockets.FastEvent(id, callback);
        }

        public static void NamedEvent(string eventName, Action<byte[]> callback)
        {
            Server.WebSockets.NamedEvent(eventName, callback);
        }

        public static async Task SendEvent(string eventName, byte[] data)
        {
            await Server.WebSockets.SendEvent(eventName, data);
        }

        public static async Task SendEvent(byte eventID, byte[] data)
        {
            await Server.WebSockets.SendEvent(eventID, data);
        }
    }

    public static partial class Server
    {
        public static class WebSockets
        {
            [ThreadStatic] private static Server.WebSockets.Connection CurrentConnection;
            [ThreadStatic] private static WebSocketReceiveResult SocketResult;
            private const int WSDataSize = 2048;
            private const int WSHeaderSize = 256;
            
            private struct WSEvent
            {
                public Action<byte[]> callback;
                public WSEvent(Action<byte[]> callback)
                {
                    this.callback = callback;
                }
            }

            private static Dictionary<string, WSEvent> StringEvents = new Dictionary<string, WSEvent>();
            private static WSEvent[] FastEvents = new WSEvent[256];

            public static void FastEvent(byte id, Action<byte[]> callback)
            {
                FastEvents[id] = new WSEvent(callback);
            }

            public static void NamedEvent(string eventName, Action<byte[]> callback)
            {
                StringEvents[eventName] = new WSEvent(callback);
            }

            public static async Task SendEvent(string eventName, byte[] data)
            {
                await SendEventToConnection(eventName, data, CurrentConnection);
            }

            public static async Task SendEvent(byte eventID, byte[] data)
            {
                await SendEventToConnection(eventID, data, CurrentConnection);
            }

            private static async Task SendEventToConnection(string eventName, byte[] data, Connection connection)
            {
                byte[] buffer = new byte[data.Length + eventName.Length + 1];
                int index = 0;
                foreach (byte b in eventName)
                {
                    buffer[index] = b;
                    index++;
                }
                buffer[index++] = 0;
                foreach (byte b in data)
                {
                    buffer[index] = b;
                    index++;
                }
                await connection.Socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, SocketResult.EndOfMessage, CancellationToken.None);
            }

            private static async Task SendEventToConnection(byte eventID, byte[] data, Connection connection)
            {
                byte[] buffer = new byte[data.Length + 2];
                buffer[0] = 0;
                buffer[1] = eventID;
                for (int i = 2; i < buffer.Length; i++)
                {
                    buffer[i] = data[i - 2];
                }
                await connection.Socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, SocketResult.EndOfMessage, CancellationToken.None);
            }

            public static void BroadcastEventSync(string eventName, byte[] data, params Room[] Rooms)
            {
                async Task BCast()
                {
                    foreach (Room room in Rooms)
                    {
                        foreach (Connection connection in room.Connections)
                        {
                            await SendEventToConnection(eventName, data, connection);
                        }
                    }
                }
                BCast().Wait();
            }

            public static void BroadcastEventSync(byte eventID, byte[] data, params Room[] Rooms)
            {
                async Task BCast()
                {
                    foreach (Room room in Rooms)
                    {
                        foreach (Connection connection in room.Connections)
                        {
                            await SendEventToConnection(eventID, data, connection);
                        }
                    }
                }
                BCast().Wait();
            }

            #pragma warning disable 4014
            public static void BroadcastEventAsync(string eventName, byte[] data, params Room[] Rooms)
            {
                foreach (Room room in Rooms)
                {
                    foreach (Connection connection in room.Connections)
                    {
                        SendEventToConnection(eventName, data, connection);
                    }
                }
            }

            public static void BroadcastEventAsync(byte eventID, byte[] data, params Room[] Rooms)
            {
                foreach (Room room in Rooms)
                {
                    foreach (Connection connection in room.Connections)
                    {
                        SendEventToConnection(eventID, data, connection);
                    }
                }
            }
            #pragma warning restore 4014

            public class Connection
            {
                private static long idCount = 0;
                //private WebSocketContext ctx;
                public WebSocket Socket;
                public readonly long id;
                public readonly string IPAddress;
                public readonly short port;
                private static readonly Object Lock = new Object();

                public Connection(WebSocket socket)
                {
                    this.Socket = socket;
                    lock (Lock)
                    {
                        this.id = idCount++;
                    }
                }
            }

            private static List<Room> _Rooms = new List<Room>(1000);

            public static class Rooms
            {
                private static readonly Object Lock = new Object();
                public static Room[] All
                {
                    get { return _Rooms.ToArray(); }
                }

                public static int Count
                {
                    get { return _Rooms.Count; }
                }

                public static Room GetRoomByID(int id)
                {
                    if(_Rooms.Count < id)
                    {
                        return null;
                    }
                    return _Rooms[id];
                }
            }

            public class Room
            {
                private List<Connection> _Connections;
                private static readonly Object Lock = new Object();
                public readonly int id;
                public int Count
                {
                    get { return _Connections.Count; }
                }
                public Connection[] Connections
                {
                    get { return _Connections.ToArray(); }
                }

                public Room(int initialSize = 32)
                {
                    this._Connections = new List<Connection>(initialSize);
                    lock (Lock)
                    {
                        _Rooms.Add(this);
                        this.id = _Rooms.Count - 1;
                    }
                }

                public void Join()
                {
                    lock (Lock)
                    {
                        this._Connections.Remove(CurrentConnection);
                        this._Connections.Add(CurrentConnection);
                    }
                }

                public void Leave()
                {
                    lock (Lock)
                    {
                        this._Connections.Remove(CurrentConnection);
                    }
                }
            }

            public static void Enable(string subprotocol = null)
            {
                WebSocketSubProtocol = subprotocol;
                WebSocketsEnabled = true;
                Server.HWS = HandleWS;
            }

            public static void Disable()
            {
                WebSocketsEnabled = false;
            }

            public static void ClearThreadStatics()
            {
                CurrentConnection = null;
            }
        

            private async static Task HandleWS(HttpListenerContext context)
            {
                HttpListenerResponse response = context.Response;
                HttpListenerRequest request = context.Request;
                if (WebSocketsEnabled)
                {
                    SetHTTPStatus(101);
                    await ProcessConnection();
                    WebSockets.ClearThreadStatics();
                    return;
                }
                else
                {
                    SetHTTPStatus(500);
                    response.Close();
                    return;
                }

                async Task ProcessConnection()
                {
                    WebSocketContext ctx = null;
                    try
                    {
                        ctx = await context.AcceptWebSocketAsync(WebSocketSubProtocol);
                    }
                    catch (Exception e)
                    {
                        PrintException(e);
                        SetHTTPStatus(500);
                        response.Close();
                    }
                    WebSocket Socket = ctx.WebSocket;
                    int originalThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
                    try
                    {
                        int WSBufferSize = WSHeaderSize + WSDataSize;
                        byte[] SocketBuffer = new byte[WSBufferSize];
                        CurrentConnection = new Connection(Socket);

                        while (Socket.State == WebSocketState.Open)
                        {
                            for (int i = 0; i < SocketBuffer.Length; i++)
                            {
                                SocketBuffer[i] = 0;
                            }

                            WebSocketReceiveResult res = null;
                            Task.Run(async () =>
                            {
                                res = await Socket.ReceiveAsync(new ArraySegment<byte>(SocketBuffer), CancellationToken.None);
                            }).Wait();

                            Server.WebSockets.SocketResult = res;


                            if (SocketResult.MessageType == WebSocketMessageType.Close)
                            {
                                await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            }
                            else
                            {
                                //0 means it's a numbered event so the next byte is the event number. There can be 256 numbered events.
                                if (SocketBuffer[0] == 0)
                                {
                                    int actualDataLength = 2;
                                    while (SocketBuffer[actualDataLength] != 0)
                                    {
                                        actualDataLength++;
                                    }
                                    actualDataLength -= 2;
                                    byte[] data = new byte[actualDataLength];
                                    for (int i = 0; i < actualDataLength; i++)
                                    {
                                        data[i] = SocketBuffer[i+2];
                                    }
                                    FastEvents[SocketBuffer[1]].callback(data);
                                }
                                else // otherwise read in the characters until we hit a null terminator and try to call the event with that name.
                                {
                                    string parseErrors = "";
                                    int actualHeaderLength = 0;
                                    while ( SocketBuffer[++actualHeaderLength] != 0 )
                                    {
                                        if (actualHeaderLength == (WSHeaderSize - 1))
                                        {
                                            parseErrors += "Invalid Event name - This event name was not null terminated. Event names need to be null terminated.\n";
                                            break;
                                        }
                                    }

                                    int actualDataLength = actualHeaderLength;
                                    while (SocketBuffer[actualDataLength + 1] != 0)
                                    { actualDataLength++; }

                                    if (parseErrors.Length == 0)
                                    {
                                        byte[] data = new byte[actualDataLength];
                                        for (int i = 0; i < actualDataLength; i++)
                                        {
                                            data[i] = SocketBuffer[i + actualHeaderLength];
                                        }
                                        StringEvents[System.Text.Encoding.UTF8.GetString(SocketBuffer, 0, actualHeaderLength).Trim('\0')].callback(data);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        PrintException(e);
                        SetHTTPStatus(500);
                        response.Close();
                    }
                    finally
                    {
                        if (Socket != null)
                        {
                            Socket.Dispose();
                        }
                    }
                }
            }
        }
        /////
    }
}