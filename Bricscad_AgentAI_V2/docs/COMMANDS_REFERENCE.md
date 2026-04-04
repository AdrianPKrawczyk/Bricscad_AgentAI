# Katalog Poleceń BricsCAD (V2 GOLD)

Ten dokument zawiera listę poleceń zarejestrowanych w systemie, które można wpisać bezpośrednio w linii komend BricsCAD po załadowaniu biblioteki.

## Instrukcja Ładowania
Aby uruchomić system w BricsCAD:
1. Skompiluj projekt do pliku DLL.
2. W BricsCAD wpisz polecenie **NETLOAD**.
3. Wskaż plik `Bricscad_AgentAI_V2.dll`.

---

## Lista Poleceń

| Polecenie | Opis | Transparentna |
|-----------|------|---------------|
| **AGENT_V2** | Główny punkt wejścia. Otwiera boczny panel (PaletteSet) z interfejsem Bielik AI V2 GOLD. Jeśli panel jest już otwarty, przywraca jego widoczność. | Nie |
| **AI_V2** | Szybkie zapytanie do asystenta. Wyświetla prompt w linii komend BricsCAD, pobiera tekst od użytkownika i przesyła go do aktywnej sesji Agenta. | **Tak** |

---

### Uwagi Techniczne
- Komendy V2 są oddzielone od starszych wersji (V1) przyrostkiem `_V2`, co zapobiega konfliktom przy ładowaniu obu wersji jednocześnie.
- Komenda **AI_V2** została oznaczona jako `CommandFlags.Transparent`, co oznacza, że możesz jej użyć nawet w trakcie trwania innego polecenia (np. podczas rysowania polilinii), aby zapytać asystenta o współrzędne lub parametry.
