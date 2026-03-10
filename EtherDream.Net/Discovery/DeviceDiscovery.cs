using LaserCore.Etherdream.Net.Dto;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace LaserCore.Etherdream.Net.Discovery
{
    public struct UdpState
    {
        public UdpClient client;
        public bool disposing;
    }

    public class DeviceDiscovery: IDisposable
    {
        private readonly int Broadcast_Port = 7654;
        private UdpClient _discoveryClient;
        private bool disposedValue;

        public event EventHandler DevicesUpdated;
        public static ConcurrentDictionary<string, DacDto> DiscoveredDevices = new ConcurrentDictionary<string, DacDto>();
        public void ClearEntries()
        {
            DiscoveredDevices.Clear(); 
        }

        public DeviceDiscovery()
        {
            _discoveryClient = new UdpClient(Broadcast_Port);
            var udpState = new UdpState();
            udpState.client = _discoveryClient;
            
            Console.WriteLine("listening for messages");
            _discoveryClient.Client.ReceiveTimeout = 1000;
            _discoveryClient.BeginReceive(ReceiveCallback, udpState);
            DiscoveredDevices = new ConcurrentDictionary<string, DacDto>();
        }

        private DacBroadcastDto Deserialize(byte[] param)
        {
            Span<byte> bytes = param;
            DacBroadcastDto dto = MemoryMarshal.Cast<byte, DacBroadcastDto>(bytes)[0];
            return dto;
        }
        

        public  DacDto FindFirstDevice()
        {
            // TODO Handle socket no connection

            var remoteEP = new IPEndPoint(IPAddress.Any, Broadcast_Port);
            byte[] bytesReceived = _discoveryClient.Receive(ref remoteEP);

            var identity = Deserialize(bytesReceived);
            DacDto etherDream = new DacDto();
            etherDream.Identity = identity;
            etherDream.Ip = remoteEP.Address.ToString();

            DiscoveredDevices.TryAdd(etherDream.Ip, etherDream);

            return etherDream;

        }

        public void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                var udpState = (UdpState)(ar.AsyncState);
                var client = udpState.client;
                var remoteEP = new IPEndPoint(IPAddress.Any, Broadcast_Port);

                var bytesReceived = client.EndReceive(ar, ref remoteEP);
                var identity = Deserialize(bytesReceived);
                DacDto etherDream = new DacDto();
                etherDream.Identity = identity;
                etherDream.Ip = remoteEP.Address.ToString();

                if (DiscoveredDevices.TryAdd(etherDream.Ip, etherDream))
                    DevicesUpdated?.Invoke(this, EventArgs.Empty);

                // Restart only if not disposing
                client.BeginReceive(ReceiveCallback, udpState); 
            }
            catch (ObjectDisposedException)
            {
                // Client closed: exit silently
            }
        }

        public static string GetDeviceName(DacDto dac)
        {
            unsafe
            {
                var identity = dac.Identity;
                string dacName = String.Format("Ether Dream {0:X2}{1:X2}{2:X2}", identity.MacAddress[3], identity.MacAddress[4], identity.MacAddress[5]);
                return dacName;
            }
        }

        public static string GetDeviceIp(DacDto dac)
        {
            unsafe
            {
                return dac.Ip;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _discoveryClient.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
