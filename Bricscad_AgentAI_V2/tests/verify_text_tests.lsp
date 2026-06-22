;;; verify_text_tests.lsp
;;; AutoLISP z asercjami dla testow manualnych profilu CadTextProfile.
;;;
;;; Kolejnosc pracy:
;;;   1. (load "verify_text_tests.lsp")
;;;   2. GEN_TEXT_TESTS                ; wygeneruj model
;;;   3. <wywolaj prompty z CadTextProfileManualTests.md>
;;;   4. VERIFY_TEXT_TESTS             ; uruchom wszystkie asercje
;;;
;;; Kazda asercja wypisuje:
;;;   [PASS] ... lub [FAIL] ...
;;; Na koncu podsumowanie liczby PASS / FAIL.

(defun vtt:pass (msg /)
  (princ (strcat "\n  [PASS] " msg))
)

(defun vtt:fail (msg /)
  (princ (strcat "\n  [FAIL] " msg))
  (setq vtt:fail-count (+ vtt:fail-count 1))
)

(defun vtt:assert (cond msg /)
  (if cond
    (vtt:pass msg)
    (vtt:fail msg)
  )
  (setq vtt:total-count (+ vtt:total-count 1))
)

;;; Zwraca liste par (ename . text) - wszystkie obiekty tekstowe w rysunku.
(defun vtt:scan-all-text (/ ss i ent data obj content result)
  (setq ss (ssget "_X"
                  '((-4 . "<OR")
                    (0 . "MTEXT")
                    (0 . "TEXT")
                    (-4 . "OR>"))))
  (if (not ss) (setq ss (ssadd)))
  (setq i 0)
  (setq result '())
  (repeat (sslength ss)
    (setq ent (ssname ss i))
    (setq data (entget ent))
    (setq obj (vlax-ename->vla-object ent))
    (cond
      ((= (cdr (assoc 0 data)) "MTEXT")
         (setq content (vla-get-TextString obj))
      )
      ((= (cdr (assoc 0 data)) "TEXT")
         (setq content (cdr (assoc 1 data)))
      )
      (T (setq content ""))
    )
    (setq result (cons (cons ent content) result))
    (setq i (+ i 1))
  )
  (reverse result)
)

;;; --- ASERCJE DLA TextEditTool.Replace -------------------------------

(defun vtt:assert-replace-removed-stary (/ count)
  ; Sprawdza ze po Replace "[STARY]" -> "[NOWY]" na 30 MText
  ; zadna nie zawiera juz tekstu "[STARY]"
  (princ "\n[TextEditTool] Czy Replace usunal wszystkie [STARY] z 30 MText?")
  (setq count 0)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName o) "AcDbMText")
      (if (wcmatch (vla-get-TextString o) "*\\[STARY]*")
        (setq count (+ count 1))
      )
    )
  )
  (vtt:assert (= count 0)
              (strcat "Liczba MText z [STARY] po Replace: " (itoa count) " (powinno byc 0)"))
)

(defun vtt:assert-replace-added-nowy (/ count)
  ; Sprawdza ze 30 MText ma teraz "[NOWY]"
  (princ "\n[TextEditTool] Czy Replace wstawil [NOWY] do 30 MText?")
  (setq count 0)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName o) "AcDbMText")
      (if (wcmatch (vla-get-TextString o) "*\\[NOWY]*")
        (setq count (+ count 1))
      )
    )
  )
  (vtt:assert (>= count 30)
              (strcat "Liczba MText z [NOWY] po Replace: " (itoa count) " (powinno byc >= 30)"))
)

;;; --- ASERCJE DLA TextEditTool.Append --------------------------------

(defun vtt:assert-append-added-suffix (/ count)
  ; Sprawdza ze Append "_2026" do 30 opisow zadzialal
  (princ "\n[TextEditTool] Czy Append _2026 dodany do wszystkich 30 opisow?")
  (setq count 0)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName o) "AcDbMText")
      (if (wcmatch (vla-get-TextString o) "*_2026")
        (setq count (+ count 1))
      )
    )
  )
  (vtt:assert (>= count 30)
              (strcat "Liczba MText z _2026: " (itoa count) " (powinno byc >= 30)"))
)

