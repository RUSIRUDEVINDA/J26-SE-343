package api

import (
	"encoding/json"
	"net/http"
	"strings"

	"governance-blockchain-service/internal/anchors"
	"governance-blockchain-service/internal/besu"
)

type Handler struct {
	anchorService *anchors.Service
	ledger        besu.LedgerClient
	networkName   string
	chainID       int64
}

func NewHandler(anchorService *anchors.Service, ledger besu.LedgerClient, networkName string, chainID int64) *Handler {
	return &Handler{
		anchorService: anchorService,
		ledger:        ledger,
		networkName:   networkName,
		chainID:       chainID,
	}
}

// HealthHandler returns microservice and ledger status.
func (h *Handler) HealthHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	connected := h.ledger.IsConnected(r.Context())
	status := "Healthy"
	if !connected {
		status = "Degraded"
	}

	resp := map[string]interface{}{
		"status":          status,
		"network":         h.networkName,
		"chainId":         h.chainID,
		"ledgerConnected": connected,
	}
	writeJSON(w, http.StatusOK, resp)
}

// PostAnchorHandler handles POST /api/v1/anchors.
func (h *Handler) PostAnchorHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req anchors.AnchorRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "Invalid request JSON payload"})
		return
	}

	if strings.TrimSpace(req.AuditRecordID) == "" || strings.TrimSpace(req.RecordHash) == "" {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": "auditRecordId and recordHash are required"})
		return
	}

	resp, err := h.anchorService.AnchorRecord(r.Context(), req)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}

	statusCode := http.StatusOK
	if resp.AnchorStatus == "Anchored" {
		statusCode = http.StatusCreated
	}
	writeJSON(w, statusCode, resp)
}

// AnchorByIDHandler handles GET /api/v1/anchors/{auditRecordId} and POST /api/v1/anchors/{auditRecordId}/verify.
func (h *Handler) AnchorByIDHandler(w http.ResponseWriter, r *http.Request) {
	path := strings.TrimPrefix(r.URL.Path, "/api/v1/anchors/")
	parts := strings.Split(path, "/")

	if len(parts) == 0 || parts[0] == "" {
		http.Error(w, "auditRecordId is required", http.StatusBadRequest)
		return
	}

	recordID := parts[0]

	// Check if this is /verify sub-path
	if len(parts) == 2 && parts[1] == "verify" {
		if r.Method != http.MethodPost {
			http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
			return
		}

		var req anchors.VerifyRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			writeJSON(w, http.StatusBadRequest, map[string]string{"error": "Invalid request JSON payload"})
			return
		}

		if strings.TrimSpace(req.ExpectedHash) == "" {
			writeJSON(w, http.StatusBadRequest, map[string]string{"error": "expectedHash is required"})
			return
		}

		resp, err := h.anchorService.VerifyRecord(r.Context(), recordID, req.ExpectedHash)
		if err != nil {
			writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
			return
		}

		writeJSON(w, http.StatusOK, resp)
		return
	}

	// Normal GET /api/v1/anchors/{auditRecordId}
	if r.Method == http.MethodGet {
		resp, err := h.anchorService.GetReceipt(r.Context(), recordID)
		if err != nil {
			writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
			return
		}

		writeJSON(w, http.StatusOK, resp)
		return
	}

	http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
}

func writeJSON(w http.ResponseWriter, statusCode int, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(statusCode)
	_ = json.NewEncoder(w).Encode(data)
}
