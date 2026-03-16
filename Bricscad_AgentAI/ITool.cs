// Plik: ITool.cs
namespace BricsCAD_Agent
{
    public interface ITool
    {
        string ActionTag { get; }      // np. [ACTION:RED_LINES]
        string Description { get; }    // Co to narzędzie robi (dla AI)
        void Execute(Bricscad.ApplicationServices.Document doc); // Logika w CAD
    }
}