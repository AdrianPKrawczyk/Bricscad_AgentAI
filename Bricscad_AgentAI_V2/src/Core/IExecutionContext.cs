using Bricscad.ApplicationServices;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Abstrakcja środowiska wykonawczego (np. CAD, środowisko plikowe).
    /// Zastępuje bezpośrednie przekazywanie Document doc przez wszystkie warstwy.
    /// </summary>
    public interface IExecutionContext
    {
        Document CadDocument { get; }
        // Tutaj w przyszłości można dodać dostęp do plików, projektów itp.
    }

    public class CadExecutionContext : IExecutionContext
    {
        public Document CadDocument { get; private set; }

        public CadExecutionContext(Document doc)
        {
            CadDocument = doc;
        }
    }
}
