# Live viva monitoring

This stack demonstrates the real execution path without storing proposal text, case IDs,
prompts, credentials, audit hashes, or private keys in Prometheus.

## Start

Start the ASP.NET API (`5264`), GovernanceIntelligence ML service (`8104`), blockchain
gateway (`8550`), and frontend (`3000`) using the project commands. Then run:

```powershell
.\monitoring\start-monitoring.ps1
```

Open:

- Grafana: <http://localhost:3001> (local default: `admin` / `admin`)
- Viva dashboard: <http://localhost:3001/d/state-land-viva-e2e/e61a9cb>
- Prometheus targets: <http://localhost:9090/targets>

Grafana provisions **Viva Demonstration / State Land Governance — Live End-to-End
Evidence** automatically. Run a proposal in the application and allow one or two scrape
intervals (5–10 seconds) for the counters to update.

## What constitutes live evidence

- **Qwen Applied (Live Groq)** increments only when the hosted response passed validation
  and was used. Any fail-closed result increments **Deterministic Fallbacks** instead.
- **Real Besu Mode** is green only when the Go gateway reports `mode="besu"`.
- **Ledger Connected** comes from a live ledger connectivity check on every scrape.
- **Anchored on Besu** increments only from the gateway's actual anchor response.
- **Integrity Matches** increments only after an explicit verification call returns `Match`.
- Five independently scraped Besu targets demonstrate the four validators and RPC node.

The counters reset when their application process restarts. Prometheus retains the sampled
history for seven days by default.

## Stop

```powershell
.\monitoring\stop-monitoring.ps1
```

To stop Besu separately, use its existing Compose file. Named volumes are intentionally
preserved so stopping monitoring does not delete metrics or blockchain data.
