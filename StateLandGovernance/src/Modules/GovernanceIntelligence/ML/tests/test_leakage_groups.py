"""
test_leakage_groups.py
-----------------------
Unit tests for deterministic source-and-text grouping utility (Experiment 2).

Tests cover:
* Same-source records remain grouped
* Exact-normalized duplicates remain grouped
* Qualifying similar texts remain grouped
* Transitive links remain grouped
* Unrelated records stay separate
* Shuffling rows preserves record-to-group assignments (row order invariance)
* Short texts (< 5 tokens) do not falsely match through empty shingle sets
* Duplicate IDs and blank required values are rejected with ValueError
"""

from __future__ import annotations

import sys
from pathlib import Path

import pandas as pd
import pytest

# Add src/ to sys.path
_SRC_DIR = Path(__file__).resolve().parent.parent / "src"
sys.path.insert(0, str(_SRC_DIR))

from leakage_groups import (
    UnionFind,
    build_shingles,
    compute_combined_groups,
    normalize_exact_text,
    tokenize_words,
    validate_grouping_inputs,
)


class TestUnionFind:
    def test_initial_state(self):
        uf = UnionFind(["A", "B", "C"])
        assert uf.find("A") == "A"
        assert uf.find("B") == "B"

    def test_union_and_transitivity(self):
        uf = UnionFind(["A", "B", "C", "D"])
        uf.union("A", "B")
        uf.union("B", "C")
        assert uf.find("A") == uf.find("C")
        assert uf.find("D") != uf.find("A")


class TestTextNormalizationAndShingles:
    def test_normalize_exact_text(self):
        raw = "   Lease   RENTAL   unpaid \n\t for 3 years.  "
        expected = "lease rental unpaid for 3 years."
        assert normalize_exact_text(raw) == expected

    def test_tokenize_words(self):
        text = "Unauthorized allocation, transfer, or misuse of 100 acres!"
        tokens = tokenize_words(text)
        assert tokens == ["unauthorized", "allocation", "transfer", "or", "misuse", "of", "100", "acres"]

    def test_short_text_empty_shingles(self):
        words = ["state", "land", "lease"]
        assert len(words) < 5
        shingles = build_shingles(words, k=5)
        assert shingles == set()

    def test_five_word_shingles(self):
        words = ["one", "two", "three", "four", "five", "six"]
        shingles = build_shingles(words, k=5)
        assert len(shingles) == 2
        assert ("one", "two", "three", "four", "five") in shingles
        assert ("two", "three", "four", "five", "six") in shingles


class TestInputValidation:
    def test_duplicate_ids_raise_value_error(self):
        df = pd.DataFrame([
            {"Research_ID": "ID-1", "Source_Group_ID": "G1", "Canonical_English_Text": "Complaint text A."},
            {"Research_ID": "ID-1", "Source_Group_ID": "G2", "Canonical_English_Text": "Complaint text B."},
        ])
        with pytest.raises(ValueError, match="Duplicate values found in 'Research_ID'"):
            validate_grouping_inputs(df)

    def test_blank_id_raises_value_error(self):
        df = pd.DataFrame([
            {"Research_ID": "   ", "Source_Group_ID": "G1", "Canonical_English_Text": "Complaint text A."},
        ])
        with pytest.raises(ValueError, match="contains blank IDs"):
            validate_grouping_inputs(df)

    def test_blank_source_group_raises_value_error(self):
        df = pd.DataFrame([
            {"Research_ID": "ID-1", "Source_Group_ID": "", "Canonical_English_Text": "Complaint text A."},
        ])
        with pytest.raises(ValueError, match="contains blank values"):
            validate_grouping_inputs(df)

    def test_blank_text_raises_value_error(self):
        df = pd.DataFrame([
            {"Research_ID": "ID-1", "Source_Group_ID": "G1", "Canonical_English_Text": " \t \n "},
        ])
        with pytest.raises(ValueError, match="contains blank values"):
            validate_grouping_inputs(df)


