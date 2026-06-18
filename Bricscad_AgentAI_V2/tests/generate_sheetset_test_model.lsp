;;; generate_sheetset_test_model.lsp
;;; Generates test drawing files with layouts for testing Sheet Set Manager tools.
;;; Load in BricsCAD and run: GEN_SHEETSET_TEST_MODEL

(defun c:GEN_SHEETSET_TEST_MODEL (/ testDir oldcmdecho)
  (setq oldcmdecho (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)

  (setq testDir "C:\\SheetSetTests\\")
  (if (not (vl-file-directory-p testDir))
    (vl-mkdir testDir)
  )

  ;; Try to delete default layouts so they don't pollute the file
  (vl-catch-all-apply 'vla-delete (list (vla-item (vla-get-layouts (vla-get-activedocument (vlax-get-acad-object))) "Model"))) ; Model cannot be deleted, but just in case
  (vl-catch-all-apply 'command (list "_.LAYOUT" "_Delete" "Layout1"))
  (vl-catch-all-apply 'command (list "_.LAYOUT" "_Delete" "Layout2"))
  (vl-catch-all-apply 'command (list "_.LAYOUT" "_Delete" "Arkusz1"))
  (vl-catch-all-apply 'command (list "_.LAYOUT" "_Delete" "Arkusz2"))

  ;; Create Architecture layouts
  (command "_.LAYOUT" "_New" "A1_Rzut_Parteru")
  (command "_.LAYOUT" "_New" "A2_Rzut_Pietra")
  (command "_.SAVEAS" "2018" (strcat testDir "Architektura.dwg"))

  ;; Delete Arch layouts
  (command "_.LAYOUT" "_Delete" "A1_Rzut_Parteru")
  (command "_.LAYOUT" "_Delete" "A2_Rzut_Pietra")

  ;; Create Structure layouts
  (command "_.LAYOUT" "_New" "K1_Zbrojenie")
  (command "_.LAYOUT" "_New" "K2_Fundamenty")
  (command "_.SAVEAS" "2018" (strcat testDir "Konstrukcja.dwg"))

  ;; Clean up
  (command "_.LAYOUT" "_Delete" "K1_Zbrojenie")
  (command "_.LAYOUT" "_Delete" "K2_Fundamenty")
  (command "_.LAYOUT" "_New" "Arkusz1")
  
  (setvar "CMDECHO" oldcmdecho)

  (princ (strcat "\nZakończono. Pliki testowe wygenerowane w folderze: " testDir))
  (princ "\nZnajdują się tam: Architektura.dwg oraz Konstrukcja.dwg z layoutami.")
  (princ "\nUżyj Agenta aby utworzyć w tym folderze zestaw arkuszy (Sheet Set) .dst i zaimportować te arkusze.")
  (princ)
)

(princ "\nZaładowano generate_sheetset_test_model.lsp. Uruchom: GEN_SHEETSET_TEST_MODEL")
(princ)
