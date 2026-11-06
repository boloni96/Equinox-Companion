using System.Text.RegularExpressions;
namespace EquinoxCompanion;
public static class LoginBackupReminderPolicy
{
    public static bool Matches(IEnumerable<string> texts)
    {
        var text=Regex.Replace(string.Join(" ",texts),@"\s+"," ").Trim();
        return text.Contains("System Configuration Backup",StringComparison.Ordinal)&&
            Regex.IsMatch(text,@"The last backup of client data was created [0-9]+ days ago\.")&&
            text.Contains("Creating routine backups is recommended.",StringComparison.Ordinal)&&
            text.Contains("Notify after a set time has elapsed since the last backup.",StringComparison.Ordinal);
    }
}
