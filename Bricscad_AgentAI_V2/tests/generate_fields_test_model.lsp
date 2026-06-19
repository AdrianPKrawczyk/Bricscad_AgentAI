;;; generate_fields_test_model.lsp
;;; AutoLISP do generowania rysunku testowego dla narzedzi pol CAD (ReadFields, ManageFields).
;;; Uruchomienie: GEN_FIELDS_TESTS
;;;
;;; Tworzy model z roznorodnymi obiektami tekstowymi zawierajacymi pola DWG
;;; w roznych wariantach:
;;;   - MText z polem %<\AcVar "DWGNAME">
;;;   - DBText z polem %<\AcDate \yyyy>
;;;   - MText z polem %<\AcExpr 1+1>
;;;   - MText z wieloma polami obok siebie
;;;   - MText bez pol (do kontroli negatywnej)
;;;   - Definicja bloku z atrybutem MText zawierajacym pole
;;;   - Definicja bloku z atrybutem DBText bez pol
;;;
;;; Po wywolaniu wszystkie obiekty trafiaja do warstwy "Bielik_Pola",
;;; a definicje blokow maja prefix "Bielik_Pole_*".

(defun c:GEN_FIELDS_TESTS (/ oldcmdecho oldclayer p1 p2 p3 p4 p5 blkdef attdef1 attref1 attref2)

  (setq oldcmdecho (getvar "CMDECHO"))
  (setq oldclayer (getvar "CLAYER"))
  (setvar "CMDECHO" 0)

  (princ "\n=== Generowanie modelu testowego dla narzedzi pol CAD ===")

  ;--- 1. Warstwa docelowa ----------------------------------------------
  (command "_.LAYER" "_M" "Bielik_Pola" "_C" "3" "" "")
  (command "_.LAYER" "_S" "Bielik_Pola" "")

  ;--- 2. MText z polem zmiennej systemowej ---------------------------
  (princ "\n- MText #1: pole %<\\AcVar \"DWGNAME\">...")
  (command "_.MTEXT"
           "0,0"
           "_H" "2.5"
           "_W" "80"
           "Projekt: %<\\AcVar \"DWGNAME\">"
           "")

  ;--- 3. DBText z polem daty -----------------------------------------
  (princ "\n- DBText #2: pole %<\\AcDate \\yyyy-MM-dd>...")
  (command "_.TEXT"
           "0,-15"
           "_H" "2.5"
           "0"
           "Data: %<\\AcDate \\yyyy-MM-dd>")

  ;--- 4. MText z wyrazeniem RPN --------------------------------------
  (princ "\n- MText #3: pole %<\\AcExpr 10*5>...")
  (command "_.MTEXT"
           "0,-30"
           "_H" "2.5"
           "_W" "60"
           "Wynik: %<\\AcExpr 10*5>"
           "")

  ;--- 5. MText z wieloma polami obok siebie --------------------------
  (princ "\n- MText #4: wiele pol obok siebie...")
  (command "_.MTEXT"
           "0,-45"
           "_H" "2.5"
           "_W" "100"
           "Poczatek: %<\\AcVar \"DWGNAME\"> | Srodek: %<\\AcDate \\HH:mm> | Koniec"
           "")

  ;--- 6. MText bez pol (kontrola negatywna) --------------------------
  (princ "\n- MText #5: czysty tekst bez pol...")
  (command "_.MTEXT"
           "0,-60"
           "_H" "2.5"
           "_W" "60"
           "Ten tekst NIE zawiera pol CAD."
           "")

  ;--- 7. Definicja bloku z atrybutem MText z polem ------------------
  (princ "\n- Blok Bielik_Pole_Tytul z atrybutem MText z polem...")

  ; Najpierw narysuj geometrie bloku
  (command "_.RECTANG" "200,0" "280,30")

  ; Zdefiniuj blok z atrybutem MText zawierajacym pole
  (command "_.BLOCK"
           "Bielik_Pole_Tytul"
           "200,0"
           ; atrybut MText z polem (wieloliniowy, moze zawierac kod pola)
           "_.ATTDEF"
           "_Tag" "TYTUL"
           "_Prompt" "Podaj tytul"
           "_MText" "%%U"
           "Tytul projektu: %<\\AcVar \"DWGNAME\">"
           "_InsertionPoint" "210,15"
           "_Height" "2.5"
           ""
           ; obrys z poprzedniego RECTANG
           "_.ENDBLK" "")

  ;--- 8. Definicja bloku z atrybutem DBText bez pol ------------------
  (princ "\n- Blok Bielik_Pole_Etykieta z atrybutem DBText bez pol...")

  (command "_.CIRCLE" "300,15" "10")
  (command "_.BLOCK"
           "Bielik_Pole_Etykieta"
           "300,15"
           "_.ATTDEF"
           "_Tag" "NUMER"
           "_Prompt" "Podaj numer"
           "Stala etykieta"
           "_InsertionPoint" "300,5"
           "_Height" "2.0"
           ""
           "_.ENDBLK" "")

  ;--- 9. Wstaw instancje blokow --------------------------------------
  (princ "\n- Wstawianie instancji blokow...")
  (command "_.INSERT" "Bielik_Pole_Tytul" "200,-100" "1" "1" "0")
  (command "_.INSERT" "Bielik_Pole_Tytul" "200,-150" "1" "1" "0")
  (command "_.INSERT" "Bielik_Pole_Etykieta" "350,15" "1" "1" "0")

  ;--- 10. Selekcja wszystkich obiektow w modelu ----------------------
  (command "_.ZOOM" "_E")
  (command "_.SELECT" "_All" "")

  (setvar "CMDECHO" oldcmdecho)
  (command "_.LAYER" "_S" oldclayer "")

  (princ "\n=== Gotowe. Zaznaczono wszystkie obiekty w rysunku. ===")
  (princ)
)