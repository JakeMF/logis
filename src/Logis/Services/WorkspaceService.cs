namespace Logis.Services;

public class WorkspaceService
{
    public string ReadFile(string path)
    {
        return File.ReadAllText(path);
    }
}
