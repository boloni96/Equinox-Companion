using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class BackupReminder449Tests
{
    [ModuleInitializer] internal static void Run()
    {
        string[] prompt=["System Configuration Backup","The last backup of client data was created 90 days ago.\nCreating routine backups is recommended.","Notify after a set time has elapsed since the last backup."];
        if(!LoginBackupReminderPolicy.Matches(prompt)||!LoginBackupReminderPolicy.Matches(prompt.Select(x=>x.Replace("90","121"))))throw new Exception("Recognize only the age reminder, including varying backup ages.");
        foreach(var other in new[]{new[]{"Players in queue: 16.","OK"},new[]{"System Configuration Backup","Restore client settings?","OK"},new[]{"System Configuration Backup","Upload your settings?","OK"},prompt.Skip(1).ToArray(),prompt.Take(2).ToArray()})
            if(LoginBackupReminderPolicy.Matches(other))throw new Exception("Queues, restore/upload prompts and partial matches must not be dismissed.");
        Console.WriteLine("Backup age reminder and unrelated-dialog rejection passed.");
    }
}
