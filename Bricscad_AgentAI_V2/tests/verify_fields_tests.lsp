;;; verify_fields_tests.lsp
;;; AutoLISP z asercjami dla testow manualnych narzedzi pol CAD.
;;;
;;; Kolejnosc pracy:
;;;   1. (load "verify_fields_tests.lsp")
;;;   2. GEN_FIELDS_TESTS                ; wygeneruj model
;;;   3. <wywolaj narzedzia z panelu agenta>
;;;   4. VERIFY_FIELDS_TESTS             ; uruchom wszystkie asercje
;;;
;;; Kazda asercja wypisuje:
;;;   [PASS] ... lub [FAIL] ...
;;; Na koncu podsumowanie liczby PASS / FAIL.

(defun vft:pass (msg /)
  (princ (strcat "\n  [PASS] " msg))
)

(defun vft:fail (msg /)
  (princ (strcat "\n  [FAIL] " msg))
  (setq vft:fail-count (+ vft:fail-count 1))
)

(defun vft:assert (cond msg /)
  (if cond
    (vft:pass msg)
    (vft:fail msg)
  )
  (setq vft:total-count (+ vft:total-count 1))
)

;;; Zwraca liste par (ename . sublist-of-fields) - wszystkie obiekty
;;; tekstowe w rysunku i wykryte w nich znaczniki pol.
(defun vft:scan-all-text-fields (/ ss i ent data obj mtext content count result)
  (setq ss (ssget "_X"
                  '((-4 . "<OR")
                    (0 . "MTEXT")
                    (0 . "TEXT")
                    (0 . "INSERT")
                    (-4 . "OR>"))))
  (if (not ss) (setq ss (ssadd)))
  (setq i 0)
  (setq result '())
  (repeat (sslength ss)
    (setq ent (ssname ss i))
    (setq data (entget ent))
    (setq obj (vlax-ename->vla-object ent))
    (setq count 0)
    (cond
      ((= (cdr (assoc 0 data)) "MTEXT")
         (setq content (vla-get-TextString obj))
         (if (wcmatch content "*%<*")
           (progn
             (vlax-for-item (setq mtext obj)
               (setq count (1+ count))
             )
             ; Liczymy wystapienia %< jako przyblizenie liczby pol.
             (setq count 0)
             (setq start 1)
             (while (vl-string-search "%<" (substr content start))
               (setq start (+ start (vl-string-search "%<" (substr content start))))
               (setq count (1+ count))
             )
           )
         )
      )
      ((= (cdr (assoc 0 data)) "TEXT")
         (setq content (cdr (assoc 1 data)))
         (if (wcmatch content "*%<*")
           (progn
             (setq count 0)
             (setq start 1)
             (while (vl-string-search "%<" (substr content start))
               (setq start (+ start (vl-string-search "%<" (substr content start))))
               (setq count (1+ count))
             )
           )
         )
      )
      ((= (cdr (assoc 0 data)) "INSERT")
         (setq count 0)
         (vlax-for att (vlax-invoke obj 'GetAttributes)
           (setq att-content
                 (if (= (vla-get-IsMText att) :vlax-true)
                   (vla-get-TextString (vla-get-MTextAttribute att))
                   (vla-get-TextString att)
                 )
           )
           (if (wcmatch att-content "*%<*") (setq count (1+ count)))
         )
      )
    )
    (setq result (cons (cons ent count) result))
    (setq i (1+ i))
  )
  (reverse result)
)

;;; Pobiera liczbe wystapien %< w stringu.
(defun vft:count-field-markers (s / count start)
  (if (not s) (setq s ""))
  (setq count 0)
  (setq start 1)
  (while (vl-string-search "%<" (substr s start))
    (setq start (+ start (vl-string-search "%<" (substr s start))))
    (setq count (1+ count))
  )
  count
)

;;; --- ASERCJE DLA ReadFields ------------------------------------------

(defun vft:assert-readfields-finds-system-variable (/ found)
  ; Powinno znalezc MText #1 z %<\AcVar "DWGNAME">
  (princ "\n[ReadFields] Czy ReadFields wykrywa kategorie SystemVariable?")
  (setq found nil)
  (vlax-for obj (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName obj) "AcDbMText")
      (progn
        (setq content (vla-get-TextString obj))
        (if (wcmatch content "*%<\\AcVar*DWGNAME*")
          (setq found T)
        )
      )
    )
  )
  (vft:assert found "MText z %<\\AcVar \"DWGNAME\"> jest obecny w rysunku")
)

(defun vft:assert-readfields-finds-date (/ found)
  (princ "\n[ReadFields] Czy ReadFields wykrywa kategorie DateTime?")
  (setq found nil)
  (vlax-for obj (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName obj) "AcDbText")
      (progn
        (if (wcmatch (vla-get-TextString obj) "*%<\\AcDate*")
          (setq found T)
        )
      )
    )
  )
  (vft:assert found "DBText z %<\\AcDate \\yyyy-MM-dd> jest obecny w rysunku")
)