;;; --- ASERCJE DLA TextEditTool.FormatHighlight -----------------------

(defun vtt:assert-highlight-added-rtf (/ count)
  ; Sprawdza ze FormatHighlight na MText dodalo kod RTF \\C
  (princ "\n[TextEditTool] Czy FormatHighlight dodal kod RTF \\C do MText?")
  (setq count 0)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName o) "AcDbMText")
      (if (wcmatch (vla-get-TextString o) "*\\C*")
        (setq count (+ count 1))
      )
    )
  )
  (vtt:assert (> count 0)
              (strcat "Liczba MText z kodem \\C: " (itoa count) " (powinno byc > 0)"))
)

;;; --- ASERCJE DLA ReadTextSampleTool ---------------------------------

(defun vtt:assert-text-sample-found-stary (/ count)
  ; Sprawdza ze wzorzec [STARy] (mala litera y) jest w modelu -
  ; LLM powinien to zobaczyc przez ReadTextSampleTool zanim zrobi Replace
  (princ "\n[ReadTextSampleTool] Czy [STARy] zostalo zapisane do pamieci i widoczne?")
  (setq count 0)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName o) "AcDbText")
      (if (wcmatch (vla-get-TextString o) "*\\[STARy]*")
        (setq count (+ count 1))
      )
    )
  )
  (vtt:assert (>= count 5)
              (strcat "Liczba DBText z [STARy]: " (itoa count) " (powinno byc >= 5)"))
)

;;; --- ASERCJE DLA ManageFields.InsertField ---------------------------

(defun vtt:assert-insert-field-appended-date (/ found)
  ; Sprawdza ze ManageFields.InsertField z data wstawil kod %<\AcDate
  ; na koncu MText "Specjalny opis" (ten z XData)
  (princ "\n[ManageFields] Czy InsertField %<\\AcDate wstawil kod na koncu MText?")
  (setq found nil)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName o) "AcDbMText")
      (progn
        (setq content (vla-get-TextString o))
        (if (and (wcmatch content "Specjalny opis*")
                 (wcmatch content "*%<\\AcDate*"))
          (setq found T)
        )
      )
    )
  )
  (vtt:assert found "MText 'Specjalny opis' ma na koncu kod %<\\AcDate")
)

;;; --- ASERCJE DLA OCHRONY POL ---------------------------------------

(defun vtt:count-field-markers (s / count start)
  (if (not s) (setq s ""))
  (setq count 0)
  (setq start 1)
  (while (vl-string-search "%<" (substr s start))
    (setq start (+ start (vl-string-search "%<" (substr s start))))
    (setq count (+ count 1))
  )
  count
)

(defun vtt:assert-field-preserved-by-textedit-replace (/ obj content count)
  ; Sprawdza ze MText #3 z %<\AcVar "DWGNAME"> NIE zostal zmieniony
  ; przez TextEditTool.Replace (powinien byc zablokowany)
  (princ "\n[TextEditTool] Czy pole %<\\AcVar \"DWGNAME\"> w MText #3 jest nienaruszone?")
  (setq obj nil)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (and (= (vla-get-ObjectName o) "AcDbMText")
             (wcmatch (vla-get-TextString o) "*%<\\AcVar*DWGNAME*"))
      (setq obj o)
    )
  )
  (if obj
    (progn
      (setq content (vla-get-TextString obj))
      (setq count (vtt:count-field-markers content))
      (vtt:assert (> count 0)
                  (strcat "MText z polem CAD jest nadal obecny (markery %<: "
                        (itoa count) ")"))
    )
    (vtt:fail "Nie znaleziono MText z polem DWGNAME")
  )
)

;;; --- ASERCJE DLA GRANICY KOMPETENCJI --------------------------------

