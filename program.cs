using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

Console.OutputEncoding = Encoding.UTF8;

var auditLogger = new AuditLogger();
var storage = new StorageInitializer(auditLogger);
var repository = new ServiceRecordRepository(auditLogger);
var validator = new RecordValidator();
var reportGen = new ReportGenerator(repository, auditLogger);
var menuController = new MenuController(repository, validator, reportGen, auditLogger);

storage.Initialize();
menuController.Run();

static class AppConstants
{
    public const string DataFolder = "data";
    public const string RecordsFile = "records.dat";
    public const string AuditFile = "audit.log";
    public const string ReportsFolder = "reports";
    public const string AppName = "Vehicle Service Records System";
    public const string Version = "v1.0.0";
}

class ServiceRecord
{
    public string RecordId { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public decimal ServiceCost { get; set; }
    public string Mechanic { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string Checksum { get; set; } = string.Empty;

    public static string[] ServiceTypes => new[]
    {
        "Oil Change", "Brake Repair", "Tire Rotation", "Engine Tune-Up",
        "Transmission Service", "Battery Replacement", "Air Filter",
        "Coolant Flush", "Wheel Alignment", "General Inspection"
    };

    public string ToCsvLine() =>
        string.Join("|", new[]
        {
            Esc(RecordId), Esc(PlateNumber), Esc(OwnerName), Esc(ServiceType),
            ServiceCost.ToString("F2"), Esc(Mechanic),
            CreatedAt.ToString("o"), UpdatedAt.ToString("o"),
            IsActive ? "1" : "0", Esc(Checksum)
        });

    public static ServiceRecord? FromCsvLine(string line)
    {
        var p = line.Split('|');
        if (p.Length < 10) return null;
        return new ServiceRecord
        {
            RecordId = Une(p[0]),
            PlateNumber = Une(p[1]),
            OwnerName = Une(p[2]),
            ServiceType = Une(p[3]),
            ServiceCost = decimal.TryParse(p[4], out var c) ? c : 0,
            Mechanic = Une(p[5]),
            CreatedAt = DateTime.Parse(p[6]),
            UpdatedAt = DateTime.Parse(p[7]),
            IsActive = p[8] == "1",
            Checksum = Une(p[9])
        };
    }

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("|", "\\|").Replace("\n", "\\n");
    private static string Une(string s) => s.Replace("\\n", "\n").Replace("\\|", "|").Replace("\\\\", "\\");
}

static class ChecksumService
{
    public static string Compute(ServiceRecord r)
    {
        var raw = $"{r.RecordId}|{r.PlateNumber}|{r.OwnerName}|{r.ServiceType}|{r.ServiceCost:F2}|{r.Mechanic}|{r.CreatedAt:o}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16];
    }

    public static bool Verify(ServiceRecord r) => r.Checksum == Compute(r);
}

class AuditLogger
{
    private readonly string _logPath = Path.Combine(AppConstants.DataFolder, AppConstants.AuditFile);
    private readonly object _lock = new();

    public void Log(string action, string details, string level = "INFO")
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level,-5}] [{action,-10}] {details}";
        lock (_lock)
        {
            try { File.AppendAllText(_logPath, entry + Environment.NewLine); }
            catch { }
        }
    }

    public void LogAdd(string id, string details) => Log("ADD", $"RecordId={id} | {details}");
    public void LogUpdate(string id, string details) => Log("UPDATE", $"RecordId={id} | {details}");
    public void LogDelete(string id, bool hard) => Log("DELETE", $"RecordId={id} | Type={(hard ? "HARD" : "SOFT")}");
    public void LogRead(string details) => Log("READ", details);
    public void LogError(string details) => Log("ERROR", details, "ERROR");
    public void LogReport(string name) => Log("REPORT", $"Generated: {name}");

    public List<string> GetRecentLogs(int count = 30)
    {
        if (!File.Exists(_logPath)) return new List<string>();
        var lines = File.ReadAllLines(_logPath);
        return lines.Skip(Math.Max(0, lines.Length - count)).ToList();
    }
}

