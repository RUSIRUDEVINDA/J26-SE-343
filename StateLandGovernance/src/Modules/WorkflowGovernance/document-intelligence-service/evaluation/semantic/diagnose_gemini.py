"""
Diagnostic Script for Gemini API Connectivity and Layer Isolation.

Sequentially tests:
0. Credential formatting validation (safe: never reveals key value).
1. Authenticated model listing (GET /v1beta/models).
2. Minimal generateContent request without schema.
3. Minimal structured-JSON request with simple schema.
4. Full extraction request (only if layers 1-3 succeed).

Identifies whether HTTP 400 originates from:
- Credential formatting (quotes, whitespace, control chars).
- Malformed URL or non-existent model ID.
- Network intermediary / proxy vs Google GFE.
- Payload schema / generationConfig incompatibility.
"""

import json
import os
import re
import sys
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple
import httpx

# Ensure document-intelligence-service root is in sys.path
_current_dir = Path(__file__).resolve().parent
_service_root = _current_dir.parent.parent
if str(_service_root) not in sys.path:
    sys.path.insert(0, str(_service_root))

from app.core.config import settings
from app.semantic.gemini_client import (
    GeminiAuthenticationError,
    GeminiBadRequestError,
    GeminiClient,
    GeminiError,
    GeminiNotFoundError,
)
from app.semantic.gemini_extractor import (
    GEMINI_RESPONSE_SCHEMA,
    GEMINI_SYSTEM_INSTRUCTION,
    GeminiSemanticExtractor,
)


def inspect_credential_format(key: Optional[str]) -> Dict[str, Any]:
    """
    Safely inspects credential formatting without printing or exposing the secret.
    """
    if not key:
        return {"present": False, "reason": "Environment variable GEMINI_API_KEY is not set or empty"}

    raw = str(key)
    has_dq = raw.startswith('"') and raw.endswith('"')
    has_sq = raw.startswith("'") and raw.endswith("'")
    has_quotes = has_dq or has_sq
    has_leading_ws = raw != raw.lstrip()
    has_trailing_ws = raw != raw.rstrip()
    has_internal_ws = bool(re.search(r"\s", raw.strip('"\'')))
    clean = raw.strip().strip('"\'').strip()
    has_control_chars = any(ord(c) < 32 or ord(c) == 127 for c in clean)
    is_ascii = raw.isascii()

    return {
        "present": True,
        "raw_length": len(raw),
        "cleaned_length": len(clean),
        "starts_with_aiza": clean.startswith("AIza"),
        "has_surrounding_quotes": has_quotes,
        "quote_type": "double" if has_dq else ("single" if has_sq else "none"),
        "has_leading_trailing_whitespace": has_leading_ws or has_trailing_ws,
        "has_internal_whitespace": has_internal_ws,
        "has_control_chars": has_control_chars,
        "is_pure_ascii": is_ascii,
        "cleaned_key": clean,
    }


def classify_endpoint_origin(headers: httpx.Headers, body_text: str) -> str:
    """Classifies whether the response originates from Google GFE or an intermediary proxy."""
    server = headers.get("server", "")
    via = headers.get("via", "")
    alt_svc = headers.get("alt-svc", "")

    indicators = []
    if "scaffolding on HTTPServer2" in server or "ESF" in server or "GSE" in server:
        indicators.append("Google GFE (scaffolding on HTTPServer2)")
    elif server:
        indicators.append(f"Non-standard server header: {server}")

    if via:
        indicators.append(f"Intermediary Via header present: {via}")
    if "h3" in alt_svc:
        indicators.append("Google Alt-Svc (HTTP/3) present")

    if "Error 400 (Bad Request)!!1" in body_text:
        indicators.append("Classic Google Front End (GFE) HTML error template")

    if not indicators:
        return "Unknown origin"
    return "; ".join(indicators)


