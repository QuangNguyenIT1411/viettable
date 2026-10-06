using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

internal sealed class CloudRealtimeProbe : IAsyncDisposable
{
    private readonly ClientWebSocket socket=new();
    public async Task ConnectAsync(string token)
    {
        socket.Options.SetRequestHeader("Authorization","Bearer "+token);
        await ConnectSocketAsync("/hubs/mobile-notifications");
    }
    public async Task ConnectStaffAsync(System.Net.CookieContainer cookies)
    {
        socket.Options.Cookies=cookies;
        await ConnectSocketAsync("/hubs/customer-notifications");
    }
    private async Task ConnectSocketAsync(string path)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await socket.ConnectAsync(new Uri("ws://127.0.0.1:5200"+path),timeout.Token);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"),WebSocketMessageType.Text,true,timeout.Token);
        var handshake=await ReadAsync(timeout.Token);
        if(!handshake.Contains("{}",StringComparison.Ordinal)) throw new CloudAcceptanceException("SignalR handshake rejected.");
    }
    public async Task AssertBranchAsync(int branch,bool allowed)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var invocation=Guid.NewGuid().ToString("N");
        var frame=JsonSerializer.Serialize(new{type=1,invocationId=invocation,target="SubscribeBranch",arguments=new[]{branch}})+"\u001e";
        await socket.SendAsync(Encoding.UTF8.GetBytes(frame),WebSocketMessageType.Text,true,timeout.Token);
        while(true)
        {
            foreach(var part in (await ReadAsync(timeout.Token)).Split('\u001e',StringSplitOptions.RemoveEmptyEntries))
            {
                using var json=JsonDocument.Parse(part);
                if(!json.RootElement.TryGetProperty("invocationId",out var id) || id.GetString()!=invocation) continue;
                var error=json.RootElement.TryGetProperty("error",out _);
                if(error==allowed) throw new CloudAcceptanceException("Staff SignalR branch authorization mismatch.");
                Console.WriteLine("PASS: staff SignalR "+(allowed?"own-branch subscription accepted":"foreign-branch subscription forbidden"));
                return;
            }
        }
    }
    public async Task ExpectAsync(string target, string? status = null)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while(true)
        {
            foreach(var frame in (await ReadAsync(timeout.Token)).Split('\u001e',StringSplitOptions.RemoveEmptyEntries))
            {
                using var json=JsonDocument.Parse(frame);
                if(json.RootElement.TryGetProperty("target",out var value)&&value.GetString()==target &&
                    (status is null || (json.RootElement.TryGetProperty("arguments",out var arguments) && arguments.GetArrayLength()>0 &&
                    MatchesState(arguments[0],status))))
                { Console.WriteLine("PASS: authenticated SignalR client received "+target+" from CloudHost Gateway action.");return; }
            }
        }
    }
    private static bool MatchesState(JsonElement payload,string status)
    {
        if((payload.TryGetProperty("status",out var state) || payload.TryGetProperty("statusCode",out state)) && state.GetString()==status) return true;
        // Chef's event includes item states; the grouped session aggregate can
        // already be SERVING while a later round is PREPARING.
        return payload.TryGetProperty("items",out var items) && items.EnumerateArray()
            .Any(x=>x.TryGetProperty("status",out var itemState) && itemState.GetString()==status);
    }
    private async Task<string> ReadAsync(CancellationToken ct)
    {
        using var bytes=new MemoryStream();var buffer=new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result=await socket.ReceiveAsync(buffer,ct);
            if(result.MessageType==WebSocketMessageType.Close)throw new CloudAcceptanceException("SignalR unexpectedly closed.");
            bytes.Write(buffer,0,result.Count);
            if(bytes.Length>1024*1024)throw new CloudAcceptanceException("SignalR frame limit exceeded.");
        }while(!result.EndOfMessage);
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
    public ValueTask DisposeAsync(){socket.Dispose();return ValueTask.CompletedTask;}
}
