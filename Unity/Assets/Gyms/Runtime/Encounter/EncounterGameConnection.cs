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
    // Single-use transport. Its detached queue is consumed only on the Unity main thread.
    public sealed class EncounterGameConnection : IDisposable
    {
        const int MaxBytes = 262144, MaxEvents = 128, MaxPendingWrites = 8;
        readonly ClientWebSocket socket = new ClientWebSocket();
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly SemaphoreSlim writer = new SemaphoreSlim(1, 1);
        readonly ConcurrentQueue<JObject> events = new ConcurrentQueue<JObject>();
        readonly object gate = new object();
        bool disposed, terminal;
        int started, pendingWrites;

        public bool TryRead(out JObject value) => events.TryDequeue(out value);

        public static Uri ValidateAddress(string address)
            => RelayAddressPolicy.Validate(address);

        public async Task Connect(string address, JObject startup)
        {
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            try
            {
                var initial = startup == null ? throw new ArgumentNullException(nameof(startup)) : (JObject)startup.DeepClone();
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    timeout.CancelAfter(12000);
                    await socket.ConnectAsync(ValidateAddress(address), timeout.Token).ConfigureAwait(false);
                }
                if (!await Send(initial).ConfigureAwait(false)) return;
                await Receive().ConfigureAwait(false);
            }
            catch (Exception) { Finish("connection_failed"); }
        }

        public async Task<bool> Send(JObject message)
        {
            if (Interlocked.Increment(ref pendingWrites) > MaxPendingWrites)
            { Interlocked.Decrement(ref pendingWrites); Finish("outbound_limit"); return false; }
            bool acquired = false;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message.ToString(Newtonsoft.Json.Formatting.None));
                if (bytes.Length > MaxBytes) { Finish("outbound_limit"); return false; }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(5000);
                await writer.WaitAsync(timeout.Token).ConfigureAwait(false);
                acquired = true;
                lock (gate) if (disposed || terminal) return false;
                if (socket.State != WebSocketState.Open) return false;
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                return true;
            }
            catch (Exception) { Finish("send_failed"); return false; }
            finally { if (acquired) writer.Release(); Interlocked.Decrement(ref pendingWrites); }
        }

        async Task Receive()
        {
            var buffer = new byte[16384];
            while (!lifetime.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), lifetime.Token).ConfigureAwait(false);
                    if (part.MessageType == WebSocketMessageType.Close) { Finish("closed"); return; }
                    if (part.MessageType != WebSocketMessageType.Text || message.Length + part.Count > MaxBytes)
                    { Finish("invalid_frame"); return; }
                    message.Write(buffer, 0, part.Count);
                } while (!part.EndOfMessage);
                var item = JObject.Parse(Encoding.UTF8.GetString(message.ToArray()));
                if (item["type"]?.Type != JTokenType.String) { Finish("invalid_frame"); return; }
                lock (gate)
                {
                    if (disposed || terminal) return;
                    if (events.Count < MaxEvents - 1) { events.Enqueue(item); continue; }
                }
                Finish("inbound_limit"); return;
            }
        }

        void Finish(string code)
        {
            lock (gate)
            {
                if (disposed || terminal) return;
                terminal = true;
                if (code == "inbound_limit") while (events.TryDequeue(out _)) { }
                events.Enqueue(new JObject { ["type"] = "game.transport.closed", ["code"] = code });
                lifetime.Cancel(); socket.Abort();
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                while (events.TryDequeue(out _)) { }
                lifetime.Cancel(); socket.Abort(); socket.Dispose();
            }
            // Do not dispose writer/CTS while awaited operations are still unwinding.
        }
    }
}
