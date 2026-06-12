# Candidate rationale

## Source
- Profile: CadBlocksProfile
- Source prompt: prompt-lab\blocks\candidate_001\system_prompt_blocks.candidate.txt
- Benchmark report: D:\GitHub\Bricscad_AgentAI\prompt-lab\blocks\candidate_001\bricscad-results\Benchmark_09_InsertBlock_Extended_FULL_20260610_2200.json
- Baseline score: 84,00%

## Failed tests analyzed
| Test | Category | Difficulty | Classification | Decision |
|------|----------|------------|----------------|----------|
| 5 | ListBlocksAdvanced | D3 | prompt_gap | Add a small, concrete rule tied to the failed workflow. |
| 6 | ListBlocksAdvanced | D4 | prompt_gap | Add a small, concrete rule tied to the failed workflow. |
| 18 | InsertBlockWorkflows | D4 | prompt_gap | Add a small, concrete rule tied to the failed workflow. |
| 20 | InsertBlockWorkflows | D5 | prompt_gap | Add a small, concrete rule tied to the failed workflow. |

## Proposed changes
- Gdy zadanie dotyczy wielu elementow tego samego typu, preferuj Foreach zamiast serii osobnych wywolan narzedzia.
- Gdy zadanie dotyczy wielu elementow tego samego typu, preferuj Foreach zamiast serii osobnych wywolan narzedzia.

## Not changed

## Regression risks
- Added rules may over-trigger on simpler tasks; verify category-level regressions after benchmark.
- Do not promote this candidate unless BricsCAD benchmark confirms improvement without unacceptable regressions.

## Manual lab edit
- Added targeted rules for ListBlocks -> Foreach(TargetVariable) using the same variable name as SaveAs, without @.
- Added targeted rule that the full CreateBlock/EditBlock/InsertBlock pipeline must end with EditAttributes when attributes are requested.
- Added targeted rule to keep Foreach InsertBlock Action minimal and avoid default Rotation/Scale unless requested.