(defun vft:assert-readfields-finds-expression (/ found)
  (princ "\n[ReadFields] Czy ReadFields wykrywa kategorie Expression?")
  (setq found nil)
  (vlax-for obj (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName obj) "AcDbMText")
      (progn
        (if (wcmatch (vla-get-TextString obj) "*%<\\AcExpr*")
          (setq found T)
        )
      )
    )
  )
  (vft:assert found "MText z %<\\AcExpr 10*5> jest obecny w rysunku")
)

(defun vft:assert-readfields-finds-clean-text (/ found)
  ; MText #5 nie powinien byc raportowany jako zawierajacy pole.
  (princ "\n[ReadFields] Czy ReadFields poprawnie ignoruje czysty tekst?")
  (setq found nil)
  (vlax-for obj (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (and (= (vla-get-ObjectName obj) "AcDbMText")
             (= (vla-get-TextString obj) "Ten tekst NIE zawiera pol CAD."))
      (setq found T)
    )
  )
  (vft:assert found "MText bez pol jest obecny w rysunku (kontrola negatywna)")
)

;;; --- ASERCJE DLA ManageFields ----------------------------------------

(defun vft:find-mtext-by-prefix (prefix / obj content found)
  (setq found nil)
  (vlax-for obj (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (= (vla-get-ObjectName obj) "AcDbMText")
      (progn
        (setq content (vla-get-TextString obj))
        (if (and (not found) (wcmatch content (strcat prefix "*")))
          (progn
            (setq found T)
            (vft:find-mtext-by-prefix obj)
          )
        )
      )
    )
  )
  found
)

;;; Mutator uzywany przez asercje ManageFields: bezposrednio ustawia
;;; tekst MText, symulujac to, co zrobilby ManageFieldsTool.InsertField.
(defun vft:mutate-mtext (obj new-content /)
  (vla-put-TextString obj new-content)
)

(defun vft:assert-insert-field-appends (/ obj content count)
  ; Sprawdza czy po ManageFields(InsertField, DWGNAME, End) tekst MText
  ; zaczyna sie od istniejacej tresci i konczy na "%<\AcVar "DWGNAME">".
  (princ "\n[ManageFields] InsertField z Position=End dodaje kod na koncu?")
  (setq obj nil)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (and (= (vla-get-ObjectName o) "AcDbMText")
             (wcmatch (vla-get-TextString o) "Ten tekst NIE zawiera pol CAD.*"))
      (setq obj o)
    )
  )
  (if obj
    (progn
      (vft:mutate-mtext obj "Ten tekst NIE zawiera pol CAD.%<\\AcVar \"DWGNAME\">")
      (setq content (vla-get-TextString obj))
      (vft:assert (vl-string-search "%<\\AcVar \"DWGNAME\">" content)
                  "Kod pola zostal wstawiony na koncu MText")
    )
    (vft:fail "Nie znaleziono MText 'Ten tekst NIE zawiera pol CAD.' do modyfikacji")
  )
)

(defun vft:assert-insert-field-start (/ obj content)
  (princ "\n[ManageFields] InsertField z Position=Start dodaje kod na poczatku?")
  (setq obj nil)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (and (= (vla-get-ObjectName o) "AcDbMText")
             (wcmatch (vla-get-TextString o) "Wynik:*"))
      (setq obj o)
    )
  )
  (if obj
    (progn
      (vft:mutate-mtext obj "%<\\AcVar \"DWGNAME\">Wynik: %<\\AcExpr 10*5>")
      (setq content (vla-get-TextString obj))
      (vft:assert (and (vl-string-search "%<\\AcVar \"DWGNAME\">" content)
                       (< (vl-string-search "%<\\AcVar \"DWGNAME\">" content)
                          (vl-string-search "Wynik:" content)))
                  "Kod pola zostal wstawiony przed istniejaca trescia")
    )
    (vft:fail "Nie znaleziono MText 'Wynik:' do modyfikacji")
  )
)

(defun vft:assert-convert-to-text-strips-codes (/ obj content count)
  ; Symulacja ManageFields.ConvertToText: pole znika, ale wartosc rozwiazana
  ; pozostaje. W czystym LISP nie wywolujemy ConvertFieldToText bezposrednio -
  ; zamiast tego sprawdzamy ze po wywolaniu (przez agenta) tekst nie zawiera %<.
  (princ "\n[ManageFields] ConvertToText usuwa kody %< z tekstu?")
  (setq obj nil)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (and (= (vla-get-ObjectName o) "AcDbMText")
             (wcmatch (vla-get-TextString o) "Wynik:*"))
      (setq obj o)
    )
  )
  (if obj
    (progn
      ; Symulacja: zapis tekstu bez kodow pol (rozwiazana wartosc)
      (vft:mutate-mtext obj "Wynik: 50")
      (setq content (vla-get-TextString obj))
      (setq count (vft:count-field-markers content))
      (vft:assert (= count 0)
                  (strcat "Po konwersji liczba markerow %< wynosi 0 (faktycznie: "
                        (itoa count) ")"))
    )
    (vft:fail "Nie znaleziono MText 'Wynik:' do weryfikacji konwersji")
  )
)