class StorageInitializer(AuditLogger logger)
{
    public void Initialize()
    {
        try
        {
            if (!Directory.Exists(AppConstants.DataFolder))
                Directory.CreateDirectory(AppConstants.DataFolder);

            string rpts = Path.Combine(AppConstants.DataFolder, AppConstants.ReportsFolder);
            if (!Directory.Exists(rpts))
                Directory.CreateDirectory(rpts);

            string rec = Path.Combine(AppConstants.DataFolder, AppConstants.RecordsFile);
            if (!File.Exists(rec)) File.WriteAllText(rec, string.Empty);

            string aud = Path.Combine(AppConstants.DataFolder, AppConstants.AuditFile);
            if (!File.Exists(aud)) File.WriteAllText(aud, string.Empty);

            logger.Log("INIT", "Storage initialized successfully.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] Could not initialize storage: {ex.Message}");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }
}

static class ConsoleUI
{
    public static void PrintHeader(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  \u2554{new string('\u2550', 60)}\u2557");
        Console.WriteLine($"  \u2551  {title.PadRight(58)}\u2551");
        Console.WriteLine($"  \u255a{new string('\u2550', 60)}\u255d");
        Console.ResetColor();
    }

    public static void PrintBanner()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine("  \u2554\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2557");
        Console.WriteLine("  \u2551         VEHICLE SERVICE RECORDS MANAGEMENT SYSTEM        \u2551");
        Console.WriteLine("  \u2551                     File-Based Edition                   \u2551");
        Console.WriteLine("  \u255a\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u255d");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {AppConstants.Version}  |  {DateTime.Now:dddd, MMMM dd, yyyy}");
        Console.ResetColor();
        Console.WriteLine();
    }

    public static void Success(string msg) { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"  \u2714  {msg}"); Console.ResetColor(); }
    public static void Error(string msg) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"  \u2718  {msg}"); Console.ResetColor(); }
    public static void Warning(string msg) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine($"  \u26a0  {msg}"); Console.ResetColor(); }
    public static void Info(string msg) { Console.ForegroundColor = ConsoleColor.DarkCyan; Console.WriteLine($"  \u2139  {msg}"); Console.ResetColor(); }

    public static string Prompt(string label)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"  {label}: ");
        Console.ResetColor();
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    public static bool Confirm(string question)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  {question} [y/N]: ");
        Console.ResetColor();
        var k = Console.ReadLine()?.Trim().ToLower();
        return k == "y" || k == "yes";
    }

    public static void PressAnyKey()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("\n  Press any key to continue...");
        Console.ResetColor();
        Console.ReadKey(intercept: true);
    }

    public static void Divider(char ch = '\u2500', int width = 64)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {new string(ch, width)}");
        Console.ResetColor();
    }

    public static string SelectFromList(string prompt, string[] options)
    {
        Console.WriteLine($"\n  {prompt}");
        for (int i = 0; i < options.Length; i++)
            Console.WriteLine($"    [{i + 1}] {options[i]}");
        while (true)
        {
            var input = Prompt("Enter number");
            if (int.TryParse(input, out int c) && c >= 1 && c <= options.Length)
                return options[c - 1];
            Error("Invalid selection. Please try again.");
        }
    }
}

class RecordValidator
{
    private static readonly Regex PlateRegex = new(@"^[A-Z0-9\-]{4,10}$", RegexOptions.IgnoreCase);