class TestGroupingRules:
    def test_same_source_records_remain_grouped(self):
        df = pd.DataFrame([
            {"Research_ID": "REC-01", "Source_Group_ID": "AUDIT-DOC-1", "Canonical_English_Text": "First distinct incident narrative with unique words."},
            {"Research_ID": "REC-02", "Source_Group_ID": "AUDIT-DOC-1", "Canonical_English_Text": "Second totally different incident complaint text."},
            {"Research_ID": "REC-03", "Source_Group_ID": "AUDIT-DOC-2", "Canonical_English_Text": "Third independent incident description text."},
        ])
        manifest, _, summary = compute_combined_groups(df)
        group_map = dict(zip(manifest["Research_ID"], manifest["Combined_Group"]))
        assert group_map["REC-01"] == group_map["REC-02"]
        assert group_map["REC-01"] != group_map["REC-03"]
        assert group_map["REC-01"] == "CG-REC-01"

    def test_exact_normalized_duplicates_remain_grouped(self):
        df = pd.DataFrame([
            {"Research_ID": "REC-10", "Source_Group_ID": "SRC-A", "Canonical_English_Text": "State land lease rent not collected for five consecutive years."},
            {"Research_ID": "REC-20", "Source_Group_ID": "SRC-B", "Canonical_English_Text": "  state   LAND  lease rent   not collected for five consecutive years. \n"},
        ])
        manifest, sim_pairs, _ = compute_combined_groups(df)
        group_map = dict(zip(manifest["Research_ID"], manifest["Combined_Group"]))
        assert group_map["REC-10"] == group_map["REC-20"]
        assert len(sim_pairs) == 1
        assert sim_pairs[0]["Exact_Match"] is True
        assert sim_pairs[0]["Similarity"] == 1.0

    def test_qualifying_similar_texts_remain_grouped(self):
        base_words = "state land was leased to private developers without provincial council approval or public auction in the district"
        text1 = f"Case Alpha {base_words} for commercial hotels."
        text2 = f"Case Beta {base_words} for residential estates."
        df = pd.DataFrame([
            {"Research_ID": "ID-A", "Source_Group_ID": "G-A", "Canonical_English_Text": text1},
            {"Research_ID": "ID-B", "Source_Group_ID": "G-B", "Canonical_English_Text": text2},
        ])
        manifest, sim_pairs, _ = compute_combined_groups(df)
        group_map = dict(zip(manifest["Research_ID"], manifest["Combined_Group"]))
        assert group_map["ID-A"] == group_map["ID-B"]
        assert len(sim_pairs) >= 1
        assert sim_pairs[0]["Similarity"] >= 0.50

    def test_transitive_links_remain_grouped(self):
        # A linked to B by source group; B linked to C by near-duplicate text
        text_b = "government land in western province was allocated illegally without tender procedure or cabinet approval."
        text_c = "state government land in western province was allocated illegally without tender procedure or cabinet approval."
        df = pd.DataFrame([
            {"Research_ID": "R-1", "Source_Group_ID": "SRC-X", "Canonical_English_Text": "Completely unrelated narrative text for first record."},
            {"Research_ID": "R-2", "Source_Group_ID": "SRC-X", "Canonical_English_Text": text_b},
            {"Research_ID": "R-3", "Source_Group_ID": "SRC-Y", "Canonical_English_Text": text_c},
        ])
        manifest, _, _ = compute_combined_groups(df)
        group_map = dict(zip(manifest["Research_ID"], manifest["Combined_Group"]))
        # R-1 and R-2 share SRC-X. R-2 and R-3 share near-duplicate text.
        # Transitively, all three must be in the same Combined_Group.
        assert group_map["R-1"] == group_map["R-2"]
        assert group_map["R-2"] == group_map["R-3"]
        assert group_map["R-1"] == "CG-R-1"

    def test_unrelated_records_stay_separate(self):
        df = pd.DataFrame([
            {"Research_ID": "R-A", "Source_Group_ID": "SRC-1", "Canonical_English_Text": "Bribery and corruption allegations against the land commissioner."},
            {"Research_ID": "R-B", "Source_Group_ID": "SRC-2", "Canonical_English_Text": "Environmental degradation in protected coastal mangrove wetland reserve."},
            {"Research_ID": "R-C", "Source_Group_ID": "SRC-3", "Canonical_English_Text": "Annual lease rental arrears exceeding fifty million rupees uncollected."},
        ])
        manifest, sim_pairs, summary = compute_combined_groups(df)
        assert summary["combined_group_count"] == 3
        assert len(sim_pairs) == 0
        groups = manifest["Combined_Group"].tolist()
        assert len(set(groups)) == 3

    def test_short_texts_do_not_falsely_match(self):
        # Texts with < 5 words have empty shingle sets and must not match each other
        df = pd.DataFrame([
            {"Research_ID": "S-1", "Source_Group_ID": "SRC-1", "Canonical_English_Text": "Lease rent unpaid."},
            {"Research_ID": "S-2", "Source_Group_ID": "SRC-2", "Canonical_English_Text": "Protected land encroached."},
        ])
        manifest, sim_pairs, _ = compute_combined_groups(df)
        group_map = dict(zip(manifest["Research_ID"], manifest["Combined_Group"]))
        assert group_map["S-1"] != group_map["S-2"]
        assert len(sim_pairs) == 0

    def test_row_shuffling_preserves_group_assignments(self):
        df = pd.DataFrame([
            {"Research_ID": "Z-99", "Source_Group_ID": "G-A", "Canonical_English_Text": "Standardized boilerplate lease non-compliance audit observation report case."},
            {"Research_ID": "A-01", "Source_Group_ID": "G-B", "Canonical_English_Text": "Standardized boilerplate lease non-compliance audit observation report case."},
            {"Research_ID": "M-50", "Source_Group_ID": "G-C", "Canonical_English_Text": "Completely distinct narrative text about mangrove logging."},
        ])
        manifest1, _, _ = compute_combined_groups(df)
        # Shuffled version
        shuffled_df = df.sample(frac=1.0, random_state=123).reset_index(drop=True)
        manifest2, _, _ = compute_combined_groups(shuffled_df)

        map1 = dict(zip(manifest1["Research_ID"], manifest1["Combined_Group"]))
        map2 = dict(zip(manifest2["Research_ID"], manifest2["Combined_Group"]))

        assert map1 == map2
        # Deterministic name uses min Research_ID across component (A-01 < Z-99)
        assert map1["Z-99"] == "CG-A-01"
        assert map1["A-01"] == "CG-A-01"
        assert map1["M-50"] == "CG-M-50"
