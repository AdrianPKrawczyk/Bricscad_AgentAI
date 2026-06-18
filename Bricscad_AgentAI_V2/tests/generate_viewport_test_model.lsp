;;; generate_viewport_test_model.lsp
;;; Test geometry for ManageViewportsTool.
;;; Load in BricsCAD and run: GEN_VIEWPORT_TEST_MODEL

(defun vpt:pt (x y /)
  (list x y 0.0)
)

(defun vpt:layer (name color /)
  (if (not (tblsearch "LAYER" name))
    (command "_.-LAYER" "_M" name "_C" (itoa color) name "")
    (command "_.-LAYER" "_S" name "")
  )
)

(defun vpt:line (p1 p2 /)
  (command "_.LINE" p1 p2 "")
)

(defun vpt:rect (x1 y1 x2 y2 /)
  (command "_.PLINE"
           (vpt:pt x1 y1)
           (vpt:pt x2 y1)
           (vpt:pt x2 y2)
           (vpt:pt x1 y2)
           "_C")
)

(defun vpt:text (x y h txt /)
  (command "_.TEXT" "_J" "_MC" (vpt:pt x y) h 0 txt)
)

(defun vpt:circle (x y r /)
  (command "_.CIRCLE" (vpt:pt x y) r)
)

(defun vpt:named-view (name x1 y1 x2 y2 /)
  (command "_.ZOOM" "_W" (vpt:pt x1 y1) (vpt:pt x2 y2))
  (command "_.-VIEW" "_S" name)
)

(defun vpt:furniture (x y /)
  (vpt:layer "A-MEBLE" 30)
  (vpt:rect x y (+ x 1400) (+ y 700))
  (vpt:rect (+ x 1800) y (+ x 3200) (+ y 700))
  (vpt:rect x (+ y 1100) (+ x 900) (+ y 2100))
  (vpt:circle (+ x 4300) (+ y 500) 350)
  (vpt:circle (+ x 5100) (+ y 500) 350)
)

(defun vpt:installations (x1 y1 x2 y2 / x)
  (vpt:layer "A-INSTALACJE" 160)
  (vpt:line (vpt:pt (+ x1 900) (+ y1 900)) (vpt:pt (- x2 900) (- y2 900)))
  (vpt:line (vpt:pt (+ x1 900) (- y2 900)) (vpt:pt (- x2 900) (+ y1 900)))
  (setq x (+ x1 2000))
  (while (< x x2)
    (vpt:circle x (+ y1 2200) 180)
    (setq x (+ x 2500))
  )
)

(defun vpt:draw-zone (name x1 y1 x2 y2 color / cx cy w h step x y)
  (setq cx (/ (+ x1 x2) 2.0))
  (setq cy (/ (+ y1 y2) 2.0))
  (setq w (- x2 x1))
  (setq h (- y2 y1))

  (vpt:layer "VPT_TEST_FRAME" color)
  (vpt:rect x1 y1 x2 y2)
  (vpt:rect (+ x1 500) (+ y1 500) (- x2 500) (- y2 500))

  (vpt:layer "VPT_TEST_AXIS" 1)
  (vpt:line (vpt:pt x1 cy) (vpt:pt x2 cy))
  (vpt:line (vpt:pt cx y1) (vpt:pt cx y2))

  (vpt:layer "VPT_TEST_GRID" 8)
  (setq step 1000.0)
  (setq x x1)
  (while (<= x x2)
    (vpt:line (vpt:pt x y1) (vpt:pt x y2))
    (setq x (+ x step))
  )
  (setq y y1)
  (while (<= y y2)
    (vpt:line (vpt:pt x1 y) (vpt:pt x2 y))
    (setq y (+ y step))
  )

  (vpt:layer "VPT_TEST_MARKERS" 3)
  (vpt:circle x1 y1 250)
  (vpt:circle x2 y1 250)
  (vpt:circle x2 y2 250)
  (vpt:circle x1 y2 250)
  (vpt:circle cx cy 450)
  (vpt:text x1 (- y1 450) 300 (strcat name " MIN"))
  (vpt:text x2 (+ y2 450) 300 (strcat name " MAX"))
  (vpt:text cx cy 450 name)

  (vpt:layer "VPT_TEST_NOTES" 2)
  (vpt:text cx (+ y2 900) 350 (strcat name " - zakres modelu: "
                                      (rtos x1 2 0) "," (rtos y1 2 0)
                                      " do "
                                      (rtos x2 2 0) "," (rtos y2 2 0)))

  (vpt:furniture (+ x1 1800) (+ y1 1600))
  (vpt:furniture (+ x1 9200) (+ y1 7200))

  (vpt:layer "A-OPIS" 2)
  (vpt:text (+ x1 4500) (+ y1 900) 320 (strcat name " OPIS DOL"))
  (vpt:text (- x2 4500) (- y2 900) 320 (strcat name " OPIS GORA"))
  (vpt:text cx (+ cy 1300) 280 "Tekst na warstwie A-OPIS - test freeze per viewport")

  (vpt:installations x1 y1 x2 y2)
)

(defun c:GEN_VIEWPORT_TEST_MODEL (/ oldcmdecho)
  (setq oldcmdecho (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)
  (command "_.UCS" "_W")
  (command "_.TILEMODE" 1)

  (vpt:draw-zone "ARKUSZ1_WINDOW_0_0_18000_12500" 0 0 18000 12500 4)
  (vpt:draw-zone "ARKUSZ2_WINDOW_20000_0_38000_12500" 20000 0 38000 12500 5)

  (vpt:layer "VPT_TEST_NOTES" 2)
  (vpt:text 19000 6500 400 "Przerwa miedzy zakresami - test kadrowania rzutni")

  (vpt:named-view "VPT_ARKUSZ1_FULL" 0 0 18000 12500)
  (vpt:named-view "VPT_ARKUSZ2_FULL" 20000 0 38000 12500)
  (vpt:named-view "VPT_ARKUSZ1_DETAIL" 3000 2000 9000 6500)
  (vpt:named-view "VPT_ARKUSZ2_DETAIL" 23000 2000 29000 6500)

  (command "_.ZOOM" "_E")
  (setvar "CMDECHO" oldcmdecho)
  (princ "\nUtworzono geometrie testowa dla rzutni.")
  (princ "\nZakres Arkusz1: ModelMinX=0 ModelMinY=0 ModelMaxX=18000 ModelMaxY=12500")
  (princ "\nZakres Arkusz2: ModelMinX=20000 ModelMinY=0 ModelMaxX=38000 ModelMaxY=12500")
  (princ "\nWarstwy testowe: A-MEBLE, A-OPIS, A-INSTALACJE.")
  (princ "\nWidoki nazwane: VPT_ARKUSZ1_FULL, VPT_ARKUSZ2_FULL, VPT_ARKUSZ1_DETAIL, VPT_ARKUSZ2_DETAIL.")
  (princ)
)

(princ "\nZaladowano generate_viewport_test_model.lsp. Uruchom: GEN_VIEWPORT_TEST_MODEL")
(princ)