    public (bool IsValid, List<string> Errors) Validate(ServiceRecord r)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(r.PlateNumber))
            errors.Add("Plate number is required.");
        else if (!PlateRegex.IsMatch(r.PlateNumber.Trim()))
            errors.Add("Plate number must be 4-10 alphanumeric/dash characters (e.g., ABC-123).");

        if (string.IsNullOrWhiteSpace(r.OwnerName))
            errors.Add("Owner name is required.");
        else if (r.OwnerName.Trim().Length < 2 || r.OwnerName.Trim().Length > 80)
            errors.Add("Owner name must be 2-80 characters.");

        if (string.IsNullOrWhiteSpace(r.ServiceType))
            errors.Add("Service type is required.");
        else if (!ServiceRecord.ServiceTypes.Contains(r.ServiceType, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Service type must be one of: {string.Join(", ", ServiceRecord.ServiceTypes)}.");

        if (r.ServiceCost < 0)
            errors.Add("Service cost cannot be negative.");
        else if (r.ServiceCost > 1_000_000)
            errors.Add("Service cost cannot exceed PHP 1,000,000.");

        if (string.IsNullOrWhiteSpace(r.Mechanic))
            errors.Add("Mechanic name is required.");
        else if (r.Mechanic.Trim().Length < 2 || r.Mechanic.Trim().Length > 60)
            errors.Add("Mechanic name must be 2-60 characters.");

        return (!errors.Any(), errors);
    }

    public bool TryParseDecimal(string? input, out decimal value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(input) && decimal.TryParse(input.Trim(), out value);
    }
}

class ServiceRecordRepository(AuditLogger logger)
{
    private readonly string _path = Path.Combine(AppConstants.DataFolder, AppConstants.RecordsFile);
    private readonly object _fileLock = new();

    public List<ServiceRecord> GetAll(bool includeInactive = false)
    {
        var records = new List<ServiceRecord>();
        lock (_fileLock)
        {
            if (!File.Exists(_path)) return records;
            var lines = File.ReadAllLines(_path);
            int n = 0;
            foreach (var line in lines)
            {
                n++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var r = ServiceRecord.FromCsvLine(line);
                if (r == null) { logger.LogError($"Malformed record at line {n}."); continue; }
                if (!ChecksumService.Verify(r)) { logger.LogError($"Checksum mismatch RecordId={r.RecordId} line {n}. Skipped."); continue; }
                if (includeInactive || r.IsActive) records.Add(r);
            }
        }
        return records;
    }

    public ServiceRecord? GetById(string id) =>
        GetAll(includeInactive: true).FirstOrDefault(r => r.RecordId.Equals(id, StringComparison.OrdinalIgnoreCase));

    public bool Add(ServiceRecord record)
    {
        try
        {
            lock (_fileLock) File.AppendAllText(_path, record.ToCsvLine() + Environment.NewLine);
            return true;
        }
        catch (Exception ex) { logger.LogError($"Add failed RecordId={record.RecordId}: {ex.Message}"); return false; }
    }

