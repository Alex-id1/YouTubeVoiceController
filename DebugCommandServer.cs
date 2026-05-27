using System.IO.Pipes;

namespace YouTubeVoiceController{
    /// <summary>
    /// Listens on a named pipe "YVCDebug" for text commands sent from PowerShell/cmd.
    /// Useful for testing without voice input.
    ///
    /// Protocol (one line per connection):
    ///   cmd:&lt;text&gt; => dispatches as voice command (e.g. "cmd:like")
    ///   search:&lt;query&gt; => executes YouTube search (e.g. "search:lo-fi music")
    ///
    /// PowerShell one-liner:
    ///   &amp; { $p = New-Object IO.Pipes.NamedPipeClientStream('.','YVCDebug','Out'); $p.Connect(2000); $w = New-Object IO.StreamWriter($p); $w.WriteLine('cmd:like'); $w.Flush(); $p.Dispose() }
    /// </summary>
    sealed class DebugCommandServer : IDisposable{
        public const string PipeName = "YVCDebug";

        private readonly Func<string, Task> _dispatchCmd;
        private readonly Func<string, Task> _dispatchSearch;
        private readonly CancellationTokenSource _cts = new();
        private Task _loopTask = Task.CompletedTask;

        public DebugCommandServer(Func<string, Task> dispatchCmd, Func<string, Task> dispatchSearch){
            _dispatchCmd = dispatchCmd;
            _dispatchSearch = dispatchSearch;
        }

        public void Start(){
            _loopTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            AppLogger.Info($"DebugCommandServer: listening on pipe '{PipeName}'");
        }

        private async Task ListenLoopAsync(CancellationToken ct){
            while (!ct.IsCancellationRequested){
                try{
                    using var pipe = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        transmissionMode: PipeTransmissionMode.Byte,
                        options: PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(ct);

                    using var reader = new StreamReader(pipe);
                    string? line = await reader.ReadLineAsync(ct);
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    AppLogger.Info($"DebugCommandServer: received '{line}'");
                    _ = HandleAsync(line.Trim());
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex){
                    AppLogger.Warning($"DebugCommandServer pipe error: {ex.Message}");
                    await Task.Delay(500, ct).ConfigureAwait(false);
                }
            }
        }

        private async Task HandleAsync(string line){
            try{
                if (line.StartsWith("cmd:", StringComparison.OrdinalIgnoreCase)){
                    string cmd = line["cmd:".Length..].Trim();
                    await _dispatchCmd(cmd);
                }
                else if (line.StartsWith("search:", StringComparison.OrdinalIgnoreCase)){
                    string query = line["search:".Length..].Trim();
                    await _dispatchSearch(query);
                }
                else{
                    // Treat bare text as a command (convenience shortcut)
                    await _dispatchCmd(line);
                }
            }
            catch (Exception ex){
                AppLogger.Error($"DebugCommandServer.HandleAsync failed for '{line}'", ex);
            }
        }

        public void Dispose(){
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}