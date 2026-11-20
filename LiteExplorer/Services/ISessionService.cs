using System;

namespace LiteExplorer.Services;

public interface ISessionService
{
    event EventHandler SessionReset;
    void ResetSession();
    void WipeClipboard();
    void CleanTempFiles();
}