    public bool Update(ServiceRecord updated)
    {
        try
        {
            lock (_fileLock)
            {
                var all = ReadRaw();
                bool found = false;
                for (int i = 0; i < all.Count; i++)
                {
                    var r = ServiceRecord.FromCsvLine(all[i]);
                    if (r != null && r.RecordId.Equals(updated.RecordId, StringComparison.OrdinalIgnoreCase))
                    {
                        all[i] = updated.ToCsvLine();
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
                WriteRaw(all);
            }
            return true;
        }
        catch (Exception ex) { logger.LogError($"Update failed RecordId={updated.RecordId}: {ex.Message}"); return false; }
    }

    public bool SoftDelete(string id)
    {
        var r = GetById(id);
        if (r == null) return false;
        r.IsActive = false;
        r.UpdatedAt = DateTime.Now;
        r.Checksum = ChecksumService.Compute(r);
        return Update(r);
    }

    public bool HardDelete(string id)
    {
        try
        {
            lock (_fileLock)
            {
                var all = ReadRaw();
                var before = all.Count;
                all = all.Where(line => { var r = ServiceRecord.FromCsvLine(line); return r == null || !r.RecordId.Equals(id, StringComparison.OrdinalIgnoreCase); }).ToList();
                if (all.Count == before) return false;
                WriteRaw(all);
            }
            return true;
        }
        catch (Exception ex) { logger.LogError($"HardDelete failed RecordId={id}: {ex.Message}"); return false; }
    }

    private List<string> ReadRaw() =>
        File.Exists(_path) ? File.ReadAllLines(_path).Where(l => !string.IsNullOrWhiteSpace(l)).ToList() : new List<string>();

    private void WriteRaw(List<string> lines) => File.WriteAllLines(_path, lines);

    public string GenerateId() =>
        $"VSR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
}

class ReportGenerator(ServiceRecordRepository repo, AuditLogger logger)
{
    private readonly string _dir = Path.Combine(AppConstants.DataFolder, AppConstants.ReportsFolder);

    public void GenerateSummaryByServiceType()
    {
        var records = repo.GetAll();
        var fileName = $"report_by_service_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(_dir, fileName);
        var groups = records.GroupBy(r => r.ServiceType).OrderByDescending(g => g.Count());

        using var sw = new StreamWriter(filePath);
        Header(sw, "SERVICE TYPE SUMMARY REPORT");
        sw.WriteLine($"  Total Active Records : {records.Count}");
        sw.WriteLine($"  Report Generated At  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sw.WriteLine();
        sw.WriteLine($"  {"Service Type",-25} {"Count",6} {"Total Revenue",15} {"Avg Cost",12}");
        sw.WriteLine($"  {new string('-', 62)}");
        foreach (var g in groups)
            sw.WriteLine($"  {g.Key,-25} {g.Count(),6} {g.Sum(r => r.ServiceCost),15:N2} {g.Average(r => r.ServiceCost),12:N2}");
        sw.WriteLine();
        sw.WriteLine($"  TOTAL REVENUE: PHP {records.Sum(r => r.ServiceCost):N2}");
        Footer(sw);

        logger.LogReport(fileName);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  Report saved: {filePath}");
        Console.ResetColor();
        PrintFile(filePath);
    }

    public void GenerateHighValueServicesReport(decimal threshold = 3000m)
    {
        var records = repo.GetAll().Where(r => r.ServiceCost >= threshold).OrderByDescending(r => r.ServiceCost).ToList();
        var fileName = $"report_highvalue_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(_dir, fileName);

        using var sw = new StreamWriter(filePath);
        Header(sw, $"HIGH-VALUE SERVICES REPORT (Threshold: PHP {threshold:N2})");
        sw.WriteLine($"  Total Records Matching: {records.Count}");
        sw.WriteLine($"  Report Generated At   : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sw.WriteLine();
        sw.WriteLine($"  {"ID",-22} {"Plate",-12} {"Owner",-20} {"Service Type",-20} {"Cost",10} {"Mechanic",-20}");
        sw.WriteLine($"  {new string('-', 90)}");
        foreach (var r in records)
            sw.WriteLine($"  {r.RecordId,-22} {r.PlateNumber,-12} {Trunc(r.OwnerName, 18),-20} {Trunc(r.ServiceType, 18),-20} {r.ServiceCost,10:N2} {Trunc(r.Mechanic, 18),-20}");
        Footer(sw);

        logger.LogReport(fileName);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  Report saved: {filePath}");
        Console.ResetColor();
        PrintFile(filePath);
    }

    public void GenerateMechanicWorkloadReport()
    {
        var records = repo.GetAll();
        var fileName = $"report_mechanic_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(_dir, fileName);
        var groups = records.GroupBy(r => r.Mechanic).OrderByDescending(g => g.Count());

        using var sw = new StreamWriter(filePath);
        Header(sw, "MECHANIC WORKLOAD REPORT");
        sw.WriteLine($"  Total Active Records : {records.Count}");
        sw.WriteLine($"  Report Generated At  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sw.WriteLine();
        sw.WriteLine($"  {"Mechanic",-25} {"Jobs",6} {"Total Billed",14} {"Avg Cost",12} {"Top Service",-20}");
        sw.WriteLine($"  {new string('-', 80)}");
        foreach (var g in groups)
        {
            var top = g.GroupBy(r => r.ServiceType).OrderByDescending(s => s.Count()).First().Key;
            sw.WriteLine($"  {g.Key,-25} {g.Count(),6} {g.Sum(r => r.ServiceCost),14:N2} {g.Average(r => r.ServiceCost),12:N2} {top,-20}");
        }
        Footer(sw);

        logger.LogReport(fileName);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  Report saved: {filePath}");
        Console.ResetColor();
        PrintFile(filePath);
    }

    private static void Header(StreamWriter sw, string title)
    {
        sw.WriteLine(new string('=', 92));
        sw.WriteLine($"  {AppConstants.AppName.ToUpper()} \u2014 {title}");
        sw.WriteLine(new string('=', 92));
        sw.WriteLine();
    }

    private static void Footer(StreamWriter sw)
    {
        sw.WriteLine();
        sw.WriteLine(new string('=', 92));
        sw.WriteLine($"  END OF REPORT  |  Generated by {AppConstants.AppName} {AppConstants.Version}");
        sw.WriteLine(new string('=', 92));
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max] + "\u2026";

    private static void PrintFile(string path)
    {
        Console.WriteLine();
        try { foreach (var l in File.ReadAllLines(path)) Console.WriteLine(l); }
        catch { }
    }
}

class MenuController(ServiceRecordRepository repo, RecordValidator validator, ReportGenerator reportGen, AuditLogger logger)
{
    public void Run()
    {
        while (true)
        {
            ShowMenu();
            switch (Console.ReadLine()?.Trim())
            {
                case "1": AddRecord(); break;
                case "2": ViewRecords(); break;
                case "3": SearchRecords(); break;
                case "4": UpdateRecord(); break;
                case "5": SoftDelete(); break;
                case "6": ReportsMenu(); break;
                case "7": ViewAuditLog(); break;
                case "8": HardDelete(); break;
                case "0": Exit(); return;
                default:
                    ConsoleUI.Error("Invalid option. Please choose from the menu.");
                    ConsoleUI.PressAnyKey();
                    break;
            }
        }
    }

    private void ShowMenu()
    {
        ConsoleUI.PrintBanner();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  MAIN MENU");
        Console.ResetColor();
        ConsoleUI.Divider();
        (string key, string label)[] items =
        {
            ("1","Add Service Record"), ("2","View All Active Records"), ("3","Search / Filter Records"),
            ("4","Update Record"), ("5","Delete Record (Soft)"), ("6","Generate Reports"),
            ("7","View Audit Log"), ("8","Hard Delete Record"), ("0","Exit")
        };
        foreach (var (k, l) in items)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($"  [{k}] ");
            Console.ResetColor();
            Console.WriteLine(l);
        }
        ConsoleUI.Divider();
        Console.Write("  Select option: ");
    }

    private void AddRecord()
    {
        ConsoleUI.PrintHeader("ADD SERVICE RECORD");
        var r = new ServiceRecord { RecordId = repo.GenerateId(), CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };

        r.PlateNumber = ConsoleUI.Prompt("Plate Number (e.g., ABC-123)").ToUpper();
        r.OwnerName = ConsoleUI.Prompt("Owner Name");
        r.ServiceType = ConsoleUI.SelectFromList("Service Type:", ServiceRecord.ServiceTypes);

        string ci = ConsoleUI.Prompt("Service Cost (PHP)");
        if (!validator.TryParseDecimal(ci, out decimal cost))
        {
            ConsoleUI.Error("Invalid cost. Operation cancelled.");
            logger.LogError($"AddRecord cancelled: invalid cost '{ci}'");
            ConsoleUI.PressAnyKey(); return;
        }
        r.ServiceCost = cost;
        r.Mechanic = ConsoleUI.Prompt("Mechanic Name");

        var (ok, errs) = validator.Validate(r);
        if (!ok)
        {
            ConsoleUI.Error("Validation failed:");
            foreach (var e in errs) Console.WriteLine($"      \u2022 {e}");
            logger.LogError($"AddRecord validation failed: {string.Join("; ", errs)}");
            ConsoleUI.PressAnyKey(); return;
        }

        r.Checksum = ChecksumService.Compute(r);
        Console.WriteLine(); ConsoleUI.Divider(); Preview(r); ConsoleUI.Divider();

        if (!ConsoleUI.Confirm("Save this record?"))
        {
            ConsoleUI.Info("Operation cancelled."); ConsoleUI.PressAnyKey(); return;
        }

        if (repo.Add(r))
        {
            ConsoleUI.Success($"Record saved! ID: {r.RecordId}");
            logger.LogAdd(r.RecordId, $"Plate={r.PlateNumber}, Owner={r.OwnerName}, Service={r.ServiceType}, Cost={r.ServiceCost:F2}, Mechanic={r.Mechanic}");
        }
        else ConsoleUI.Error("Failed to save record. Check audit log.");

        ConsoleUI.PressAnyKey();
    }

    private void ViewRecords()
    {
        ConsoleUI.PrintHeader("ALL ACTIVE SERVICE RECORDS");
        var records = repo.GetAll();
        logger.LogRead($"View all active records. Count={records.Count}");
        if (!records.Any()) { ConsoleUI.Warning("No active records found."); ConsoleUI.PressAnyKey(); return; }
        Table(records);
        ConsoleUI.PressAnyKey();
    }

    private void SearchRecords()
    {
        ConsoleUI.PrintHeader("SEARCH / FILTER RECORDS");
        Console.WriteLine("  Search by:");
        Console.WriteLine("    [1] Plate Number");
        Console.WriteLine("    [2] Owner Name");
        Console.WriteLine("    [3] Service Type");
        Console.WriteLine("    [4] Mechanic");
        Console.WriteLine("    [5] Record ID");
        Console.WriteLine("    [0] Back");
        Console.Write("  Choice: ");
        var choice = Console.ReadLine()?.Trim();
        if (choice == "0") return;

        string kw = ConsoleUI.Prompt("Search keyword");
        if (string.IsNullOrWhiteSpace(kw)) { ConsoleUI.Warning("Empty keyword."); ConsoleUI.PressAnyKey(); return; }

        var all = repo.GetAll(includeInactive: true);
        var res = choice switch
        {
            "1" => all.Where(r => r.PlateNumber.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList(),
            "2" => all.Where(r => r.OwnerName.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList(),
            "3" => all.Where(r => r.ServiceType.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList(),
            "4" => all.Where(r => r.Mechanic.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList(),
            "5" => all.Where(r => r.RecordId.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList(),
            _ => new List<ServiceRecord>()
        };

        logger.LogRead($"Search field={choice}, keyword='{kw}', results={res.Count}");
        if (!res.Any()) { ConsoleUI.Warning($"No records found for '{kw}'."); ConsoleUI.PressAnyKey(); return; }
        ConsoleUI.Info($"{res.Count} record(s) found:");
        Table(res);
        ConsoleUI.PressAnyKey();
    }

    private void UpdateRecord()
    {
        ConsoleUI.PrintHeader("UPDATE SERVICE RECORD");
        string id = ConsoleUI.Prompt("Enter Record ID to update");
        var r = repo.GetById(id);

        if (r == null)
        {
            ConsoleUI.Error($"Record '{id}' not found.");
            logger.LogError($"UpdateRecord: RecordId='{id}' not found.");
            ConsoleUI.PressAnyKey(); return;
        }
        if (!r.IsActive)
        {
            ConsoleUI.Warning("This record is inactive (soft-deleted).");
            if (!ConsoleUI.Confirm("Update anyway?")) { ConsoleUI.PressAnyKey(); return; }
        }

        Console.WriteLine(); Preview(r); ConsoleUI.Divider();
        ConsoleUI.Info("Leave blank to keep current value.");
        Console.WriteLine();

        string np = ConsoleUI.Prompt($"Plate Number [{r.PlateNumber}]");
        if (!string.IsNullOrWhiteSpace(np)) r.PlateNumber = np.ToUpper();

        string no = ConsoleUI.Prompt($"Owner Name [{r.OwnerName}]");
        if (!string.IsNullOrWhiteSpace(no)) r.OwnerName = no;

        if (ConsoleUI.Confirm("Change Service Type?"))
            r.ServiceType = ConsoleUI.SelectFromList("Service Type:", ServiceRecord.ServiceTypes);

        string nc = ConsoleUI.Prompt($"Service Cost [{r.ServiceCost:F2}]");
        if (!string.IsNullOrWhiteSpace(nc))
        {
            if (validator.TryParseDecimal(nc, out decimal cv)) r.ServiceCost = cv;
            else ConsoleUI.Warning("Invalid cost input — keeping original.");
        }

        string nm = ConsoleUI.Prompt($"Mechanic [{r.Mechanic}]");
        if (!string.IsNullOrWhiteSpace(nm)) r.Mechanic = nm;

        var (ok, errs) = validator.Validate(r);
        if (!ok)
        {
            ConsoleUI.Error("Validation failed:");
            foreach (var e in errs) Console.WriteLine($"      \u2022 {e}");
            logger.LogError($"UpdateRecord validation failed for {id}: {string.Join("; ", errs)}");
            ConsoleUI.PressAnyKey(); return;
        }

        r.UpdatedAt = DateTime.Now;
        r.Checksum = ChecksumService.Compute(r);

        if (!ConsoleUI.Confirm("Save changes?")) { ConsoleUI.Info("Update cancelled."); ConsoleUI.PressAnyKey(); return; }

        if (repo.Update(r))
        {
            ConsoleUI.Success("Record updated successfully.");
            logger.LogUpdate(r.RecordId, $"Plate={r.PlateNumber}, Owner={r.OwnerName}, Service={r.ServiceType}, Cost={r.ServiceCost:F2}, Mechanic={r.Mechanic}");
        }
        else ConsoleUI.Error("Update failed. Check audit log.");
        ConsoleUI.PressAnyKey();
    }

    private void SoftDelete()
    {
        ConsoleUI.PrintHeader("DELETE SERVICE RECORD (Soft)");
        string id = ConsoleUI.Prompt("Enter Record ID to delete");
        var r = repo.GetById(id);

        if (r == null)
        {
            ConsoleUI.Error($"Record '{id}' not found.");
            logger.LogError($"SoftDelete: RecordId='{id}' not found.");
            ConsoleUI.PressAnyKey(); return;
        }
        if (!r.IsActive) { ConsoleUI.Warning("Record is already inactive."); ConsoleUI.PressAnyKey(); return; }

        Preview(r);
        if (!ConsoleUI.Confirm("Mark this record as inactive (soft delete)?"))
        {
            ConsoleUI.Info("Cancelled."); ConsoleUI.PressAnyKey(); return;
        }

        if (repo.SoftDelete(id)) { ConsoleUI.Success("Record marked as inactive."); logger.LogDelete(id, false); }
        else ConsoleUI.Error("Delete failed. Check audit log.");
        ConsoleUI.PressAnyKey();
    }

    private void HardDelete()
    {
        ConsoleUI.PrintHeader("HARD DELETE SERVICE RECORD");
        ConsoleUI.Warning("This permanently removes the record from storage. This cannot be undone.");
        Console.WriteLine();

        string id = ConsoleUI.Prompt("Enter Record ID");
        var r = repo.GetById(id);

        if (r == null) { ConsoleUI.Error($"Record '{id}' not found."); ConsoleUI.PressAnyKey(); return; }

        Preview(r);
        if (!ConsoleUI.Confirm($"PERMANENTLY DELETE record {id}? Type 'y' to confirm"))
        {
            ConsoleUI.Info("Cancelled."); ConsoleUI.PressAnyKey(); return;
        }

        if (repo.HardDelete(id)) { ConsoleUI.Success("Record permanently deleted."); logger.LogDelete(id, true); }
        else ConsoleUI.Error("Hard delete failed. Check audit log.");
        ConsoleUI.PressAnyKey();
    }

    private void ReportsMenu()
    {
        ConsoleUI.PrintHeader("REPORT GENERATION");
        Console.WriteLine("  [1] Summary by Service Type");
        Console.WriteLine("  [2] High-Value Services Report");
        Console.WriteLine("  [3] Mechanic Workload Report");
        Console.WriteLine("  [0] Back");
        Console.Write("  Choice: ");
        switch (Console.ReadLine()?.Trim())
        {
            case "1": reportGen.GenerateSummaryByServiceType(); break;
            case "2":
                string ti = ConsoleUI.Prompt("Cost threshold (PHP) [default: 3000]");
                decimal th = decimal.TryParse(ti, out decimal tv) ? tv : 3000m;
                reportGen.GenerateHighValueServicesReport(th);
                break;
            case "3": reportGen.GenerateMechanicWorkloadReport(); break;
            case "0": return;
            default: ConsoleUI.Error("Invalid choice."); break;
        }
        ConsoleUI.PressAnyKey();
    }

    private void ViewAuditLog()
    {
        ConsoleUI.PrintHeader("RECENT AUDIT LOG (Last 30 Entries)");
        var logs = logger.GetRecentLogs(30);
        if (!logs.Any()) { ConsoleUI.Warning("Audit log is empty."); ConsoleUI.PressAnyKey(); return; }
        foreach (var line in logs)
        {
            Console.ForegroundColor = line.Contains("[ERROR]") ? ConsoleColor.Red
                                    : line.Contains("[WARN]") ? ConsoleColor.Yellow
                                    : line.Contains("[ADD]") ? ConsoleColor.Green
                                    : line.Contains("[DELETE]") ? ConsoleColor.DarkRed
                                    : line.Contains("[UPDATE]") ? ConsoleColor.Cyan
                                    : ConsoleColor.DarkGray;
            Console.WriteLine($"  {line}");
            Console.ResetColor();
        }
        ConsoleUI.PressAnyKey();
    }

    private void Exit()
    {
        logger.Log("EXIT", "User exited the application.");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n  Thank you for using Vehicle Service Records System. Goodbye!\n");
        Console.ResetColor();
    }

    private static void Preview(ServiceRecord r)
    {
        void F(string label, string val, ConsoleColor? color = null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray; Console.Write($"  {label,-12}: "); Console.ResetColor();
            if (color.HasValue) Console.ForegroundColor = color.Value;
            Console.WriteLine(val); Console.ResetColor();
        }
        F("Record ID", r.RecordId);
        F("Plate", r.PlateNumber);
        F("Owner", r.OwnerName);
        F("Service", r.ServiceType);
        F("Cost", $"PHP {r.ServiceCost:N2}");
        F("Mechanic", r.Mechanic);
        F("Created", r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        F("Updated", r.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        F("Status", r.IsActive ? "Active" : "Inactive", r.IsActive ? ConsoleColor.Green : ConsoleColor.Red);
        F("Checksum", r.Checksum);
    }

    private static void Table(List<ServiceRecord> records)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  {"ID",-22} {"Plate",-10} {"Owner",-20} {"Service",-20} {"Cost",10} {"Mechanic",-18} {"Status",-8}");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {new string('\u2500', 96)}");
        Console.ResetColor();
        foreach (var r in records)
        {
            Console.ForegroundColor = r.IsActive ? ConsoleColor.White : ConsoleColor.DarkGray;
            Console.WriteLine($"  {r.RecordId,-22} {r.PlateNumber,-10} {T(r.OwnerName, 18),-20} {T(r.ServiceType, 18),-20} {r.ServiceCost,10:N2} {T(r.Mechanic, 16),-18} {(r.IsActive ? "Active" : "Inactive"),-8}");
            Console.ResetColor();
        }
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n  Total: {records.Count} record(s)");
        Console.ResetColor();
    }

    private static string T(string s, int max) => s.Length <= max ? s : s[..max] + "\u2026";
}
