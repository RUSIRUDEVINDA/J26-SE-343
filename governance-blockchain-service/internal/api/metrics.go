package api

import (
	"fmt"
	"net/http"
	"sort"
	"strings"
	"sync"
)

// Metrics contains only bounded operational labels. Audit IDs and hashes are never recorded.
type Metrics struct {
	mu                   sync.RWMutex
	anchorOutcomes       map[string]uint64
	verificationOutcomes map[string]uint64
}

func NewMetrics() *Metrics {
	return &Metrics{
		anchorOutcomes:       make(map[string]uint64),
		verificationOutcomes: make(map[string]uint64),
	}
}

func (m *Metrics) RecordAnchor(status string) {
	m.mu.Lock()
	defer m.mu.Unlock()
	m.anchorOutcomes[boundedLabel(status, "Unknown")]++
}

func (m *Metrics) RecordVerification(status string) {
	m.mu.Lock()
	defer m.mu.Unlock()
	m.verificationOutcomes[boundedLabel(status, "Unknown")]++
}

func (h *Handler) MetricsHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	connected := h.ledger.IsConnected(r.Context())
	connectedValue := 0
	if connected {
		connectedValue = 1
	}

	w.Header().Set("Content-Type", "text/plain; version=0.0.4; charset=utf-8")
	fmt.Fprintln(w, "# HELP state_land_governance_blockchain_info Static blockchain gateway configuration.")
	fmt.Fprintln(w, "# TYPE state_land_governance_blockchain_info gauge")
	fmt.Fprintf(w, "state_land_governance_blockchain_info{mode=\"%s\",network=\"%s\",chain_id=\"%d\"} 1\n",
		prometheusEscape(h.blockchainMode), prometheusEscape(h.networkName), h.chainID)
	fmt.Fprintln(w, "# HELP state_land_governance_blockchain_ledger_connected Whether the gateway can reach its configured ledger.")
	fmt.Fprintln(w, "# TYPE state_land_governance_blockchain_ledger_connected gauge")
	fmt.Fprintf(w, "state_land_governance_blockchain_ledger_connected %d\n", connectedValue)

	h.metrics.mu.RLock()
	defer h.metrics.mu.RUnlock()
	writeCounterMap(w, "state_land_governance_blockchain_anchor_requests_total", "Blockchain anchor request outcomes.", "status", h.metrics.anchorOutcomes)
	writeCounterMap(w, "state_land_governance_blockchain_verifications_total", "Blockchain verification outcomes.", "status", h.metrics.verificationOutcomes)
}

func writeCounterMap(w http.ResponseWriter, name, help, labelName string, values map[string]uint64) {
	fmt.Fprintf(w, "# HELP %s %s\n", name, help)
	fmt.Fprintf(w, "# TYPE %s counter\n", name)
	keys := make([]string, 0, len(values))
	for key := range values {
		keys = append(keys, key)
	}
	sort.Strings(keys)
	for _, key := range keys {
		fmt.Fprintf(w, "%s{%s=\"%s\"} %d\n", name, labelName, prometheusEscape(key), values[key])
	}
}

func boundedLabel(value, fallback string) string {
	trimmed := strings.TrimSpace(value)
	if trimmed == "" {
		return fallback
	}
	if len(trimmed) > 80 {
		return trimmed[:80]
	}
	return trimmed
}

func prometheusEscape(value string) string {
	value = strings.ReplaceAll(value, "\\", "\\\\")
	value = strings.ReplaceAll(value, "\"", "\\\"")
	return strings.ReplaceAll(value, "\n", "\\n")
}
