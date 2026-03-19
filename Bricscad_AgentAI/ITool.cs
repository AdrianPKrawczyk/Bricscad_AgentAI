using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public interface ITool
    {
        string ActionTag { get; }
        string Description { get; }
        string Execute(Document doc, string jsonArgs);
        string Execute(Document doc);
    }
}