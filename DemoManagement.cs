using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;

namespace MatchZy
{
    public partial class MatchZy
    {
        public string demoPath = "MatchZy/";
        public string demoNameFormat = "{TIME}_{MATCH_ID}_{MAP}_{TEAM1}_vs_{TEAM2}";
        public string demoUploadURL = "";
        public string demoUploadHeaderKey = "";
        public string demoUploadHeaderValue = "";

        public string activeDemoFile = "";

        public bool isDemoRecording = false;
        public bool isDemoRecordingEnabled = true;

        public void StartDemoRecording()
        {
            if (!isDemoRecordingEnabled)
            {
                Log("[StartDemoRecording] Demo recording is disabled.");
                return;
            }
            if (isDemoRecording)
            {
                Log("[StartDemoRecording] Demo recording is already in progress.");
                return;
            }
            string demoFileName = FormatCvarValue(demoNameFormat.Replace(" ", "_")) + ".dem";
            try
            {
                string? directoryPath = Path.GetDirectoryName(
                    Path.Join(Server.GameDirectory + "/csgo/", demoPath)
                );
                if (directoryPath != null)
                {
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                }
                string tempDemoPath = demoPath == "" ? demoFileName : demoPath + demoFileName;
                activeDemoFile = tempDemoPath;

                // The engine resolves a relative tv_record path against the first Game search path in
                // gameinfo.gi, which is csgo/addons/metamod on Metamod servers. An absolute path keeps the
                // demo in csgo/, where the folder above was created and the checks and upload look for it.
                string recordPath = Path.Join(Server.GameDirectory, "csgo", tempDemoPath)
                    .Replace('\\', '/');
                string recordArg = recordPath.Contains(' ') ? $"\"{recordPath}\"" : recordPath;

                // tv_record_immediate 1 makes GOTV write the .dem while the match runs instead of buffering
                // it, so the file is on disk (which is what the verification below checks) and survives a
                // server crash mid-match.
                Server.ExecuteCommand($"tv_record_immediate 1;tv_record {recordArg}");
                isDemoRecording = true;
                Log($"[StartDemoRecording] tv_record {recordArg}");
            }
            catch (Exception ex)
            {
                Log(
                    $"[StartDemoRecording - FATAL] Error: {ex.Message}. Starting demo recording with path. Name: {demoFileName}"
                );
                // This is to avoid demo loss in any case of exception
                Server.ExecuteCommand($"tv_record {demoFileName}");
                isDemoRecording = true;
            }
        }

        public void StopDemoRecording(
            float delay,
            string activeDemoFile,
            long liveMatchId,
            int currentMapNumber
        )
        {
            Log($"[StopDemoRecording] Going to stop demorecording in {delay}s");
            string demoPath = Path.Join(Server.GameDirectory + "/csgo/", activeDemoFile);
            (int t1score, int t2score) = GetTeamsScore();
            int roundNumber = t1score + t2score;
            AddTimer(
                delay,
                () =>
                {
                    if (isDemoRecording)
                    {
                        Server.ExecuteCommand($"tv_stoprecord");
                    }
                    isDemoRecording = false;
                    AddTimer(
                        15,
                        () =>
                        {
                            Task.Run(async () =>
                            {
                                await UploadFileAsync(
                                    demoPath,
                                    demoUploadURL,
                                    demoUploadHeaderKey,
                                    demoUploadHeaderValue,
                                    liveMatchId,
                                    currentMapNumber,
                                    roundNumber
                                );
                            });
                        }
                    );
                }
            );
        }

        public int GetTvDelay()
        {
            bool tvEnable = ConVar.Find("tv_enable")!.GetPrimitiveValue<bool>();
            if (!tvEnable)
                return 0;

            bool tvEnable1 = ConVar.Find("tv_enable1")!.GetPrimitiveValue<bool>();
            int tvDelay = ConVar.Find("tv_delay")!.GetPrimitiveValue<int>();

            if (!tvEnable1)
                return tvDelay;
            int tvDelay1 = ConVar.Find("tv_delay1")!.GetPrimitiveValue<int>();

            if (tvDelay < tvDelay1)
                return tvDelay1;
            return tvDelay;
        }

        [ConsoleCommand(
            "get5_demo_upload_header_key",
            "If defined, a custom HTTP header with this name is added to the HTTP requests for demos"
        )]
        [ConsoleCommand(
            "matchzy_demo_upload_header_key",
            "If defined, a custom HTTP header with this name is added to the HTTP requests for demos"
        )]
        public void DemoUploadHeaderKeyCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player != null)
                return;
            string header = command.ArgByIndex(1).Trim();

            if (header != "")
                demoUploadHeaderKey = header;
        }

        [ConsoleCommand(
            "get5_demo_upload_header_value",
            "If defined, the value of the custom header added to the demos sent over HTTP"
        )]
        [ConsoleCommand(
            "matchzy_demo_upload_header_value",
            "If defined, the value of the custom header added to the demos sent over HTTP"
        )]
        public void DemoUploadHeaderValueCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player != null)
                return;
            string headerValue = command.ArgByIndex(1).Trim();

            if (headerValue != "")
                demoUploadHeaderValue = headerValue;
        }
    }
}
