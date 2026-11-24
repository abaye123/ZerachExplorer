namespace ZerachExplorer.MVVM.Models;

public class BreadcrumbItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsLast { get; set; }
}
