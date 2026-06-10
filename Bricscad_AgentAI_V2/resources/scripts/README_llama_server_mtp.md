# Uruchamianie Gemma-4 IT QAT z MTP (Multi-Token Prediction)

## Dlaczego modele IT są wolniejsze?

W testach porównawczych (2026-06-10):

| Model | Gen TPS | Prompt TPS | TTFT | Total Time |
|-------|---------|------------|------|------------|
| 31B IT (Unsloth) | **8.41** | 55.7 | 2546ms | 6155ms |
| 26B IT (Unsloth) | 46.26 | 305.2 | 377ms | 1025ms |
| 26B google | 57.09 | 382.9 | 355ms | 886ms |
| 31B google | 13.70 | 97.7 | 1679ms | 4332ms |

**Główna przyczyna**: Gemma 4 to **hybrid-thinking model**. Domyślnie `enable_thinking: true` - model generuje wewnętrzne rozumowanie ZANIM odpowie. To dodaje ogromny overhead.

**31B IT jest 7x wolniejszy** niż 26B IT (8.41 vs 46.26 t/s) - to znak że thinking generuje masę tokenów.

## Rozwiązanie 1: Wyłączyć thinking (łatwe, szybkie)

W LM Studio per model:
1. **My Models** → kliknij ⚙️ na modelu → 🧪 Advanced Configuration
2. **Reasoning** → ustaw na **Off**
3. Lub w naszym niestandardowym template: `--chat-template-kwargs '{"enable_thinking":false}'`

**Oczekiwany efekt**: 31B IT 8.41 → ~40-50 t/s (5x szybciej).

## Rozwiązanie 2: Użyć MTP (Multi-Token Prediction) - najszybsze

Modele Unsloth mają specjalne pliki MTP drafter:
- `mtp-gemma-4-26B-A4B-it.gguf` (252 MB)
- `mtp-gemma-4-31B-it.gguf` (280 MB)

MTP to **speculative decoding** - mały drafter model przewiduje tokeny, główny model weryfikuje. Według Unsloth: 200MB smaller, near-lossless.

### Pliki modeli (na dysku M:)

```
M:\ai\unsloth\
├── gemma-4-26B-A4B-it-qat-GGUF\
│   ├── gemma-4-26B-A4B-it-qat-UD-Q4_K_XL.gguf  (14.2 GB)
│   ├── mtp-gemma-4-26B-A4B-it.gguf              (252 MB)
│   └── mmproj-F32.gguf
└── gemma-4-31B-it-qat-GGUF\
    ├── gemma-4-31B-it-qat-UD-Q4_K_XL.gguf      (17.3 GB)
    ├── mtp-gemma-4-31B-it.gguf                 (280 MB)
    └── mmproj-F32.gguf
```

## Uruchamianie z llama-server

**Narzędzie**: `M:\Progarmy\llama-bench\llama-server.exe` (wersja 8893)

### Szybki start

```bash
# Terminal 1 - Uruchom 31B z MTP (port 1235)
cd D:\GitHub\Bricscad_AgentAI\Bricscad_AgentAI_V2\resources\scripts
start_llama_server_31b_mtp.bat

# Terminal 2 - Uruchom 26B z MTP (port 1236)
start_llama_server_26b_mtp.bat
```

### Konfiguracja benchmarku w `llm_providers.json`

Dodaj nowy provider wskazujący na llama-server:

```json
{
  "Id": "uuid-nowy-31b",
  "Name": "llama-server 31B MTP",
  "EndpointUrl": "http://localhost:1235/v1/chat/completions",
  "ApiKey": "not-needed",
  "ModelName": "gemma-4-31b-it-qat-mtp",
  "Temperature": 0.2,
  "MaxTokens": 3500,
  "TopP": 0.95,
  "TopK": 64,
  "MinP": 0.0,
  "RepetitionPenalty": 1.0,
  "ReasoningEffort": "none"
}
```

