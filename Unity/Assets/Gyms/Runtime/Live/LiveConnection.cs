using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LucidLoop.Gyms
{
    // All socket work is off the Unity thread. The controller drains Events in Update.
    public sealed class LiveConnection : IDisposable
    {
        readonly ClientWebSocket socket=new ClientWebSocket();
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        readonly SemaphoreSlim writer=new SemaphoreSlim(1,1);
        readonly ConcurrentQueue<JObject> events=new ConcurrentQueue<JObject>();
        int queuedAudio;
        volatile bool disposed;
        public bool TryRead(out JObject value)=>events.TryDequeue(out value);
        public Task Connect(string address,string character,string token)=>Connect(address,new JObject{{"type","gym.start"},{"character",character},{"token",token}});
        public async Task Connect(string address,JObject start)
        {
            try
            {
                var uri=new Uri(address);
                if(uri.Scheme!="wss" && !(uri.Scheme=="ws"&&uri.IsLoopback))throw new ArgumentException("Use wss:// for remote servers, or ws:// for localhost.");
                using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {timeout.CancelAfter(TimeSpan.FromSeconds(12));await socket.ConnectAsync(uri,timeout.Token).ConfigureAwait(false);}
                await Send(start).ConfigureAwait(false);
                await Receive().ConfigureAwait(false);
            }
            catch(OperationCanceledException){if(!disposed)Error("Connection timed out or was canceled.");}
            catch(Exception){if(!disposed)Error("Connection failed. Check the relay address, access token and server logs.");}
        }
        void Error(string message)=>Enqueue(new JObject{{"type","gym.status"},{"status","error"},{"message",message}});
        void Enqueue(JObject value)
        {
            lock(events)
            {
                if(events.Count>=256)
                {
                    while(events.TryDequeue(out _)){}
                    events.Enqueue(new JObject{{"type","gym.status"},{"status","error"},{"message","Inbound event limit exceeded; session stopped."}});
                    socket.Abort();
                    return;
                }
                events.Enqueue(value);
            }
        }
        async Task Receive()
        {
            var chunk=new byte[16384];
            while(!lifetime.IsCancellationRequested && socket.State==WebSocketState.Open)
            {
                using var message=new MemoryStream();WebSocketReceiveResult part;
                do
                {
                    part=await socket.ReceiveAsync(new ArraySegment<byte>(chunk),lifetime.Token).ConfigureAwait(false);
                    if(part.MessageType==WebSocketMessageType.Close)
                    {Enqueue(new JObject{{"type","gym.transport.closed"}});return;}
                    if(part.MessageType!=WebSocketMessageType.Text||message.Length+part.Count>262144)throw new IOException("Invalid relay frame.");
                    message.Write(chunk,0,part.Count);
                }while(!part.EndOfMessage);
                Enqueue(JObject.Parse(Encoding.UTF8.GetString(message.ToArray())));
            }
        }
        public async Task Send(JObject message)
        {
            if(disposed)return;
            await writer.WaitAsync(lifetime.Token).ConfigureAwait(false);
            try
            {
                if(socket.State!=WebSocketState.Open)return;
                var bytes=Encoding.UTF8.GetBytes(message.ToString(Newtonsoft.Json.Formatting.None));
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(5000);
                await socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,timeout.Token).ConfigureAwait(false);
            }
            finally{writer.Release();}
        }
        public async Task SendAudio(byte[] bytes)
        {
            if(Interlocked.Increment(ref queuedAudio)>8){Interlocked.Decrement(ref queuedAudio);return;}
            try{await Send(new JObject{{"type","session.input_audio.append"},{"audio",Convert.ToBase64String(bytes)}}).ConfigureAwait(false);}
            catch(Exception){if(!disposed)Error("Audio upload interrupted.");}
            finally{Interlocked.Decrement(ref queuedAudio);}
        }
        public async Task Command(string type)
        {try{await Send(new JObject{{"type",type}}).ConfigureAwait(false);}catch(Exception){if(!disposed)Error("Session command failed.");}}
        public void Dispose()
        {if(disposed)return;disposed=true;lifetime.Cancel();socket.Abort();socket.Dispose();}
    }
}
