using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public interface ITool
    {
        string ActionTag { get; }
        string Description { get; }

        // Zmiana z void na string!
        string Execute(Document doc, string args);
        string Execute(Document doc);
    }
}