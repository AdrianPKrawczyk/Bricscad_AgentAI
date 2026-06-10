@echo off
REM =============================================================
REM llama-server z MTP dla Gemma 4 IT QAT
REM Wyłącza thinking + używa drafter MTP = szybsza generacja
REM =============================================================

setlocal enabledelayedexpansion

REM Konfiguracja
set "MODEL_DIR=M:\ai\unsloth\gemma-4-31B-it-qat-GGUF"
set "MODEL_FILE=UD-Q4_K_XL"
set "MTP_FILE=mtp-gemma-4-31B-it.gguf"
set "LLAMA_EXE=M:\Progarmy\llama-bench\llama-server.exe"
set "PORT=1235"
set "HOST=0.0.0.0"
set "CTX=8192"
set "NGL=999"
set "BATCH=2048"
set "PARALLEL=4"

REM Sprawdź czy istnieje
if not exist "%LLAMA_EXE%" (
    echo ERROR: llama-server not found at %LLAMA_EXE%
    exit /b 1
)
if not exist "%MODEL_DIR%\%MODEL_FILE%.gguf" (
    echo ERROR: Model not found at %MODEL_DIR%\%MODEL_FILE%.gguf
    exit /b 1
)
if not exist "%MODEL_DIR%\%MTP_FILE%" (
    echo ERROR: MTP drafter not found at %MODEL_DIR%\%MTP_FILE%
    exit /b 1
)

echo.
echo ============================================
echo  Start llama-server z MTP
echo  Model:  %MODEL_DIR%\%MODEL_FILE%.gguf
echo  Drafter: %MODEL_DIR%\%MTP_FILE%
echo  Port:   %PORT%
echo ============================================
echo.

"%LLAMA_EXE%" ^
    --model "%MODEL_DIR%\%MODEL_FILE%.gguf" ^
    --model-draft "%MODEL_DIR%\%MTP_FILE%" ^
    --alias "gemma-4-31b-it-qat-mtp" ^
    --port %PORT% ^
    --host %HOST% ^
    --ctx-size %CTX% ^
    --n-gpu-layers %NGL% ^
    --batch-size %BATCH% ^
    --parallel %PARALLEL% ^
    --flash-attn ^
    --reasoning off ^
    --jinja ^
    --special ^
    --chat-template-kwargs '{"enable_thinking":false}'
