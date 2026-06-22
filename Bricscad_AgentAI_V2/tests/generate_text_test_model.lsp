;;; generate_text_test_model.lsp
;;; AutoLISP do generowania rysunku testowego dla profilu CadTextProfile.
;;; Uruchomienie: GEN_TEXT_TESTS
;;;
;;; Tworzy model z roznorodnymi obiektami tekstowymi (DBText i MText)
;;; pokrywajacymi wszystkie scenariusze testowe profilu:
;;;   - DBText czysty (do testu FormatHighlight = ostrzezenie)
;;;   - DBText z polem %<\AcDate \yyyy> (kontrola pojedynczego pola)
;;;   - MText z polem %<\AcVar "DWGNAME"> (kontrola wielu pol)
;;;   - MText wieloliniowy z RTF (\\fArial|b1;\\C1; - test FormatHighlight/ClearFormatting)
;;;   - MText z lista opisow (do testu Append/Prepend/Replace)
;;;   - MText z markerem szukanego tekstu (test Foreach + IterateSelection)
;;;   - MText z XData (kontekst identyfikacji)
;;;   - 30 MText na jednej warstwie (test >10 obiektow - ForeachTool)
;;;   - 5 DBText z blednym wzorcem (do testu ReadTextSampleTool - kontrola FindText)
;;;
;;; Po wywolaniu wszystkie obiekty trafiaja do warstwy "Bielik_TestTekstow".

(defun c:GEN_TEXT_TESTS (/ oldcmdecho oldclayer i)

  (setq oldcmdecho (getvar "CMDECHO"))
  (setq oldclayer (getvar "CLAYER"))
  (setvar "CMDECHO" 0)

  (princ "\n=== Generowanie modelu testowego dla profilu CadTextProfile ===")

  ;--- 1. Warstwa docelowa ----------------------------------------------
  (command "_.LAYER" "_M" "Bielik_TestTekstow" "_C" "5" "" "")
  (command "_.LAYER" "_S" "Bielik_TestTekstow" "")

  ;--- 2. DBText czysty (kontrola negatywna FormatHighlight) ----------
  (princ "\n- DBText #1: czysty tekst (kontrola negatywna)...")
  (command "_.TEXT" "0,0" "_H" "2.5" "0" "Stara etykieta A")

  ;--- 3. DBText z polem daty -----------------------------------------
  (princ "\n- DBText #2: pole %<\\AcDate \\yyyy-MM-dd>...")
  (command "_.TEXT" "0,-8" "_H" "2.5" "0" "Data: %<\\AcDate \\yyyy-MM-dd>")

  ;--- 4. MText z polem zmiennej systemowej ---------------------------
  (princ "\n- MText #3: pole %<\\AcVar \"DWGNAME\">...")
  (command "_.MTEXT" "0,-20" "_H" "2.5" "_W" "60"
           "Projekt: %<\\AcVar \"DWGNAME\">" "")

  ;--- 5. MText wieloliniowy z RTF (test FormatHighlight/ClearFormatting) ---
  (princ "\n- MText #4: wieloliniowy z RTF (\\fArial|b1;\\C1;)...")
  (command "_.MTEXT" "0,-35" "_H" "2.5" "_W" "60"
           "Linia 1 znacznik\\PLinia 2 znacznik\\PLinia 3 znacznik" "")

  ;--- 6. MText z lista opisow (test Replace/Append) -----------------
  (princ "\n- MText #5: lista opisow (test Append/Prepend)...")
  (command "_.MTEXT" "0,-55" "_H" "2.5" "_W" "60"
           "Opis A\\POpis B\\POpis C" "")

  ;--- 7. MText z markerem do Foreach (test Replace na selekcji) -----
  (princ "\n- MText #6 do #35: 30 opisow ze znacznikiem [STARY]...")

  (setq i 0)
  (repeat 30
    (command "_.MTEXT"
             (strcat "200," (rtos (* i -3.5) 2 2))
             "_H" "2.0" "_W" "40"
             (strcat "[STARY] Opis " (itoa (+ i 1))) "")
    (setq i (+ i 1))
  )

  ;--- 8. 5 DBText z literowka w znaczniku (test ReadTextSampleTool) -
  ; Wykonane PRZED sekcja z XData, zeby ewentualny blad XData
  ; nie przerwal dalszych sekcji (vla-put-XData moze nie zadzialac
  ; w niektorych wersjach BricsCADa i przerwac wykonanie).
  (princ "\n- DBText #37 do #41: ze znacznikiem [STARy] (mala/mala litera)...")
  (command "_.TEXT" "500,0"   "_H" "2.0" "0" "[STARy] Notatka 1")
  (command "_.TEXT" "500,-5"  "_H" "2.0" "0" "[STARy] Notatka 2")
  (command "_.TEXT" "500,-10" "_H" "2.0" "0" "[STARy] Notatka 3")
  (command "_.TEXT" "500,-15" "_H" "2.0" "0" "[STARy] Notatka 4")
  (command "_.TEXT" "500,-20" "_H" "2.0" "0" "[STARy] Notatka 5")

  ;--- 9. MText z XData (test FindXDataTool w kontekscie) ------------
  ; Wzor: regapp + entmod z lista asocjacyjna DXF (jak w generate_test_objects.lsp).
  ; Dzieki temu XData dziala stabilnie niezaleznie od wersji BricsCADa.
  (princ "\n- MText #36: z XData 'Bielik_Test' (kontekst identyfikacji)...")
  (command "_.MTEXT" "450,0" "_H" "2.5" "_W" "50"
           "Specjalny opis" "")
  (command "_.REGEN")

  ; Zarejestruj aplikacje XData i dodaj ja do ostatniego MText.
  (princ "\n- Dodawanie XData 'Bielik_Test' do MText 'Specjalny opis'...")
  (vl-load-com)
  (regapp "Bielik_Test")
  (if (setq mtext36 (entlast))
    (progn
      (setq xdata-list
        (list
          -3
          (list
            "Bielik_Test"
            '(1000 . "Kontekst")
            '(1000 . "Etykieta_36")
          )
        )
      )
      (setq elist (entget mtext36))
      (setq newlist (append elist (list xdata-list)))
      (entmod newlist)
    )
    (princ "\n  [OSTRZEZENIE] Nie udalo sie pobrac MText 'Specjalny opis' dla XData.")
  )

  ;--- 10. Selekcja wszystkich -----------------------------------------
  (command "_.ZOOM" "_E")
  (command "_.SELECT" "_All" "")

  (setvar "CMDECHO" oldcmdecho)
  (command "_.LAYER" "_S" oldclayer "")

  (princ "\n=== Gotowe. Wygenerowano:")
  (princ "\n   - 2x DBText (czysty + z polem)")
  (princ "\n   - 3x MText (z polem, z RTF, lista opisow)")
  (princ "\n   - 30x MText ze znacznikiem [STARY] (test Foreach)")
  (princ "\n   - 1x MText z XData 'Bielik_Test'")
  (princ "\n   - 5x DBText ze znacznikiem [STARy] (test ReadTextSampleTool)")
  (princ "\n=== Zaznaczono wszystkie obiekty w rysunku. ===")
  (princ)
)
