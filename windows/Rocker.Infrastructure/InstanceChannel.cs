// SPDX-License-Identifier: AGPL-3.0-only
using System.IO.Pipes;
using System.Text;
namespace Rocker.Infrastructure;
public static class InstanceChannel
{
    public const int MaximumBytes=131072;
    public static async Task SendAsync(string name,string request,CancellationToken cancellation)
    {
        var bytes=Encoding.UTF8.GetBytes(request);if(bytes.Length>MaximumBytes)throw new InvalidDataException("Požadavek je příliš dlouhý.");using var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);await pipe.ConnectAsync(cancellation).ConfigureAwait(false);await pipe.WriteAsync(BitConverter.GetBytes(bytes.Length),cancellation).ConfigureAwait(false);await pipe.WriteAsync(bytes,cancellation).ConfigureAwait(false);await pipe.FlushAsync(cancellation).ConfigureAwait(false);byte[] ack=new byte[1];await pipe.ReadExactlyAsync(ack,cancellation).ConfigureAwait(false);if(ack[0]!=1)throw new IOException("Běžící aplikace požadavek nepřijala.");
    }
    public static async Task ListenAsync(string name,Action<string> receive,CancellationToken cancellation)
    {
        while(!cancellation.IsCancellationRequested)
        {
            try{using var pipe=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);await pipe.WaitForConnectionAsync(cancellation).ConfigureAwait(false);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(5));byte[] size=new byte[4];await pipe.ReadExactlyAsync(size,timeout.Token).ConfigureAwait(false);int count=BitConverter.ToInt32(size);if(count<0 || count>MaximumBytes)continue;byte[] bytes=new byte[count];await pipe.ReadExactlyAsync(bytes,timeout.Token).ConfigureAwait(false);string request=new UTF8Encoding(false,true).GetString(bytes);if(request.Any(char.IsControl) || request.Length>32767)continue;receive(request);await pipe.WriteAsync(new byte[]{1},timeout.Token).ConfigureAwait(false);await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);}
            catch(OperationCanceledException)when(cancellation.IsCancellationRequested){break;}
            catch(Exception error)when(error is IOException or OperationCanceledException or DecoderFallbackException){/* One broken client must not terminate the existing window. */try{await Task.Delay(25,cancellation).ConfigureAwait(false);}catch(OperationCanceledException)when(cancellation.IsCancellationRequested){break;}}
        }
    }
}
