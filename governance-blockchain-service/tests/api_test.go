package tests

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"

	"governance-blockchain-service/internal/anchors"
	"governance-blockchain-service/internal/api"
	"governance-blockchain-service/internal/besu"
)

func setupTestServer() (http.Handler, *besu.MockLedgerClient) {
	mockLedger := besu.NewMockLedgerClient("", false)
	anchorService := anchors.NewService(mockLedger)
	handler := api.NewHandler(anchorService, mockLedger, "Hyperledger Besu (QBFT)", 1337)
	router := api.SetupRouter(handler)
	return router, mockLedger
}

func TestHealthEndpoint(t *testing.T) {
	router, mockLedger := setupTestServer()

	// 1. Healthy
	req := httptest.NewRequest(http.MethodGet, "/health", nil)
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	if w.Code != http.StatusOK {
		t.Fatalf("expected status 200, got %d", w.Code)
	}

	var resp map[string]interface{}
	if err := json.Unmarshal(w.Body.Bytes(), &resp); err != nil {
		t.Fatalf("failed to parse JSON: %v", err)
	}
	if resp["status"] != "Healthy" {
		t.Errorf("expected status Healthy, got %v", resp["status"])
	}

	// 2. Degraded when offline
	mockLedger.SetOffline(true)
	req2 := httptest.NewRequest(http.MethodGet, "/health", nil)
	w2 := httptest.NewRecorder()
	router.ServeHTTP(w2, req2)

	var resp2 map[string]interface{}
	_ = json.Unmarshal(w2.Body.Bytes(), &resp2)
	if resp2["status"] != "Degraded" {
		t.Errorf("expected status Degraded, got %v", resp2["status"])
	}
}

func TestAnchorFlow(t *testing.T) {
	router, _ := setupTestServer()

	testRecordID := "11111111-2222-3333-4444-555555555555"
	testHashA := "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
	testHashB := "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"

	// 1. Anchor new record
	anchorPayload, _ := json.Marshal(anchors.AnchorRequest{
		AuditRecordID: testRecordID,
		RecordHash:    testHashA,
		EngineType:    "RegulatoryCompliance",
		RecordVersion: "AUDIT-V1",
	})

	req := httptest.NewRequest(http.MethodPost, "/api/v1/anchors", bytes.NewReader(anchorPayload))
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	if w.Code != http.StatusCreated {
		t.Fatalf("expected 201 Created, got %d, body: %s", w.Code, w.Body.String())
	}

	var anchorResp anchors.AnchorResponse
	_ = json.Unmarshal(w.Body.Bytes(), &anchorResp)
	if anchorResp.AnchorStatus != "Anchored" {
		t.Errorf("expected AnchorStatus Anchored, got %s", anchorResp.AnchorStatus)
	}
	if anchorResp.TransactionReference == "" {
		t.Errorf("expected valid transaction reference")
	}

	// 2. Idempotent repeat anchor with identical hash
	req2 := httptest.NewRequest(http.MethodPost, "/api/v1/anchors", bytes.NewReader(anchorPayload))
	w2 := httptest.NewRecorder()
	router.ServeHTTP(w2, req2)

	if w2.Code != http.StatusCreated && w2.Code != http.StatusOK {
		t.Fatalf("expected 200/201 on idempotent repeat, got %d", w2.Code)
	}

	var repeatResp anchors.AnchorResponse
	_ = json.Unmarshal(w2.Body.Bytes(), &repeatResp)
	if repeatResp.AnchorStatus != "Anchored" {
		t.Errorf("expected AnchorStatus Anchored on idempotent repeat, got %s", repeatResp.AnchorStatus)
	}
	if repeatResp.TransactionReference != anchorResp.TransactionReference {
		t.Errorf("expected same transaction reference on idempotent repeat, got %s vs %s",
			repeatResp.TransactionReference, anchorResp.TransactionReference)
	}
	if *repeatResp.BlockNumber != *anchorResp.BlockNumber {
		t.Errorf("expected same block number on idempotent repeat, got %d vs %d",
			*repeatResp.BlockNumber, *anchorResp.BlockNumber)
	}

	// 3. Reject re-anchor with differing hash
	tamperPayload, _ := json.Marshal(anchors.AnchorRequest{
		AuditRecordID: testRecordID,
		RecordHash:    testHashB,
		EngineType:    "RegulatoryCompliance",
		RecordVersion: "AUDIT-V1",
	})
	req3 := httptest.NewRequest(http.MethodPost, "/api/v1/anchors", bytes.NewReader(tamperPayload))
	w3 := httptest.NewRecorder()
	router.ServeHTTP(w3, req3)

	var tamperResp anchors.AnchorResponse
	_ = json.Unmarshal(w3.Body.Bytes(), &tamperResp)
	if tamperResp.AnchorStatus != "Failed" {
		t.Errorf("expected Failed status on differing hash tamper, got %s", tamperResp.AnchorStatus)
	}

	// 4. Retrieve receipt via GET
	req4 := httptest.NewRequest(http.MethodGet, "/api/v1/anchors/"+testRecordID, nil)
	w4 := httptest.NewRecorder()
	router.ServeHTTP(w4, req4)

	if w4.Code != http.StatusOK {
		t.Fatalf("expected 200 OK on GET receipt, got %d", w4.Code)
	}

	var receiptResp anchors.AnchorResponse
	_ = json.Unmarshal(w4.Body.Bytes(), &receiptResp)
	if receiptResp.AnchorStatus != "Anchored" {
		t.Errorf("expected Anchored, got %s", receiptResp.AnchorStatus)
	}

	// 5. Verify matching digest
	verifyPayloadA, _ := json.Marshal(anchors.VerifyRequest{ExpectedHash: testHashA})
	req5 := httptest.NewRequest(http.MethodPost, "/api/v1/anchors/"+testRecordID+"/verify", bytes.NewReader(verifyPayloadA))
	w5 := httptest.NewRecorder()
	router.ServeHTTP(w5, req5)

	var verifyRespA anchors.VerifyResponse
	_ = json.Unmarshal(w5.Body.Bytes(), &verifyRespA)
	if verifyRespA.VerificationStatus != "Match" {
		t.Errorf("expected VerificationStatus Match, got %s", verifyRespA.VerificationStatus)
	}

	// 6. Verify mismatching digest
	verifyPayloadB, _ := json.Marshal(anchors.VerifyRequest{ExpectedHash: testHashB})
	req6 := httptest.NewRequest(http.MethodPost, "/api/v1/anchors/"+testRecordID+"/verify", bytes.NewReader(verifyPayloadB))
	w6 := httptest.NewRecorder()
	router.ServeHTTP(w6, req6)

	var verifyRespB anchors.VerifyResponse
	_ = json.Unmarshal(w6.Body.Bytes(), &verifyRespB)
	if verifyRespB.VerificationStatus != "Mismatch" {
		t.Errorf("expected VerificationStatus Mismatch, got %s", verifyRespB.VerificationStatus)
	}
}