### Test curl (weryfikacja)

```powershell
$body = @{
    model = "gemma-4-31b-it-qat-mtp"
    messages = @(@{role = "user"; content = "Wylistuj bloki."})
    tools = @(
        @{
            type = "function"
            function = @{
                name = "ListBlocks"
                description = "List blocks"
                parameters = @{
                    type = "object"
                    properties = @{ SaveAs = @{ type = "string" } }
                }
            }
        }
    )
    tool_choice = "auto"
    temperature = 0.2
    max_tokens = 200
} | ConvertTo-Json -Depth 10 -Compress

Invoke-WebRequest -Uri "http://localhost:1235/v1/chat/completions" `
    -Method POST -ContentType "application/json" -Body $body `
    -TimeoutSec 120 -UseBasicParsing | Select-Object -ExpandProperty Content
```

Powinno zwrócić JSON z `tool_calls` (nie tekst `<|tool_call|>`).

### Parametry llama-server użyte w skrypcie

| Parametr | Wartość | Uzasadnienie |
|----------|---------|--------------|
| `--model` | main GGUF | 14-17 GB |
| `--model-draft` | MTP drafter | 252-280 MB (auto-wykrywany) |
| `--ctx-size` | 8192 | Benchmark używa krótkich promptów |
| `--n-gpu-layers` | 999 | Cały model na GPU |
| `--batch-size` | 2048 | Standard |
| `--parallel` | 4 | Wielokrotne requesty równolegle |
| `--flash-attn` | on | Szybsze inference |
| `--reasoning` | off | **KLUCZOWE** - wyłącza thinking |
| `--jinja` | - | Używa jinja template z GGUF |
| `--chat-template-kwargs` | `{"enable_thinking":false}` | Dodatkowe wyłączenie |

### Oczekiwane rezultaty

| Metryka | Bez MTP | Z MTP | Bez thinking |
|---------|---------|-------|--------------|
| 31B IT Gen TPS | 8.41 | ~25-35 | ~40-50 |
| 31B IT Total (25 testów) | 487s | ~200-250s | ~150-200s |

**Kombinacja (MTP + bez thinking)**: 31B IT powinien osiągnąć ~100-150s (porównywalne z google 297s, a może szybciej).

## Troubleshooting

### llama-server nie startuje
- Sprawdź czy pliki GGUF istnieją w M:\ai\unsloth\
- Sprawdź czy llama-server.exe ma uprawnienia
- Sprawdź logi - szukaj "CUDA out of memory" - zmniejsz `NGL` lub `parallel`

### benchmark_09 nie działa
- Sprawdź czy profil jest `CadBlocksProfile` (nie pusty)
- Sprawdź czy `tools` są w formacie JSON prawidłowym
- Sprawdź prompt - chat template (kopiuj z `resources/chat-templates/gemma4-google-fixed.jinja`)

### MTP nie działa (wolne tak samo)
- Sprawdź czy `mtp-gemma-4-XX-it.gguf` jest w tym samym katalogu co main model
- Sprawdź logi llama-server - powinien wypisać "using MTP drafter"
- Sprawdź wersję llama-server (minimum b4400+ dla MTP auto-discovery)

## Co to jest MTP?

MTP (Multi-Token Prediction) to **speculative decoding** gdzie:
- Mały **drafter** model szybko proponuje tokeny (np. 4 na raz)
- Duży **target** model weryfikuje je wszystkie naraz
- Zaakceptowane tokeny są używane, odrzucone są regenerowane

**Zalety MTP vs zwykły draft model**:
- Drafter ma **identyczny vocabulary** co target (nie potrzeba dopasowywania)
- Drafter jest **współdzielony** z KV cache (mniej pamięci)
- Mniejszy drafter = szybsze generowanie

**Efektywność**: według Unsloth Gemma 4 MTP jest **near-lossless** - jakość wyjścia jest prawie identyczna.
