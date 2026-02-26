//using System.Diagnostics;
//using MeetingReminder.Infrastructure.Notifications;

//namespace MeetingReminder.Infrastructure.Linux.Notifications;

///// <summary>
///// Linux implementation of sound playback using aplay or paplay.
///// </summary>
//public class SoundPlayer : ISoundPlayer
//{
//    private readonly string? _playerCommand;

//    public SoundPlayer()
//    {
//        _playerCommand = DetectAudioPlayer();
//    }

//    public bool IsSupported => OperatingSystem.IsLinux() && _playerCommand != null;

//    public async Task PlayAsync(string filePath, float volume)
//    {
//        if (!IsSupported || _playerCommand == null)
//            return;

//        var arguments = BuildArguments(filePath, volume);

//        var startInfo = new ProcessStartInfo
//        {
//            FileName = _playerCommand,
//            Arguments = arguments,
//            UseShellExecute = false,
//            CreateNoWindow = true,
//            RedirectStandardOutput = true,
//            RedirectStandardError = true
//        };

//        using var process = Process.Start(startInfo);
//        if (process != null)
//        {
//            await process.WaitForExitAsync();
//        }
//    }

//    private string BuildArguments(string filePath, float volume)
//    {
//        var escapedPath = $"\"{filePath.Replace("\"", "\\\"")}\"";

//        if (_playerCommand == "paplay")
//        {
//            var volumeValue = (int)(volume * 65536);
//            return $"--volume={volumeValue} {escapedPath}";
//        }

//        return escapedPath;
//    }

//    private static string? DetectAudioPlayer()
//    {
//        if (IsCommandAvailable("paplay"))
//            return "paplay";

//        if (IsCommandAvailable("aplay"))
//            return "aplay";

//        return null;
//    }

//    private static bool IsCommandAvailable(string command)
//    {
//        try
//        {
//            var startInfo = new ProcessStartInfo
//            {
//                FileName = "which",
//                Arguments = command,
//                UseShellExecute = false,
//                CreateNoWindow = true,
//                RedirectStandardOutput = true,
//                RedirectStandardError = true
//            };

//            using var process = Process.Start(startInfo);
//            process?.WaitForExit(1000);
//            return process?.ExitCode == 0;
//        }
//        catch
//        {
//            return false;
//        }
//    }
//}
