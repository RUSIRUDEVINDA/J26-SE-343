"""Unit tests for the independent OCR evaluation harness (CER & WER)."""

import pytest
from pathlib import Path
from evaluation.ocr.evaluate_ocr import (
    compute_cer,
    compute_wer,
    compute_levenshtein,
    normalize_strict,
    normalize_whitespace,
    validate_ground_truth,
    categorize_mismatch,
    extract_diff_snippets,
)


def test_normalize_strict_preserves_sinhala_and_unifies_newlines():
    text = "අයදුම්කරු:\r\n  පී.කේ.  ගුණසේකර \r\n"
    norm = normalize_strict(text)
    assert norm == "අයදුම්කරු:\nපී.කේ. ගුණසේකර"


def test_normalize_whitespace_collapses_all_tokens():
    text = "අයදුම්කරු:\n\n  පී.කේ.   ගුණසේකර \t මයා\n"
    norm = normalize_whitespace(text)
    assert norm == "අයදුම්කරු: පී.කේ. ගුණසේකර මයා"


def test_placeholder_validation_refuses_placeholder_marker(tmp_path):
    p = tmp_path / "gt_placeholder.txt"
    p.write_text("[PLACEHOLDER: Manually transcribe Sinhala reference text here]", encoding="utf-8")
    is_valid, msg, _ = validate_ground_truth(p)
    assert is_valid is False
    assert "placeholder marker" in msg


def test_placeholder_validation_refuses_empty_file(tmp_path):
    p = tmp_path / "gt_empty.txt"
    p.write_text("   \n\n  ", encoding="utf-8")
    is_valid, msg, _ = validate_ground_truth(p)
    assert is_valid is False
    assert "empty" in msg


def test_placeholder_validation_accepts_real_transcription(tmp_path):
    p = tmp_path / "gt_valid.txt"
    p.write_text("පී.කේ. ගුණසේකර මයා\n85/6, කුඩා කිවුල", encoding="utf-8")
    is_valid, msg, content = validate_ground_truth(p)
    assert is_valid is True
    assert "පී.කේ." in content


def test_cer_identical_strings_zero_error():
    ref = "රාජ්‍ය ඉඩම් බදු යෝජනාව"
    hyp = "රාජ්‍ය ඉඩම් බදු යෝජනාව"
    res = compute_cer(ref, hyp)
    assert res.error_rate == 0.0
    assert res.total_edits == 0
    assert res.substitutions == 0
    assert res.deletions == 0
    assert res.insertions == 0
    assert res.ref_count == len(ref)


def test_cer_known_edits():
    # ref: "kitten" (6 chars)
    # hyp: "sitting" (7 chars) -> sub k->s, sub e->i, ins g -> total 3 edits
    res = compute_cer("kitten", "sitting")
    assert res.ref_count == 6
    assert res.hyp_count == 7
    assert res.total_edits == 3
    assert res.substitutions == 2
    assert res.insertions == 1
    assert res.deletions == 0
    assert res.error_rate == pytest.approx(3 / 6)


def test_cer_can_exceed_100_percent_on_insertions():
    ref = "abc"
    hyp = "abcdefghij"
    res = compute_cer(ref, hyp)
    assert res.insertions == 7
    assert res.total_edits == 7
    assert res.error_rate == pytest.approx(7 / 3)
    assert res.error_rate > 1.0


def test_wer_known_edits():
    ref = "the quick brown fox jumps"
    hyp = "the fast brown fox jumps over"
    # substitutions: quick -> fast (1)
    # insertions: over (1)
    # total edits: 2, ref words: 5
    res = compute_wer(ref, hyp)
    assert res.ref_count == 5
    assert res.hyp_count == 6
    assert res.total_edits == 2
    assert res.substitutions == 1
    assert res.insertions == 1
    assert res.deletions == 0
    assert res.error_rate == pytest.approx(2 / 5)


def test_diff_snippets_extraction():
    ref = "පුත්තලම දිස්ත්‍රික්කය"
    hyp = "පුත්තලම දිස්ත්‍රික්කෙ"  # 1 substitution at end
    res = compute_cer(ref, hyp)
    snippets = extract_diff_snippets(res.alignment_ops, is_char_level=True)
    assert len(snippets) >= 1
    assert "REFERENCE" in snippets[0] or "reference" in snippets[0]

