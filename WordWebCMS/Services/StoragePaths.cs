namespace WordWebCMS.Services;

public class StoragePaths(string root)
{
    public string Root { get; } = root;
    public string Media => Path.Combine(Root, "media");
    public string Themes => Path.Combine(Root, "theme");
}
