@echo off
REM =============================================================
REM llama-server z MTP dla Gemma 4 26B-A4B IT QAT
REM =============================================================

setlocal enabledelayedexpansion

set "MODEL_DIR=M:\ai\unsloth\gemma-4-26B-A4B-it-qat-GGUF"
set "MODEL_FILE=UD-Q4_K_XL"
set "MTP_FILE=mtp-gemma-4-26B-A4B-it.gguf"
set "LLAMA_EXE=M:\Progarmy\llama-bench\llama-server.exe"
set "PORT=1236"
set "HOST=0.0.0.0"
set "CTX=8192"
set "NGL=999"
set "BATCH=2048"
set "PARALLEL=4"

if not exist "%LLAMA_EXE%" (
    echo ERROR: llama-server not found
    exit /b 1
)
if not exist "%MODEL_DIR%\%MODEL_FILE%.gguf" (
    echo ERROR: Model not found
    exit /b 1
)
if not exist "%MODEL_DIR%\%MTP_FILE%" (
    echo ERROR: MTP drafter not found
    exit /b 1
)

echo.
echo ============================================
echo  Start llama-server z MTP (26B A4B)
echo  Port:   %PORT%
echo ============================================
echo.

"%LLAMA_EXE%" ^
    --model "%MODEL_DIR%\%MODEL_FILE%.gguf" ^
    --model-draft "%MODEL_DIR%\%MTP_FILE%" ^
    --alias "gemma-4-26b-a4b-it-qat-mtp" ^
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
