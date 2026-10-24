# Frozen-model reproduction — 2026-10-20

Run from the workspace root after installing the existing ML requirements in `.tmp/c4-ml-venv`:

```powershell
& .tmp/c4-ml-venv/Scripts/python.exe output/evaluation/c4-pp1-20261020/reproduce.py
```

The runner imports the existing validated artifact loaders and scoring functions. It never calls fit, changes a threshold, regenerates a model/bundle or writes into the ML source/artifact directories. It writes a new scoring report to this directory. The frontend's working-tree report is a dated copy of that generated report, not a model/result service or live accuracy monitor.

- Complaint v2: **69** eligible reviewed prospective synthetic records in **23** families, excluded from the 425-row training population. Accuracy **0.9420289855**, macro F1 **0.9409760053**, weighted F1 **0.9429515402**. Per-class precision/recall/F1 and confusion matrix are in `reproduced-evaluation.json`. These match the saved historical Experiment 6 result, but were newly scored using the promoted frozen artifact. The original experiment script also fits a model, so it was deliberately not executed.
- Workflow anomaly: **108** reserved synthetic traces, **80** generator-designated references and **28** controlled perturbations. TP **19**, FP **6**, TN **74**, FN **9**; precision **0.76**, recall **0.6785714286**, F1 **0.7169811321**. Ranking metrics and unchanged threshold are in the report. No independent real-world ground truth is established; these are synthetic test results, not corruption-detection accuracy.
- Runtime: isolated Python **3.11**, scikit-learn **1.7.2** as pinned by existing requirements. `runtime-packages.txt` records installed packages. Artifact loaders, bundle verification, `pip check`, reproduced scoring and actual HTTP/UI inference passed. Original artifact metadata records a different Python/package environment; byte-for-byte model identity and successful compatibility checks are retained instead of silently reserializing artifacts.
- SHA-256 classifier: `63b37eb7175e74a8dcde82bf042fcf9dcbb4f0432fbe3b4677aac037007a20ae`.
- SHA-256 anomaly: `59ed4a73d56d9f06798c4ebc4684a633afe72841c41ed0b3ba4e7bb658782b46`.

Labels, input hashes, split descriptions, generation time, limitations and model versions are included in the report. No personal complaint text is copied into the public evaluation page. Synthetic families share generator origins; reference-normality is not independently verified. Some perturbation types have zero or very few test examples. Confidence, demonstration success and test pass counts are not evaluation accuracy.