func TestValidationErrors(t *testing.T) {
	router, _ := setupTestServer()

	// Invalid UUID
	payload, _ := json.Marshal(anchors.AnchorRequest{
		AuditRecordID: "not-a-uuid",
		RecordHash:    "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
	})
	req := httptest.NewRequest(http.MethodPost, "/api/v1/anchors", bytes.NewReader(payload))
	w := httptest.NewRecorder()
	router.ServeHTTP(w, req)

	if w.Code != http.StatusBadRequest {
		t.Errorf("expected 400 Bad Request for invalid UUID, got %d", w.Code)
	}

	// Invalid Hash length
	payload2, _ := json.Marshal(anchors.AnchorRequest{
		AuditRecordID: "11111111-2222-3333-4444-555555555555",
		RecordHash:    "short-hash",
	})
	req2 := httptest.NewRequest(http.MethodPost, "/api/v1/anchors", bytes.NewReader(payload2))
	w2 := httptest.NewRecorder()
	router.ServeHTTP(w2, req2)

	if w2.Code != http.StatusBadRequest {
		t.Errorf("expected 400 Bad Request for short hash, got %d", w2.Code)
	}
}

func TestTrueIdempotency_SameRecordAndHash_DoesNotCreateNewTransaction(t *testing.T) {
	router, _ := setupTestServer()

	recordID := "22222222-3333-4444-5555-666666666666"
	hashA := "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
	hashB := "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"

	// 1. First anchor call -> 201 Created
	payloadA, _ := json.Marshal(anchors.AnchorRequest{
		AuditRecordID: recordID,
		RecordHash:    hashA,
		EngineType:    "RegulatoryCompliance",
		RecordVersion: "AUDIT-V1",
	})
	req1 := httptest.NewRequest(http.MethodPost, "/api/v1/anchors", bytes.NewReader(payloadA))
	w1 := httptest.NewRecorder()
	router.ServeHTTP(w1, req1)

	if w1.Code != http.StatusCreated {
		t.Fatalf("first anchor expected 201 Created, got %d", w1.Code)
	}
	var resp1 anchors.AnchorResponse
	_ = json.Unmarshal(w1.Body.Bytes(), &resp1)

	// 2. Repeat anchor with identical recordID + identical hash
	req2 := httptest.NewRequest(http.MethodPost, "/api/v1/anchors", bytes.NewReader(payloadA))
	w2 := httptest.NewRecorder()
	router.ServeHTTP(w2, req2)

	var resp2 anchors.AnchorResponse
	_ = json.Unmarshal(w2.Body.Bytes(), &resp2)

	if resp2.AnchorStatus != "Anchored" {
		t.Errorf("expected Anchored, got %s", resp2.AnchorStatus)
	}
	if resp2.TransactionReference != resp1.TransactionReference {
		t.Errorf("idempotency violation: new tx reference %s, expected %s", resp2.TransactionReference, resp1.TransactionReference)
	}
	if *resp2.BlockNumber != *resp1.BlockNumber {
		t.Errorf("idempotency violation: block number advanced %d, expected %d", *resp2.BlockNumber, *resp1.BlockNumber)
	}

	// 3. Repeat anchor with same recordID + DIFFERENT hash -> MUST reject
	payloadB, _ := json.Marshal(anchors.AnchorRequest{
		AuditRecordID: recordID,
		RecordHash:    hashB,
		EngineType:    "RegulatoryCompliance",
		RecordVersion: "AUDIT-V1",
	})
	req3 := httptest.NewRequest(http.MethodPost, "/api/v1/anchors", bytes.NewReader(payloadB))
	w3 := httptest.NewRecorder()
	router.ServeHTTP(w3, req3)

	var resp3 anchors.AnchorResponse
	_ = json.Unmarshal(w3.Body.Bytes(), &resp3)

	if resp3.AnchorStatus != "Failed" {
		t.Errorf("tamper protection violation: expected Failed status for differing hash, got %s", resp3.AnchorStatus)
	}
}
