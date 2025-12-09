using System.IO;

namespace systembuilderGUI.Models;

public class WSL
{
    static public string BuildWslPath(string path)
    {
        var root = Path.GetPathRoot(path);
        var rootName = root.Replace(":\\", "").ToLower();
        var wslPath = path.Replace(root, $"/mnt/{rootName}/").Replace("\\","/");
        return wslPath;
    }
}