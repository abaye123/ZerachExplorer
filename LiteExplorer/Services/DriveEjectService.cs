using LiteExplorer.Helpers.WinAPI;
using System.IO;

namespace LiteExplorer.Services;

public interface IDriveEjectService
{
    bool EjectDrive(string drivePath, out string userMessage);
}

public class DriveEjectService : IDriveEjectService
{
    private readonly IPathPolicy pathPolicy;

    public DriveEjectService(IPathPolicy? pathPolicy = null)
    {
        this.pathPolicy = pathPolicy ?? new PathPolicy();
    }

    public bool EjectDrive(string drivePath, out string userMessage)
    {
        if (string.IsNullOrWhiteSpace(drivePath))
        {
            userMessage = "לא נבחר כונן להוצאה.";
            return false;
        }

        if (!pathPolicy.CanEjectDrive(drivePath, out string reason))
        {
            userMessage = reason;
            return false;
        }

        string root = Path.GetPathRoot(drivePath)?.TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(root))
        {
            userMessage = "אות הכונן אינה חוקית.";
            return false;
        }

        if (Interop.EjectVolume(root, out string error))
        {
            userMessage = $"כונן {root} הוצא בהצלחה. כעת ניתן לנתק את ההתקן בבטחה.";
            return true;
        }

        userMessage = $"לא ניתן להוציא את הכונן {root}. ייתכן שקבצים ממנו פתוחים בתוכנה אחרת ({error}).";
        return false;
    }
}
