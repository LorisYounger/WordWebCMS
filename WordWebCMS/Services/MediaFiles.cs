using WordWebCMS.Models;

namespace WordWebCMS.Services;

public static class MediaFiles
{
    public static bool ValidName(string name) => name.Length is > 0 and <= 500 && !name.Any(char.IsControl) &&
        !name.Any(c => "<>:\\|?*%#\"".Contains(c)) && name.Split('/').All(s => s.Length is > 0 and <= 255 && s is not ("." or "..") && !s.EndsWith('.') && !s.EndsWith(' '));

    public static string Url(Media media) => "/media/" + string.Join('/', (media.LinkName ?? media.FileName).Split('/').Select(Uri.EscapeDataString));

    public static string PathFor(Media media, StoragePaths storage, string webRoot)
    {
        if (!ValidName(media.FileName)) throw new InvalidDataException("Invalid stored media path.");
        var root = Path.GetFullPath(media.IsLegacyUpload ? Path.Combine(webRoot, "wp-content", "uploads") : storage.Media);
        var path = Path.GetFullPath(Path.Combine(root, media.FileName));
        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Media path escapes its storage directory.");
        return path;
    }
}
