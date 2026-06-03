;;; AutoLISP do generowania obiektów testowych dla Bielik AI V2 GOLD
;;; Uruchomienie komendą: GEN_BIELIK_TESTS

(defun C:GEN_BIELIK_TESTS ( / old_attreq old_attdia old_cmdecho pl1 pl2 xdata elist newlist g1 g2 a1 a2 )
  (setq old_cmdecho (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)

  (princ "\nGenerowanie obiektów testowych Bielik AI...")

  ;; 1. UTWORZENIE WARSTW TESTOWYCH
  (command "_-LAYER" "_M" "Bielik_Konstrukcja" "_C" "2" "" 
                     "_M" "Bielik_Opisy" "_C" "4" "" 
                     "_M" "Bielik_Wymiary" "_C" "6" "" "")

  ;; 2. OBIEKTY DLA: CadGeometryProfile
  (princ "\n- Rysowanie polilinii i okręgów (dla CadGeometryProfile)...")
  (command "_-LAYER" "_S" "Bielik_Konstrukcja" "")
  
  ;; Rysowanie czerwonych polilinii
  (command "_-COLOR" "1")
  (command "_PLINE" "0,0" "50,0" "50,50" "0,50" "_C")
  (setq pl1 (entlast))

  (command "_PLINE" "10,10" "40,10" "40,40" "10,40" "_C")
  (setq pl2 (entlast))

  ;; Rysowanie żółtych okręgów o średnicach/promieniach
  (command "_-COLOR" "2")
  (command "_CIRCLE" "120,30" "25") ; Promień 25 (Średnica 50)
  (command "_CIRCLE" "180,30" "50") ; Promień 50 (Średnica 100)
  (command "_CIRCLE" "250,30" "75") ; Promień 75 (Średnica 150)

  ;; Rysowanie tekstów na warstwie Bielik_Opisy
  (command "_-LAYER" "_S" "Bielik_Opisy" "")
  (command "_-COLOR" "_BYLAYER")
  (command "_MTEXT" "0,60" "_H" "4.0" "_W" "80" "BIURKO NR 1" "")
  (command "_MTEXT" "100,60" "_H" "4.0" "_W" "80" "BIURKO NR 2" "")
  (command "_MTEXT" "200,60" "_H" "4.0" "_W" "80" "KONTROLER SYSTEMU" "")

  ;; 3. DOŁĄCZENIE METADANYCH XDATA DLA: CadMetadataProfile
  (princ "\n- Dołączanie metadanych XData (dla CadMetadataProfile)...")
  (regapp "BIELIK_APP")
  (if pl1
    (progn
      (setq xdata 
        (list 
          -3 
          (list 
            "BIELIK_APP"
            '(1000 . "Bielik-V2-ID-999")   ; Identyfikator w postaci ciągu znaków
            '(1040 . 123.45)               ; Wartość liczbowa (np. cena / rzędna)
            '(1071 . 45678)                ; Liczba całkowita 32-bitowa
          )
        )
      )
      (setq elist (entget pl1))
      (setq newlist (append elist (list xdata)))
      (entmod newlist)
    )
  )

  ;; 4. TWORZENIE BLOKU Z ATRYBUTAMI DLA: CadBlocksProfile
  (princ "\n- Definiowanie bloku z atrybutami (dla CadBlocksProfile)...")
  
  ;; Sprawdzenie czy blok już istnieje w bazie rysunku
  (if (not (tblsearch "BLOCK" "Bielik_Biurko"))
    (progn
      ;; Zapamiętanie ustawień wprowadzania atrybutów
      (setq old_attreq (getvar "ATTREQ"))
      (setq old_attdia (getvar "ATTDIA"))
      (setvar "ATTREQ" 1)
      (setvar "ATTDIA" 0)

      ;; Rysowanie geometrii bloku na warstwie 0
      (command "_-LAYER" "_S" "0" "")
      (command "_RECTANG" "-10,-5" "10,5")
      (setq g1 (entlast))
      (command "_CIRCLE" "0,-8" "3")
      (setq g2 (entlast))

      ;; Tworzenie definicji atrybutów (Tag, Prompt, Domyślny, Pozycja, Wysokość, Obrót)
      (command "_-ATTDEF" "" "STAN" "Status biurka (Wolne/Zajete)" "Wolne" "-8,-2" "" "2.0" "0")
      (setq a1 (entlast))
      (command "_-ATTDEF" "" "ID" "Identyfikator biurka" "A0" "-8,1" "" "2.0" "0")
      (setq a2 (entlast))

      ;; Stworzenie definicji bloku "Bielik_Biurko" o punkcie bazowym (0,0)
      (command "_-BLOCK" "Bielik_Biurko" "0,0" g1 g2 a1 a2 "")

      ;; Przywrócenie trybów wprowadzania
      (setvar "ATTREQ" old_attreq)
      (setvar "ATTDIA" old_attdia)
    )
  )

  ;; Wstawianie instancji bloku w zadanym układzie
  (princ "\n- Wstawianie instancji bloku Bielik_Biurko...")
  (setq old_attreq (getvar "ATTREQ"))
  (setq old_attdia (getvar "ATTDIA"))
  (setvar "ATTREQ" 1)
  (setvar "ATTDIA" 0)

  ;; Wstawienie 3 biurek na warstwie Bielik_Konstrukcja
  (command "_-LAYER" "_S" "Bielik_Konstrukcja" "")
  (command "_-INSERT" "Bielik_Biurko" "50,150" "1" "1" "0" "Wolne" "A1")
  (command "_-INSERT" "Bielik_Biurko" "150,150" "1" "1" "0" "Zajete" "A2")
  (command "_-INSERT" "Bielik_Biurko" "250,150" "1" "1" "0" "Wolne" "A3")

  (setvar "ATTREQ" old_attreq)
  (setvar "ATTDIA" old_attdia)

  (command "_-LAYER" "_S" "0" "")
  (setvar "CMDECHO" old_cmdecho)
  (princ "\n[SUKCES] Obiekty testowe Bielik AI zostały wygenerowane pomyślnie!")
  (princ "\nUżyj komend w panelu wtyczki do przetestowania profili:")
  (princ "\n1. CadGeometryProfile: 'zaznacz czerwone prostokąty i zmień ich warstwę na Bielik_Wymiary'")
  (princ "\n2. CadBlocksProfile: 'zmień wartość atrybutu STAN na Wolne dla biurka o ID A2'")
  (princ "\n3. CadMetadataProfile: 'wyszukaj obiekt posiadający XData aplikacji BIELIK_APP i odczytaj jego ID'")
  (princ)
)

(princ "\nZaładowano pomyślnie generator testów Bielik AI. Wpisz komendę: GEN_BIELIK_TESTS")
(princ)
