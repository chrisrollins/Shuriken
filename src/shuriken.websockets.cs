﻿namespace Shuriken
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

        public static void FastEvent(byte id, Action<Server.WebSockets.WSData> callback)
        {
            Server.WebSockets.FastEvent(id, callback);
        }

        public static void NamedEvent(string eventName, Action<Server.WebSockets.WSData> callback)
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

            public struct WSData
            {
                public readonly byte[] data;
                public WSData(byte[] initData)
                {
                    this.data = initData;
                }
            }
            
            private struct WSEvent
            {
                public Action<WSData> callback;
                public WSEvent(Action<WSData> callback)
                {
                    this.callback = callback;
                }
            }
            private static Dictionary<string, WSEvent> StringEvents = new Dictionary<string, WSEvent>();
            private static WSEvent[] FastEvents = new WSEvent[256];

            public static void FastEvent(byte id, Action<WSData> callback)
            {
                FastEvents[id] = new WSEvent(callback);
            }

            public static void NamedEvent(string eventName, Action<WSData> callback)
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
                ArraySegment<byte> sendWithEventHeader = new ArraySegment<byte>(data);
                await connection.Socket.SendAsync(sendWithEventHeader, WebSocketMessageType.Binary, SocketResult.EndOfMessage, CancellationToken.None);
            }

            private static async Task SendEventToConnection(byte eventID, byte[] data, Connection connection)
            {
                byte[] buffer = new byte[data.Length + 1];
                buffer[0] = 0;
                for (int i = 1; i < buffer.Length; i++)
                {
                    buffer[i] = data[i - 1];
                }
                ArraySegment<byte> sendWithEventHeader = new ArraySegment<byte>(data);
                await connection.Socket.SendAsync(sendWithEventHeader, WebSocketMessageType.Binary, SocketResult.EndOfMessage, CancellationToken.None);
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

            public class Connection
            {
                private static long idCount = 0;
                private WebSocketContext ctx;
                public WebSocket Socket;
                public readonly long id;
                public readonly string IPAddress;
                public readonly short port;
                private readonly Object Lock = new Object();

                public Connection()
                {
                    lock (Lock)
                    {
                        this.id = idCount++;
                    }
                }
            }

            private static List<Room> _Rooms = new List<Room>(1000);

            public static class Rooms
            {
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
                    return _Rooms[id];
                }
            }

            public class Room
            {
                private List<Connection> _Connections;
                private readonly Object Lock = new Object();
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
        

            private async static Task HandleWS(HttpListenerResponse response, HttpListenerContext context, string IPAddress)
            {
                if (WebSocketsEnabled)
                {
                    await ProcessConnection();
                    WebSockets.ClearThreadStatics();
                    return;
                }
                else
                {
                    response.StatusCode = 500;
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
                        response.StatusCode = 500;
                        response.Close();
                    }
                    WebSocket Socket = ctx.WebSocket;
                    try
                    {
                        int WSBufferSize = WSHeaderSize + WSDataSize;
                        byte[] SocketBuffer = new byte[WSBufferSize];
                        WSData data;
                        CurrentConnection = new Connection();
                        while (Socket.State == WebSocketState.Open)
                        {
                            Server.WebSockets.SocketResult = await Socket.ReceiveAsync(new ArraySegment<byte>(SocketBuffer), CancellationToken.None);
                            if (SocketResult.MessageType == WebSocketMessageType.Close)
                            {
                                await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            }
                            else
                            {
                                //0 means it's a numbered event so the next byte is the event number. There can be 256 numbered events.
                                if (SocketBuffer[0] == 0)
                                {
                                    data = new WSData(new ArraySegment<byte>(SocketBuffer, 1, SocketBuffer.Length).Array);
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

                                    if(parseErrors.Length == 0)
                                    {
                                        data = new WSData(new ArraySegment<byte>(SocketBuffer, actualHeaderLength, SocketBuffer.Length).Array);
                                        StringEvents[System.Text.Encoding.UTF8.GetString(SocketBuffer, 0, actualHeaderLength).Trim('\0')].callback(data);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        PrintException(e);
                        response.StatusCode = 500;
                        response.Close();
                    }
                    finally
                    {
                        if (Socket != null)
                            Socket.Dispose();
                    }
                }
            }
        }
        /////
    }
}