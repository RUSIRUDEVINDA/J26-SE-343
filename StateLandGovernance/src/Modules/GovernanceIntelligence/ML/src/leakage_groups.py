"""
leakage_groups.py
-----------------
Deterministic source-and-text grouping utility for Experiment 2 of the
Component 4 GovernanceIntelligence text-classification ML module.

Purpose
-------
Identifies and merges related complaint records using:
1. Identical Source_Group_ID (shared audit document / source citation)
2. Exact text matches (after lowercasing, trimming, and whitespace collapse)
3. Near-duplicate narrative overlap (5-word shingle Jaccard similarity >= 0.50)

Records connected by any of these edges are transitively grouped into a single
Combined_Group key using Disjoint Set Union (Union-Find). Group identifiers are
deterministically derived from the minimum Research_ID in each connected component,
ensuring complete row-order independence.

Note on Transitivity
--------------------
Transitive merging (if A is similar to B, and B is similar to C, then A, B, and C
share one Combined_Group) intentionally creates broad grouping clusters for
standardized reporting boilerplate. This makes Experiment 2 a conservative
sensitivity evaluation that prevents template leakage across cross-validation folds.
It does NOT imply that merged records describe the exact same real-world incident.
"""

from __future__ import annotations

import json
import logging
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

import pandas as pd

logger = logging.getLogger(__name__)

SHINGLE_K = 5
JACCARD_THRESHOLD = 0.50


class UnionFind:
    """
    Standard Disjoint Set Union with path compression.
    """

    def __init__(self, elements: list[str]) -> None:
        self.parent: dict[str, str] = {x: x for x in elements}

    def find(self, x: str) -> str:
        path = []
        curr = x
        while self.parent[curr] != curr:
            path.append(curr)
            curr = self.parent[curr]
        for node in path:
            self.parent[node] = curr
        return curr

    def union(self, x: str, y: str) -> None:
        rx = self.find(x)
        ry = self.find(y)
        if rx != ry:
            self.parent[rx] = ry


def normalize_exact_text(text: str) -> str:
    """Lowercase, strip, and collapse contiguous whitespace."""
    return re.sub(r"\s+", " ", str(text).strip().lower())


def tokenize_words(text: str) -> list[str]:
    """Tokenize lowercase text into word tokens."""
    return re.findall(r"\b\w+\b", str(text).lower())


def build_shingles(words: list[str], k: int = SHINGLE_K) -> set[tuple[str, ...]]:
    """
    Construct a set of consecutive k-token tuples.
    Returns empty set if word count is less than k.
    """
    if len(words) < k:
        return set()
    return set(tuple(words[i : i + k]) for i in range(len(words) - k + 1))


def validate_grouping_inputs(
    df: pd.DataFrame,
    id_column: str = "Research_ID",
    source_group_column: str = "Source_Group_ID",
    text_column: str = "Canonical_English_Text",
) -> None:
    """
    Validate that input DataFrame contains non-blank unique IDs,
    non-blank source groups, and non-blank texts.
    """
    for col in [id_column, source_group_column, text_column]:
        if col not in df.columns:
            raise ValueError(f"Required column '{col}' missing from DataFrame.")

    # Unique IDs
    if df[id_column].isna().any():
        raise ValueError(f"Column '{id_column}' contains null values.")
    blank_ids = (df[id_column].astype(str).str.strip() == "").sum()
    if blank_ids > 0:
        raise ValueError(f"Column '{id_column}' contains blank IDs.")
    if df[id_column].duplicated().any():
        dups = df[id_column][df[id_column].duplicated()].tolist()
        raise ValueError(f"Duplicate values found in '{id_column}': {dups}")

    # Nonblank source groups
    if df[source_group_column].isna().any():
        raise ValueError(f"Column '{source_group_column}' contains null values.")
    blank_groups = (df[source_group_column].astype(str).str.strip() == "").sum()
    if blank_groups > 0:
        raise ValueError(f"Column '{source_group_column}' contains blank values.")

    # Nonblank text
    if df[text_column].isna().any():
        raise ValueError(f"Column '{text_column}' contains null values.")
    blank_texts = (df[text_column].astype(str).str.strip() == "").sum()
    if blank_texts > 0:
        raise ValueError(f"Column '{text_column}' contains blank values.")


