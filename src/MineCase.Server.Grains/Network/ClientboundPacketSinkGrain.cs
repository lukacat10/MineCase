using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MineCase.Protocol;
using MineCase.Protocol.Handshaking;
using Orleans;
using Orleans.Concurrency;

namespace MineCase.Server.Network
{
    internal class ClientboundPacketSinkGrain : Grain, IClientboundPacketSink
    {
        private List<IClientboundPacketObserver> _observers;
        private readonly IPacketPackager _packetPackager;

        public ClientboundPacketSinkGrain(IPacketPackager packetPackager)
        {
            _packetPackager = packetPackager;
        }

        public override Task OnActivateAsync()
        {
            _observers = new List<IClientboundPacketObserver>();
            return base.OnActivateAsync();
        }

        // Clients call this to subscribe.
        public Task Subscribe(IClientboundPacketObserver observer)
        {
            _observers.Add(observer);
            return Task.CompletedTask;
        }

        // Also clients use this to unsubscribe themselves to no longer receive the messages.
        public Task UnSubscribe(IClientboundPacketObserver observer)
        {
            _observers.Remove(observer);
            return Task.CompletedTask;
        }

        public async Task SendPacket(ISerializablePacket packet)
        {
            var prepared = await _packetPackager.PreparePacket(packet);
            await SendPacket(prepared.packetId, prepared.data.AsImmutable());
        }

        public Task SendPacket(uint packetId, Immutable<byte[]> data)
        {
            var packet = new UncompressedPacket
            {
                PacketId = packetId,
                Data = new ArraySegment<byte>(data.Value)
            };
            if (_observers.Count == 0)
                DeactivateOnIdle();
            else
                _observers.ForEach(n => n.ReceivePacket(packet));
            return Task.CompletedTask;
        }

        public Task Close()
        {
            _observers.ForEach(n => n.OnClosed());
            _observers.Clear();
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public Task NotifyUseCompression(uint threshold)
        {
            _observers.ForEach(n => n.UseCompression(threshold));
            return Task.CompletedTask;
        }
    }
}