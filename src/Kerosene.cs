using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

[assembly: AssemblyTitle("Kerosene")]
[assembly: AssemblyDescription("Recuperação adaptativa de desempenho após jogos pesados")]
[assembly: AssemblyCompany("created by @frstt")]
[assembly: AssemblyProduct("Kerosene")]
[assembly: AssemblyCopyright("created by @frstt")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

internal static class KeroseneApp
{
    private const int SystemMemoryListInformation = 80;
    private const int MemoryPurgeStandbyList = 4;
    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint SePrivilegeEnabled = 0x00000002;
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    private const string Purple = "\x1b[38;2;206;126;255m";
    private const string Lavender = "\x1b[38;2;180;150;255m";
    private const string Pink = "\x1b[38;2;239;170;255m";
    private const string White = "\x1b[38;2;245;242;255m";
    private const string Gray = "\x1b[38;2;145;140;160m";
    private const string Green = "\x1b[38;2;120;235;170m";
    private const string Yellow = "\x1b[38;2;255;215;120m";
    private const string Reset = "\x1b[0m";

    private static bool ansiEnabled;

    private static readonly HashSet<string> LauncherNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Battle.net", "Agent", "steamwebhelper", "Discord", "EpicGamesLauncher",
        "EADesktop", "EALauncher", "EABackgroundService", "Overwolf",
        "CurseForge", "RiotClientServices", "RiotClientUx", "UbisoftConnect", "upc",
        "GalaxyClient", "GalaxyClientService", "RockstarService", "LauncherPatcher"
    };

    private static readonly HashSet<string> TrimNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Battle.net", "Agent", "steamwebhelper", "EpicGamesLauncher",
        "EADesktop", "EALauncher", "EABackgroundService", "Overwolf",
        "CurseForge", "RiotClientServices", "RiotClientUx", "UbisoftConnect", "upc",
        "GalaxyClient", "GalaxyClientService", "RockstarService", "LauncherPatcher"
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public Luid Luid;
        public uint Attributes;
    }

    private sealed class Result
    {
        public ulong TotalMb;
        public ulong BeforeAvailableMb;
        public ulong AfterAvailableMb;
        public uint BeforeLoad;
        public uint AfterLoad;
        public int PrioritiesAdjusted;
        public int WorkingSetsTrimmed;
        public bool StandbyNeeded;
        public bool StandbyPurged;
        public bool PowerPlanRefreshed;
        public string PowerPlanName = "Plano atual";
        public readonly List<string> Errors = new List<string>();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx status);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string systemName, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        bool disableAllPrivileges,
        ref TokenPrivileges newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int informationClass, ref int information, int informationLength);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr processHandle);

    private static string C(string ansi, ConsoleColor fallback)
    {
        if (ansiEnabled)
            return ansi;
        Console.ForegroundColor = fallback;
        return String.Empty;
    }

    private static void W(string color, ConsoleColor fallback, string text)
    {
        Console.Write(C(color, fallback));
        Console.Write(text);
        if (ansiEnabled)
            Console.Write(Reset);
        else
            Console.ResetColor();
    }

    private static void WL(string color, ConsoleColor fallback, string text)
    {
        W(color, fallback, text);
        Console.WriteLine();
    }

    private static void ConfigureConsole()
    {
        Console.Title = "KEROSENE";
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            int width = Math.Min(94, Console.LargestWindowWidth);
            int height = Math.Min(32, Console.LargestWindowHeight);
            if (Console.BufferWidth < width)
                Console.SetBufferSize(width, Math.Max(height, Console.BufferHeight));
            Console.SetWindowSize(width, height);
        }
        catch
        {
        }

        IntPtr output = GetStdHandle(StdOutputHandle);
        uint mode;
        ansiEnabled = output != IntPtr.Zero &&
                      GetConsoleMode(output, out mode) &&
                      SetConsoleMode(output, mode | EnableVirtualTerminalProcessing);
    }

    private static void PrintHeader()
    {
        Console.Clear();
        WL(Purple, ConsoleColor.Magenta, "╔══════════════════════════════════════════════════════════════════════════════════════╗");
        WL(Purple, ConsoleColor.Magenta, "║                                                                                      ║");
        WL(Pink, ConsoleColor.Magenta,   "║  ██╗  ██╗███████╗██████╗  ██████╗ ███████╗███████╗███╗   ██╗███████╗              ║");
        WL(Lavender, ConsoleColor.DarkMagenta, "║  ██║ ██╔╝██╔════╝██╔══██╗██╔═══██╗██╔════╝██╔════╝████╗  ██║██╔════╝              ║");
        WL(Purple, ConsoleColor.Magenta, "║  █████╔╝ █████╗  ██████╔╝██║   ██║███████╗█████╗  ██╔██╗ ██║█████╗                ║");
        WL(Lavender, ConsoleColor.DarkMagenta, "║  ██╔═██╗ ██╔══╝  ██╔══██╗██║   ██║╚════██║██╔══╝  ██║╚██╗██║██╔══╝                ║");
        WL(Pink, ConsoleColor.Magenta,   "║  ██║  ██╗███████╗██║  ██║╚██████╔╝███████║███████╗██║ ╚████║███████╗              ║");
        WL(Purple, ConsoleColor.Magenta, "║  ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚══════╝╚═╝  ╚═══╝╚══════╝              ║");
        WL(Gray, ConsoleColor.DarkGray,  "║                               created by @frstt                                     ║");
        WL(Purple, ConsoleColor.Magenta, "╚══════════════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private static void Stage(string id, string text)
    {
        W(Purple, ConsoleColor.Magenta, "  [" + id + "] ");
        W(White, ConsoleColor.White, text.PadRight(56));
        Thread.Sleep(260);
        WL(Green, ConsoleColor.Green, " OK");
    }

    private static MemoryStatusEx ReadMemory()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return status;
    }

    private static bool EnablePrivilege(string privilegeName)
    {
        IntPtr token;
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out token))
            return false;

        try
        {
            Luid luid;
            if (!LookupPrivilegeValue(null, privilegeName, out luid))
                return false;

            var privileges = new TokenPrivileges
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SePrivilegeEnabled
            };

            return AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero) &&
                   Marshal.GetLastWin32Error() == 0;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static bool PurgeStandbyMemory()
    {
        if (!EnablePrivilege("SeProfileSingleProcessPrivilege"))
            return false;
        int command = MemoryPurgeStandbyList;
        return NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int)) == 0;
    }

    private static bool RefreshCurrentPowerPlan(out string planName)
    {
        planName = "Plano atual";
        try
        {
            var readInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "powercfg.exe"),
                Arguments = "/getactivescheme",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            string output;
            using (Process process = Process.Start(readInfo))
            {
                if (process == null)
                    return false;
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
            }

            Match guid = Regex.Match(output ?? String.Empty, "[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
            if (!guid.Success)
                return false;

            Match name = Regex.Match(output ?? String.Empty, "\\(([^()]*)\\)\\s*$");
            if (name.Success && !String.IsNullOrWhiteSpace(name.Groups[1].Value))
                planName = name.Groups[1].Value.Trim();

            var applyInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "powercfg.exe"),
                Arguments = "/setactive " + guid.Value,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(applyInfo))
            {
                if (process == null)
                    return false;
                process.WaitForExit(5000);
                return process.HasExited && process.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    private static void TuneLaunchers(Result result)
    {
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (!LauncherNames.Contains(process.ProcessName))
                    continue;

                ProcessPriorityClass priority = process.PriorityClass;
                if (priority == ProcessPriorityClass.AboveNormal ||
                    priority == ProcessPriorityClass.High ||
                    priority == ProcessPriorityClass.RealTime)
                {
                    process.PriorityClass = ProcessPriorityClass.Normal;
                    result.PrioritiesAdjusted++;
                }
                else if (priority == ProcessPriorityClass.Normal && process.MainWindowHandle == IntPtr.Zero)
                {
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
                    result.PrioritiesAdjusted++;
                }

                if (TrimNames.Contains(process.ProcessName) &&
                    process.MainWindowHandle == IntPtr.Zero &&
                    process.WorkingSet64 >= 160L * 1024L * 1024L &&
                    EmptyWorkingSet(process.Handle))
                {
                    result.WorkingSetsTrimmed++;
                }
            }
            catch
            {
                // Processos podem encerrar ou bloquear acesso durante a varredura.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static Result Recover()
    {
        var result = new Result();
        MemoryStatusEx before = ReadMemory();
        result.TotalMb = before.TotalPhysical / 1024 / 1024;
        result.BeforeAvailableMb = before.AvailablePhysical / 1024 / 1024;
        result.BeforeLoad = before.MemoryLoad;

        Stage("01", "Mapeando pressão de RAM e paginação");
        result.PowerPlanRefreshed = RefreshCurrentPowerPlan(out result.PowerPlanName);
        Stage("02", "Reaplicando o plano de energia atual");
        TuneLaunchers(result);
        Stage("03", "Normalizando launchers e processos auxiliares");

        ulong dynamicReserve = Math.Max(1024UL, result.TotalMb * 12UL / 100UL);
        result.StandbyNeeded = before.MemoryLoad >= 80 || result.BeforeAvailableMb < dynamicReserve;
        if (result.StandbyNeeded)
            result.StandbyPurged = PurgeStandbyMemory();
        Stage("04", result.StandbyNeeded ? "Liberando memória de espera sob pressão" : "Preservando cache útil do Windows");

        Thread.Sleep(650);
        MemoryStatusEx after = ReadMemory();
        result.AfterAvailableMb = after.AvailablePhysical / 1024 / 1024;
        result.AfterLoad = after.MemoryLoad;
        Stage("05", "Validando o estado final do sistema");
        return result;
    }

    private static void PrintResult(Result result)
    {
        long recovered = (long)result.AfterAvailableMb - (long)result.BeforeAvailableMb;
        Console.WriteLine();
        WL(Purple, ConsoleColor.Magenta, "  ┌────────────────────────────── RESULTADO ──────────────────────────────┐");
        W(White, ConsoleColor.White, "  │ RAM disponível : ");
        W(Lavender, ConsoleColor.Magenta, String.Format("{0:N0} MB  →  {1:N0} MB", result.BeforeAvailableMb, result.AfterAvailableMb));
        WL(White, ConsoleColor.White, String.Format("  ({0:+#;-#;0} MB)", recovered).PadRight(19) + "│");
        WL(White, ConsoleColor.White, String.Format("  │ Carga de memória: {0}% → {1}%", result.BeforeLoad, result.AfterLoad).PadRight(79) + "│");
        WL(White, ConsoleColor.White, String.Format("  │ Prioridades ajustadas: {0}   •   auxiliares reduzidos: {1}", result.PrioritiesAdjusted, result.WorkingSetsTrimmed).PadRight(79) + "│");
        WL(White, ConsoleColor.White, ("  │ Plano de energia: " + result.PowerPlanName).PadRight(79) + "│");
        string standby = result.StandbyNeeded
            ? (result.StandbyPurged ? "liberada com sucesso" : "não disponível nesta máquina")
            : "preservada — não havia pressão real";
        WL(White, ConsoleColor.White, ("  │ Memória de espera: " + standby).PadRight(79) + "│");
        WL(Purple, ConsoleColor.Magenta, "  └────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        WL(Green, ConsoleColor.Green, "  [ SYSTEM STABILIZED ]  Recuperação concluída.");
        WL(Gray, ConsoleColor.DarkGray, "  created by @frstt");
    }

    private static void WriteLog(Result result)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Kerosene");
            Directory.CreateDirectory(directory);
            string text = String.Format(
                "Kerosene - created by @frstt\r\n{0:yyyy-MM-dd HH:mm:ss}\r\n" +
                "RAM total: {1} MB\r\nRAM disponível: {2} -> {3} MB\r\n" +
                "Carga: {4}% -> {5}%\r\nPrioridades ajustadas: {6}\r\n" +
                "Auxiliares reduzidos: {7}\r\nStandby necessária: {8}\r\n" +
                "Standby liberada: {9}\r\nPlano reaplicado: {10} ({11})\r\n",
                DateTime.Now,
                result.TotalMb,
                result.BeforeAvailableMb,
                result.AfterAvailableMb,
                result.BeforeLoad,
                result.AfterLoad,
                result.PrioritiesAdjusted,
                result.WorkingSetsTrimmed,
                result.StandbyNeeded,
                result.StandbyPurged,
                result.PowerPlanRefreshed,
                result.PowerPlanName);
            File.WriteAllText(Path.Combine(directory, "last-run.log"), text, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static void CountdownAndClose(int seconds)
    {
        Console.WriteLine();
        for (int remaining = seconds; remaining > 0; remaining--)
        {
            Console.Write("\r");
            W(Gray, ConsoleColor.DarkGray, "  Fechando automaticamente em ");
            W(Pink, ConsoleColor.Magenta, remaining.ToString());
            W(Gray, ConsoleColor.DarkGray, " segundo(s)...   ");
            Thread.Sleep(1000);
        }
        Console.WriteLine();
    }

    [STAThread]
    private static int Main()
    {
        ConfigureConsole();
        PrintHeader();

        try
        {
            Result result = Recover();
            WriteLog(result);
            PrintResult(result);
            CountdownAndClose(5);
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine();
            WL(Yellow, ConsoleColor.Yellow, "  [!] A recuperação encontrou um erro nesta máquina:");
            WL(White, ConsoleColor.White, "      " + error.Message);
            WL(Gray, ConsoleColor.DarkGray, "      created by @frstt");
            CountdownAndClose(8);
            return 1;
        }
    }
}