(defun vtt:assert-dbtext-highlight-skipped (/ count)
  ; Sprawdza ze DBText #1 (czysty tekst) NIE zostal zmodyfikowany
  ; przez FormatHighlight (ktory dziala tylko na MText).
  ; Asercja: tekst "Stara etykieta A" jest nadal obecny (nie zostal zepsuty).
  (princ "\n[TextEditTool] Czy DBText nie zostal zmieniony przez FormatHighlight (brak RTF)?")
  (setq count 0)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName o) "AcDbText")
      (if (= (vla-get-TextString o) "Stara etykieta A")
        (setq count (+ count 1))
      )
    )
  )
  (vtt:assert (>= count 1)
              (strcat "DBText 'Stara etykieta A' nadal istnieje: " (itoa count) " (powinno byc 1)"))
)

;;; --- ASERCJE DLA XDATA KONTEKSTU -----------------------------------

(defun vtt:assert-xdata-on-special-text (/ found data xdata-group)
  ; Sprawdza ze MText "Specjalny opis" ma XData "Bielik_Test" / "Etykieta_36"
  (princ "\n[ReadXData] Czy MText 'Specjalny opis' ma XData Bielik_Test?")
  (setq found nil)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName o) "AcDbMText")
      (if (wcmatch (vla-get-TextString o) "Specjalny opis*")
        (progn
          (setq data (entget (vlax-vla-object->ename o)))
          ; Szukaj grupy -3 (XData) zawierajacej 'Bielik_Test'
          (foreach pair data
            (if (and (= (car pair) -3)
                     (member "Bielik_Test" (cadr pair)))
              (progn
                (setq xdata-group pair)
                (setq found T)
              )
            )
          )
        )
      )
    )
  )
  (vtt:assert found "MText 'Specjalny opis' ma XData aplikacji Bielik_Test")
)

;;; --- GLOWNA PROCEDURA WERYFIKACJI -----------------------------------

(defun c:VERIFY_TEXT_TESTS (/ vtt:total-count vtt:fail-count)
  (setq vtt:total-count 0)
  (setq vtt:fail-count 0)

  (princ "\n========================================")
  (princ "\n= WERYFIKACJA PROFILU CadTextProfile =")
  (princ "\n========================================")

  ; Grupa 1: TextEditTool.Replace
  (princ "\n--- TextEditTool.Replace (z ForeachTool) ---")
  (vtt:assert-replace-removed-stary)
  (vtt:assert-replace-added-nowy)

  ; Grupa 2: TextEditTool.Append
  (princ "\n--- TextEditTool.Append (z ForeachTool) ---")
  (vtt:assert-append-added-suffix)

  ; Grupa 3: TextEditTool.FormatHighlight
  (princ "\n--- TextEditTool.FormatHighlight ---")
  (vtt:assert-highlight-added-rtf)

  ; Grupa 4: ReadTextSampleTool
  (princ "\n--- ReadTextSampleTool ---")
  (vtt:assert-text-sample-found-stary)

  ; Grupa 5: ManageFields.InsertField
  (princ "\n--- ManageFields.InsertField ---")
  (vtt:assert-insert-field-appended-date)

  ; Grupa 6: Ochrona pol
  (princ "\n--- Ochrona pol CAD ---")
  (vtt:assert-field-preserved-by-textedit-replace)

  ; Grupa 7: Granica kompetencji
  (princ "\n--- Granica kompetencji (DBText vs MText) ---")
  (vtt:assert-dbtext-highlight-skipped)

  ; Grupa 8: XData
  (princ "\n--- Kontekst XData ---")
  (vtt:assert-xdata-on-special-text)

  (princ "\n========================================")
  (princ (strcat "\n= PODSUMOWANIE: "
                 (itoa (- vtt:total-count vtt:fail-count))
                 " PASS / "
                 (itoa vtt:fail-count)
                 " FAIL (z "
                 (itoa vtt:total-count)
                 " asercji)"))
  (princ "\n========================================")
  (princ)
)

(princ "\nverify_text_tests.lsp zaladowany.")
(princ "\nUzycie:")
(princ "\n  1. GEN_TEXT_TESTS      - wygeneruj model testowy")
(princ "\n  2. <wywolaj prompty z CadTextProfileManualTests.md>")
(princ "\n  3. VERIFY_TEXT_TESTS   - uruchom asercje")
(princ)