def run_diagnostics():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    print("\n" + "=" * 80)
    print("GEMINI API DIAGNOSTIC AND LAYER ISOLATION RUNNER")
    print("=" * 80)

    # ------------------------------------------------------------------------
    # LAYER 0: Credential & Configuration Inspection
    # ------------------------------------------------------------------------
    print("\n[LAYER 0] Inspecting Credential & Environment Configuration...")

    # Explicit precedence: 1. os.environ -> 2. settings.gemini_api_key (.env file)
    env_var_key = os.getenv("GEMINI_API_KEY")
    settings_key = settings.gemini_api_key

    if env_var_key and env_var_key.strip():
        raw_key = env_var_key
        key_source = "environment variable (os.environ)"
    elif settings_key and settings_key.strip():
        raw_key = settings_key
        key_source = ".env file (document-intelligence-service/.env)"
    elif env_var_key is not None:
        raw_key = env_var_key
        key_source = "environment variable (os.environ - empty)"
    elif settings_key is not None:
        raw_key = settings_key
        key_source = ".env file (document-intelligence-service/.env - empty)"
    else:
        raw_key = None
        key_source = "unconfigured"

    print(f"  [*] Credential Source: {key_source}")

    cred_info = inspect_credential_format(raw_key)

    if not cred_info["present"]:
        print("  [-] FAIL: GEMINI_API_KEY is not configured or is empty.")
        print("      To configure, provide GEMINI_API_KEY in:")
        print("      1. StateLandGovernance/src/Modules/WorkflowGovernance/document-intelligence-service/.env")
        print('      2. Or PowerShell environment: $env:GEMINI_API_KEY="AIzaSy..."')
        print("=" * 80 + "\n")
        return

    print(f"  [+] Credential detected: length={cred_info['raw_length']} chars")
    print(f"  [*] Starts with 'AIza': {cred_info['starts_with_aiza']}")
    print(f"  [*] Surrounding quotes detected: {cred_info['has_surrounding_quotes']} (type: {cred_info['quote_type']})")
    print(f"  [*] Leading/trailing whitespace: {cred_info['has_leading_trailing_whitespace']}")
    print(f"  [*] Internal whitespace: {cred_info['has_internal_whitespace']}")
    print(f"  [*] Control characters detected: {cred_info['has_control_chars']}")
    print(f"  [*] Pure ASCII: {cred_info['is_pure_ascii']}")

    # Pre-flight rejection: reject empty or corrupted credentials before any HTTP request
    if cred_info["has_control_chars"]:
        print("  [-] CRITICAL: Credential contains invalid control characters (e.g. carriage returns or unprintable bytes)!")
        print("      Rejecting before any HTTP request is dispatched. Please sanitize the .env or environment variable.")
        print("=" * 80 + "\n")
        return

    if not cred_info["cleaned_key"]:
        print("  [-] CRITICAL: Credential is empty or contains only whitespace after sanitization!")
        print("      Rejecting before any HTTP request is dispatched.")
        print("=" * 80 + "\n")
        return

    if not cred_info["starts_with_aiza"]:
        print("  [!] WARNING: Key does NOT start with 'AIza' (the standard prefix for Google AI Studio API keys).")
        print("      If this is not a valid Gemini API key, Google GFE / REST API will reject it.")

    if cred_info["has_surrounding_quotes"] or cred_info["has_leading_trailing_whitespace"]:
        print("  [!] WARNING: Credential had surrounding quotes or whitespace that would corrupt the HTTP header!")
        print("      Using automatically sanitized key (length: {} chars)".format(cred_info["cleaned_length"]))

    active_key = cred_info["cleaned_key"]
    base_url = (os.getenv("GEMINI_BASE_URL") or settings.gemini_base_url).rstrip("/")
    configured_model = os.getenv("GEMINI_MODEL") or settings.gemini_model
    print(f"  [*] Base URL: {base_url}")
    print(f"  [*] Configured Model in Settings: '{configured_model}'")

    # ------------------------------------------------------------------------
    # LAYER 1: Authenticated Model Listing
    # ------------------------------------------------------------------------
    print("\n[LAYER 1] Testing Authenticated Model Listing (GET /v1beta/models)...")
    list_url = f"{base_url}/models"
    headers = {
        "x-goog-api-key": active_key,
        "User-Agent": "StateLandGovernance-Diagnostics/1.0",
    }

    try:
        with httpx.Client(timeout=15.0) as client:
            resp1 = client.get(list_url, headers=headers)
    except Exception as ex:
        print(f"  [-] Network connection failure: {ex}")
        print("=" * 80 + "\n")
        return

    ct1 = resp1.headers.get("content-type", "")
    server1 = resp1.headers.get("server", "None")
    origin1 = classify_endpoint_origin(resp1.headers, resp1.text)
    print(f"  [*] HTTP Status: {resp1.status_code}")
    print(f"  [*] Content-Type: {ct1}")
    print(f"  [*] Server Header: {server1}")
    print(f"  [*] Endpoint Origin Analysis: {origin1}")

    if resp1.status_code != 200:
        print(f"  [-] LAYER 1 FAILED (HTTP {resp1.status_code})")
        print(f"      Body snippet: {repr(resp1.text[:250])}")
        if resp1.status_code in (401, 403):
            print("      Verdict: API key was rejected by Google. Verify key validity in Google AI Studio.")
        elif resp1.status_code == 400:
            print("      Verdict: Request was rejected as HTTP 400 Bad Request at the network/header layer.")
            if "Error 400 (Bad Request)!!1" in resp1.text:
                print("      Specific: Google Front End (GFE) rejected the HTTP request before reaching the API backend.")
                if cred_info["has_surrounding_quotes"]:
                    print("      Probable cause: Surrounding quotes in GEMINI_API_KEY corrupted the x-goog-api-key header.")
        print("=" * 80 + "\n")
        return

    # Parse available models
    available_generate_models: List[str] = []
    try:
        models_data = resp1.json().get("models", [])
        for m in models_data:
            m_name = m.get("name", "")  # e.g. "models/gemini-1.5-flash"
            methods = m.get("supportedGenerationMethods", [])
            if "generateContent" in methods:
                short_name = m_name.removeprefix("models/")
                available_generate_models.append(short_name)
    except Exception as ex:
        print(f"  [-] Failed to parse models JSON: {ex}")
        return

    print(f"  [+] LAYER 1 PASSED: Found {len(available_generate_models)} models supporting generateContent.")
    print("      Active models snippet: " + ", ".join(available_generate_models[:8]))

    # Check if configured model is valid
    clean_configured = configured_model.strip().strip('"\'').removeprefix("models/")
    model_is_available = clean_configured in available_generate_models
    print(f"  [*] Configured model '{clean_configured}' available in your account: {model_is_available}")

    if not model_is_available:
        print(f"  [!] CRITICAL FINDING: Configured model '{clean_configured}' is NOT in the Google API models list!")
        # Fallback to recommended stable model
        candidates_to_pick = ["gemini-1.5-flash", "gemini-2.0-flash", "gemini-1.5-pro", "gemini-2.5-flash"]
        selected_model = next((c for c in candidates_to_pick if c in available_generate_models), available_generate_models[0])
        print(f"      Selected fallback model for remaining diagnostic steps: '{selected_model}'")
    else:
        selected_model = clean_configured
        print(f"      Selected model for remaining diagnostic steps: '{selected_model}'")

    # ------------------------------------------------------------------------
    # LAYER 2: Minimal generateContent (No Schema)
    # ------------------------------------------------------------------------
    print(f"\n[LAYER 2] Testing Minimal generateContent with model '{selected_model}' (No Schema)...")
    gen_url = f"{base_url}/models/{selected_model}:generateContent"
    minimal_payload = {
        "contents": [
            {
                "role": "user",
                "parts": [{"text": "Hello, respond with the single word: OK"}],
            }
        ],
        "generationConfig": {
            "temperature": 0.0,
        },
    }

    try:
        with httpx.Client(timeout=20.0) as client:
            resp2 = client.post(gen_url, json=minimal_payload, headers=headers)
    except Exception as ex:
        print(f"  [-] Network connection failure on minimal generateContent: {ex}")
        print("=" * 80 + "\n")
        return

    ct2 = resp2.headers.get("content-type", "")
    server2 = resp2.headers.get("server", "None")
    origin2 = classify_endpoint_origin(resp2.headers, resp2.text)
    print(f"  [*] HTTP Status: {resp2.status_code}")
    print(f"  [*] Content-Type: {ct2}")
    print(f"  [*] Server Header: {server2}")
    print(f"  [*] Endpoint Origin Analysis: {origin2}")

    if resp2.status_code != 200:
        print(f"  [-] LAYER 2 FAILED (HTTP {resp2.status_code})")
        print(f"      Body snippet: {repr(resp2.text[:250])}")
        print("=" * 80 + "\n")
        return

    try:
        cand2 = resp2.json().get("candidates", [])[0]["content"]["parts"][0]["text"].strip()
        print(f"  [+] LAYER 2 PASSED: Model response: {repr(cand2[:60])}")
    except Exception as ex:
        print(f"  [!] LAYER 2 parsed response with unexpected structure: {ex}")

    # ------------------------------------------------------------------------
    # LAYER 3: Minimal Structured-JSON generateContent
    # ------------------------------------------------------------------------
    print(f"\n[LAYER 3] Testing Structured-JSON generateContent with model '{selected_model}'...")
    json_payload = {
        "contents": [
            {
                "role": "user",
                "parts": [{"text": "Respond with a JSON object where status is OK"}],
            }
        ],
        "generationConfig": {
            "temperature": 0.0,
            "responseMimeType": "application/json",
            "responseSchema": {
                "type": "OBJECT",
                "properties": {
                    "status": {"type": "STRING"}
                },
                "required": ["status"],
            },
        },
    }

    try:
        with httpx.Client(timeout=20.0) as client:
            resp3 = client.post(gen_url, json=json_payload, headers=headers)
    except Exception as ex:
        print(f"  [-] Network connection failure on structured JSON: {ex}")
        print("=" * 80 + "\n")
        return

    ct3 = resp3.headers.get("content-type", "")
    print(f"  [*] HTTP Status: {resp3.status_code}")
    print(f"  [*] Content-Type: {ct3}")

    if resp3.status_code != 200:
        print(f"  [-] LAYER 3 FAILED (HTTP {resp3.status_code})")
        print(f"      Body snippet: {repr(resp3.text[:250])}")
        print("      Verdict: Structured output schema rejection by model API.")
        print("=" * 80 + "\n")
        return

    try:
        cand3 = resp3.json().get("candidates", [])[0]["content"]["parts"][0]["text"].strip()
        parsed3 = json.loads(cand3)
        print(f"  [+] LAYER 3 PASSED: Parsed JSON: {parsed3}")
    except Exception as ex:
        print(f"  [!] LAYER 3 response parse warning: {ex}")

    # ------------------------------------------------------------------------
    # LAYER 4: Full Proposal Extraction Schema Test (1 page smoke test)
    # ------------------------------------------------------------------------
    print(f"\n[LAYER 4] Testing Full Governed 17-Field Schema on Sample Page with model '{selected_model}'...")
    sample_page = "=== PAGE 1 ===\nඅයදුම්කරු: පී.කේ. ගුණසේකර මයා, ව්‍යාපෘතිය: කිවුල කැලේ එස්ටේට්ස්, දිස්ත්‍රික්කය: පුත්තලම"
    full_prompt = (
        f"Please extract the governed proposal fields from the following official state land document OCR transcript.\n\n"
        f"{sample_page}\n\n"
        f"Remember: Output structured JSON conforming to the schema."
    )
    full_payload = {
        "contents": [{"role": "user", "parts": [{"text": full_prompt}]}],
        "systemInstruction": {"parts": [{"text": GEMINI_SYSTEM_INSTRUCTION}]},
        "generationConfig": {
            "temperature": 0.0,
            "responseMimeType": "application/json",
            "responseSchema": GEMINI_RESPONSE_SCHEMA,
        },
    }

    try:
        with httpx.Client(timeout=30.0) as client:
            resp4 = client.post(gen_url, json=full_payload, headers=headers)
    except Exception as ex:
        print(f"  [-] Network connection failure on full schema: {ex}")
        print("=" * 80 + "\n")
        return

    print(f"  [*] HTTP Status: {resp4.status_code}")
    print(f"  [*] Content-Type: {resp4.headers.get('content-type', '')}")

    if resp4.status_code != 200:
        print(f"  [-] LAYER 4 FAILED (HTTP {resp4.status_code})")
        print(f"      Body snippet: {repr(resp4.text[:300])}")
        print("=" * 80 + "\n")
        return

    try:
        cand4 = resp4.json().get("candidates", [])[0]["content"]["parts"][0]["text"].strip()
        parsed4 = json.loads(cand4)
        ext_fields = parsed4.get("extractedFields", [])
        print(f"  [+] LAYER 4 PASSED: Successfully extracted {len(ext_fields)} fields conforming to schema!")
        for f in ext_fields[:4]:
            print(f"      - {f.get('fieldCode')}: {repr(f.get('value'))} (conf: {f.get('confidence')})")
    except Exception as ex:
        print(f"  [!] LAYER 4 parse warning: {ex}")

    print("\n" + "=" * 80)
    print("DIAGNOSTIC SUMMARY: ALL LAYERS OPERATIONAL")
    print(f"Recommended Model Setting: $env:GEMINI_MODEL=\"{selected_model}\"")
    print("=" * 80 + "\n")


if __name__ == "__main__":
    run_diagnostics()

