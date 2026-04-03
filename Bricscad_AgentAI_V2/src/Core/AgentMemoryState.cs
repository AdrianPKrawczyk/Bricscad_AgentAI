using System.Collections.Generic;
using System.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Przechowuje pamięć podręczną operacji dokonywanych w obrębie sesji Agenta AI.
    /// Zapobiega kolizjom stanu podczas asynchronicznych modyfikacji i pozwala
    /// na składowanie wskaźników referencyjnych dla narzędzi.
    /// </summary>
    public static class AgentMemoryState
    {
        private static ObjectId[] _activeSelection = new ObjectId[0];

        /// <summary>
        /// Zbiór referencji do aktualnie wyizolowanych (lub zaznaczonych) obiektów w dokumencie.
        /// </summary>
        public static ObjectId[] ActiveSelection => _activeSelection;

        /// <summary>
        /// Całkowicie zastępuje aktualną pamięć zaznaczenia nową tablicą ID.
        /// </summary>
        public static void Update(ObjectId[] ids)
        {
            _activeSelection = ids ?? new ObjectId[0];
        }

        /// <summary>
        /// Dołącza nowe IDki do istniejącego zbioru zaznaczenia, upewniając się, że wartości są unikalne.
        /// </summary>
        public static void Append(ObjectId[] ids)
        {
            if (ids == null || ids.Length == 0) return;
            var currentList = _activeSelection.ToList();
            currentList.AddRange(ids);
            _activeSelection = currentList.Distinct().ToArray();
        }

        /// <summary>
        /// Usuwa wybrane IDki z aktualnego zbioru zaznaczenia.
        /// </summary>
        public static void Remove(ObjectId[] ids)
        {
            if (ids == null || ids.Length == 0) return;
            var currentList = _activeSelection.ToList();
            var toRemove = new HashSet<ObjectId>(ids);
            currentList.RemoveAll(id => toRemove.Contains(id));
            _activeSelection = currentList.ToArray();
        }

        /// <summary>
        /// Czyści całkowicie pamięć zestawu zaznaczenia.
        /// </summary>
        public static void Clear()
        {
            _activeSelection = new ObjectId[0];
        }
    }
}
