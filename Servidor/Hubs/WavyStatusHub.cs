using Microsoft.AspNetCore.SignalR;

namespace Servidor.Hubs
{
    public class WavyStatusHub : Hub
    {
        public async Task SendWavyStatus(string wavyId, string status)
        {
            await Clients.All.SendAsync("ReceiveWavyStatus", wavyId, status);
        }
    }
} 