def compute_combined_groups(
    df: pd.DataFrame,
    id_column: str = "Research_ID",
    source_group_column: str = "Source_Group_ID",
    text_column: str = "Canonical_English_Text",
    target_column: str = "ML_Label_4Class",
    shingle_k: int = SHINGLE_K,
    jaccard_threshold: float = JACCARD_THRESHOLD,
) -> tuple[pd.DataFrame, list[dict[str, Any]], dict[str, Any]]:
    """
    Build deterministic Combined_Group assignments based on identical source groups,
    exact normalized text matches, and 5-word shingle Jaccard similarity >= 0.50.

    Returns
    -------
    manifest_df : pd.DataFrame
        Columns: Research_ID, Source_Group_ID, Combined_Group.
    similarity_pairs : list[dict]
        Qualifying text pairs with Research_ID_1, Research_ID_2, Similarity, Exact_Match.
    summary : dict
        High-level grouping diagnostics, distributions, and conservative grouping notice.
    """
    validate_grouping_inputs(df, id_column, source_group_column, text_column)

    rids = df[id_column].tolist()
    uf = UnionFind(rids)

    # 1. Edges from identical Source_Group_ID
    for _, group_df in df.groupby(source_group_column):
        members = group_df[id_column].tolist()
        for i in range(len(members) - 1):
            uf.union(members[0], members[i + 1])

    # 2. Edges from exact normalized text match
    normalized_texts = df[text_column].apply(normalize_exact_text).tolist()
    norm_to_ids: dict[str, list[str]] = defaultdict(list)
    for rid, norm_t in zip(rids, normalized_texts):
        norm_to_ids[norm_t].append(rid)

    similarity_pairs: list[dict[str, Any]] = []
    seen_pairs: set[tuple[str, str]] = set()

    for norm_t, member_ids in norm_to_ids.items():
        if len(member_ids) > 1:
            for i in range(len(member_ids)):
                for j in range(i + 1, len(member_ids)):
                    id1, id2 = sorted([member_ids[i], member_ids[j]])
                    seen_pairs.add((id1, id2))
                    similarity_pairs.append({
                        "Research_ID_1": id1,
                        "Research_ID_2": id2,
                        "Similarity": 1.0,
                        "Exact_Match": True,
                    })
                    uf.union(id1, id2)

    # 3. Edges from 5-word shingle Jaccard similarity >= threshold
    words_list = [tokenize_words(t) for t in df[text_column]]
    shingles_list = [build_shingles(w, k=shingle_k) for w in words_list]

    n = len(df)
    for i in range(n):
        s1 = shingles_list[i]
        if not s1:
            continue
        id1 = rids[i]
        for j in range(i + 1, n):
            s2 = shingles_list[j]
            if not s2:
                continue
            inter = len(s1 & s2)
            if inter == 0:
                continue
            union = len(s1 | s2)
            jaccard = inter / union  # unrounded comparison
            if jaccard >= jaccard_threshold:
                id2 = rids[j]
                pair_key = tuple(sorted([id1, id2]))
                if pair_key not in seen_pairs:
                    seen_pairs.add(pair_key)
                    similarity_pairs.append({
                        "Research_ID_1": pair_key[0],
                        "Research_ID_2": pair_key[1],
                        "Similarity": round(float(jaccard), 4),
                        "Exact_Match": False,
                    })
                uf.union(id1, id2)

    # Sort similarity pairs deterministically
    similarity_pairs.sort(key=lambda p: (p["Research_ID_1"], p["Research_ID_2"]))

    # 4. Deterministic Combined_Group assignment
    # Map each root to all component members
    components: dict[str, list[str]] = defaultdict(list)
    for rid in rids:
        root = uf.find(rid)
        components[root].append(rid)

    # Assign group name from lexicographically smallest Research_ID in component
    rid_to_cg: dict[str, str] = {}
    for root, members in components.items():
        min_rid = min(members)
        cg_name = f"CG-{min_rid}"
        for rid in members:
            rid_to_cg[rid] = cg_name

    manifest_rows = []
    for _, row in df.iterrows():
        rid = row[id_column]
        manifest_rows.append({
            id_column: rid,
            source_group_column: row[source_group_column],
            "Combined_Group": rid_to_cg[rid],
        })
    manifest_df = pd.DataFrame(manifest_rows)

    # 5. Diagnostic Summary
    cg_series = manifest_df["Combined_Group"]
    cg_counts = cg_series.value_counts()
    largest_cg = cg_counts.index[0]
    largest_cg_size = int(cg_counts.iloc[0])
    largest_members = manifest_df[manifest_df["Combined_Group"] == largest_cg][id_column].tolist()

    # Size distribution
    size_dist = {str(sz): int(cnt) for sz, cnt in cg_counts.value_counts().sort_index().items()}

    # Label distribution per combined group (if target column present)
    label_dist_per_group: dict[str, dict[str, int]] = {}
    if target_column in df.columns:
        temp_df = df[[id_column, target_column]].copy()
        temp_df["Combined_Group"] = temp_df[id_column].map(rid_to_cg)
        for cg_name, group_df in temp_df.groupby("Combined_Group"):
            label_dist_per_group[cg_name] = {
                str(lbl): int(cnt) for lbl, cnt in group_df[target_column].value_counts().items()
            }

    summary: dict[str, Any] = {
        "dataset_row_count": len(df),
        "source_group_count": int(df[source_group_column].nunique()),
        "combined_group_count": int(cg_series.nunique()),
        "qualifying_text_pair_count": len(similarity_pairs),
        "group_size_distribution": size_dist,
        "largest_group": {
            "combined_group": largest_cg,
            "size": largest_cg_size,
            "members": largest_members,
        },
        "label_distribution_per_group": label_dist_per_group,
        "grouping_parameters": {
            "shingle_k": shingle_k,
            "jaccard_threshold": jaccard_threshold,
            "case_normalization": "lower",
            "whitespace_normalization": "collapsed",
        },
        "conservative_grouping_notice": (
            "Transitive union-find merging of source documents, exact text matches, and "
            "near-duplicate shingle overlaps creates broad composite clusters. This prevents "
            "narrative template leakage across cross-validation folds, making Experiment 2 "
            "a conservative sensitivity baseline. It does not establish incident identity."
        ),
    }

    return manifest_df, similarity_pairs, summary


def save_grouping_artifacts(
    output_dir: Path,
    manifest_df: pd.DataFrame,
    similarity_pairs: list[dict[str, Any]],
    summary: dict[str, Any],
) -> None:
    """Save grouping manifest, similarity pairs, and grouping summary to output_dir."""
    output_dir.mkdir(parents=True, exist_ok=True)

    manifest_path = output_dir / "grouping_manifest.csv"
    pairs_path = output_dir / "similarity_pairs.csv"
    summary_path = output_dir / "grouping_summary.json"

    manifest_df.to_csv(manifest_path, index=False)
    pd.DataFrame(similarity_pairs).to_csv(pairs_path, index=False)

    with open(summary_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)

    logger.info("Grouping artifacts saved to '%s'", output_dir)
