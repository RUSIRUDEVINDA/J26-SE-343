"""Unit tests for OCR evaluation metrics (CER, WER) and Sinhala Unicode handling."""

import unicodedata
from app.evaluation.metrics import (
    calculate_cer,
    calculate_wer,
    evaluate_ocr_pair,
    levenshtein_distance,
    normalize_text,
)


def test_levenshtein_distance():
    assert levenshtein_distance("", "") == 0
    assert levenshtein_distance("kitten", "sitting") == 3
    assert levenshtein_distance(["the", "quick", "fox"], ["the", "slow", "fox"]) == 1
    assert levenshtein_distance("abc", "") == 3
    assert levenshtein_distance("", "xyz") == 3


def test_calculate_cer_identical():
    ref = "State Land Lease Agreement"
    hyp = "State Land Lease Agreement"
    assert calculate_cer(ref, hyp) == 0.0


def test_calculate_cer_with_errors():
    ref = "12345"
    hyp = "123"  # 2 deletions
    # Edit distance = 2, length = 5 -> CER = 0.4
    assert calculate_cer(ref, hyp) == 0.4


def test_calculate_wer_identical():
    ref = "Government of Sri Lanka"
    hyp = "Government of Sri Lanka"
    assert calculate_wer(ref, hyp) == 0.0


def test_calculate_wer_with_errors():
    ref = "one two three four"
    hyp = "one two five four"  # 1 substitution
    # Edit distance = 1, length = 4 -> WER = 0.25
    assert calculate_wer(ref, hyp) == 0.25


def test_normalize_text_english():
    raw = "  State   Land\r\n\r\nLease\t\tAgreement   \n "
    normalized = normalize_text(raw)
    assert normalized == "State Land\n\nLease Agreement"


def test_normalize_text_sinhala_unicode_preservation():
    # Sinhala text with combining vowel signs (combining characters)
    # ශ්‍රී ලංකා (Sri Lanka)
    raw = "\r\n  ශ්‍රී   ලංකා   රාජ්‍ය   ඉඩම්  \t "
    normalized = normalize_text(raw)
    # Check that combining characters are preserved intact
    assert "ශ්‍රී ලංකා රාජ්‍ය ඉඩම්" in normalized
    # Check that NFC normalization preserves proper character count
    assert unicodedata.is_normalized("NFC", normalized)


def test_calculate_cer_sinhala_exact():
    ref = "ශ්‍රී ලංකා ප්‍රජාතාන්ත්‍රික සමාජවාදී ජනරජය"
    hyp = "ශ්‍රී ලංකා ප්‍රජාතාන්ත්‍රික සමාජවාදී ජනරජය"
    assert calculate_cer(ref, hyp) == 0.0
    assert calculate_wer(ref, hyp) == 0.0


def test_calculate_cer_sinhala_substitution():
    ref = "කොළඹ"
    hyp = "ගම්පහ"
    cer = calculate_cer(ref, hyp)
    assert cer > 0.0


def test_evaluate_ocr_pair_structure():
    ref = "Land Parcel Survey Plan SP-001\r\nExtent: 2.5 Acres"
    hyp = "Land Parcel Survey Plan SP-001 \nExtent: 2.5 Acres"
    eval_result = evaluate_ocr_pair(ref, hyp)

    assert "raw" in eval_result
    assert "normalized" in eval_result
    assert "counts" in eval_result
    assert eval_result["normalized"]["cer"] == 0.0
    assert eval_result["normalized"]["wer"] == 0.0
    assert eval_result["counts"]["reference_words"] == 8


def test_cer_wer_deterministic_sanity():
    # Section 11 exact requirements:
    # 1. reference: cat, hypothesis: cut -> CER = 1 / 3
    cer = calculate_cer("cat", "cut", normalize=False)
    assert round(cer, 4) == round(1.0 / 3.0, 4)

    # 2. WER simple word example: 1 substitution out of 4 words -> WER = 1 / 4
    wer = calculate_wer("the quick brown fox", "the slow brown fox", normalize=False)
    assert wer == 0.25

    # 3. Sinhala exact match -> CER = 0.0, WER = 0.0
    sinhala_text = "ශ්‍රී ලංකා ප්‍රජාතාන්ත්‍රික සමාජවාදී ජනරජය"
    assert calculate_cer(sinhala_text, sinhala_text, normalize=True) == 0.0
    assert calculate_wer(sinhala_text, sinhala_text, normalize=True) == 0.0


def test_evaluation_cli_runner(tmp_path, capsys):
    from app.evaluation.metrics import main

    ref_file = tmp_path / "ref.txt"
    hyp_file = tmp_path / "hyp.txt"
    ref_file.write_text("Hello World", encoding="utf-8")
    hyp_file.write_text("Hello Word", encoding="utf-8")

    # 1. Test with --reference-file and --hypothesis-file
    code = main(["--reference-file", str(ref_file), "--hypothesis-file", str(hyp_file)])
    assert code == 0
    captured = capsys.readouterr()
    assert '"cer":' in captured.out

    # 2. Test with positional arguments
    code_pos = main([str(ref_file), str(hyp_file)])
    assert code_pos == 0

    # 3. Test with direct text flags
    code_text = main(["--ref-text", "cat", "--hyp-text", "cut"])
    assert code_text == 0