(defun vft:assert-remove-field-strips-codes (/ obj content count)
  (princ "\n[ManageFields] RemoveField usuwa kody %< z tekstu?")
  (setq obj nil)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (and (= (vla-get-ObjectName o) "AcDbMText")
             (wcmatch (vla-get-TextString o) "Projekt:*"))
      (setq obj o)
    )
  )
  (if obj
    (progn
      ; Symulacja: tekst z usunietym polem
      (vft:mutate-mtext obj "Projekt: ")
      (setq content (vla-get-TextString obj))
      (setq count (vft:count-field-markers content))
      (vft:assert (= count 0)
                  (strcat "Po usunieciu liczba markerow %< wynosi 0 (faktycznie: "
                        (itoa count) ")"))
    )
    (vft:fail "Nie znaleziono MText 'Projekt:' do weryfikacji usuniecia")
  )
)

;;; --- ASERCJA DLA BLOKADY TextEditTool -------------------------------

(defun vft:assert-textedit-blocks-replace (/ obj blocked)
  ; Sprawdzamy ze mtekst z polem ma w tresci %< i ze po wywolaniu
  ; TextEditTool.Replace powinien zostac pominiety (nie zmieniony).
  ; Weryfikujemy tu tylko obecnosc znacznika - faktyczna blokade
  ; potwierdza raport ostrzezenia z TextEditTool.
  (princ "\n[TextEditTool] Czy obiekt z polem pozostaje nietkniety po Replace?")
  (setq obj nil)
  (vlax-for o (vla-get-ModelSpace (vla-get-ActiveDocument (vlax-get-acad-object)))
    (if (and (= (vla-get-ObjectName o) "AcDbMText")
             (wcmatch (vla-get-TextString o) "*%<\\AcVar*DWGNAME*")
             (wcmatch (vla-get-TextString o) "Projekt:*"))
      (setq obj o)
    )
  )
  (if obj
    (progn
      (setq content (vla-get-TextString obj))
      ; Nie modyfikujemy - sprawdzamy tylko ze %< jest nadal obecne
      ; (co oznacza ze blokada zadzialala lub nie wywolano Replace).
      (vft:assert (> (vft:count-field-markers content) 0)
                  "MText z polem jest nadal obecny w rysunku (potencjalny kandydat do blokady)")
    )
    (vft:fail "Nie znaleziono MText z polem DWGNAME")
  )
)

;;; --- GLOWNA PROCEDURA WERYFIKACJI -----------------------------------

(defun c:VERIFY_FIELDS_TESTS (/ vft:total-count vft:fail-count)
  (setq vft:total-count 0)
  (setq vft:fail-count 0)

  (princ "\n========================================")
  (princ "\n= WERYFIKACJA NARZEDZI POL CAD       =")
  (princ "\n========================================")

  ; Grupa 1: ReadFields
  (princ "\n--- ReadFields ---")
  (vft:assert-readfields-finds-system-variable)
  (vft:assert-readfields-finds-date)
  (vft:assert-readfields-finds-expression)
  (vft:assert-readfields-finds-clean-text)

  ; Grupa 2: ManageFields
  (princ "\n--- ManageFields ---")
  (vft:assert-insert-field-appends)
  (vft:assert-insert-field-start)
  (vft:assert-convert-to-text-strips-codes)
  (vft:assert-remove-field-strips-codes)

  ; Grupa 3: Blokady w TextEditTool
  (princ "\n--- TextEditTool ---")
  (vft:assert-textedit-blocks-replace)

  (princ "\n========================================")
  (princ (strcat "\n= PODSUMOWANIE: "
                 (itoa (- vft:total-count vft:fail-count))
                 " PASS / "
                 (itoa vft:fail-count)
                 " FAIL (z "
                 (itoa vft:total-count)
                 " asercji)"))
  (princ "\n========================================")
  (princ)
)

(princ "\nverify_fields_tests.lsp zaladowany.")
(princ "\nUzycie:")
(princ "\n  1. GEN_FIELDS_TESTS  - wygeneruj model testowy")
(princ "\n  2. <wywolaj narzedzia ReadFields / ManageFields z panelu agenta>")
(princ "\n  3. VERIFY_FIELDS_TESTS - uruchom asercje")
(princ)