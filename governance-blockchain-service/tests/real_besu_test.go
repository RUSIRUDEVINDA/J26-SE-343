package tests

import (
	"context"
	"crypto/rand"
	"crypto/sha256"
	"os"
	"testing"
	"time"

	"governance-blockchain-service/internal/besu"
	"governance-blockchain-service/internal/config"
)

func TestRealBesuLiveIntegration(t *testing.T) {
	rpcURL := os.Getenv("BESU_RPC_URL")
	if rpcURL == "" {
		rpcURL = "http://127.0.0.1:8545"
	}

	cfg := config.Load()
	if cfg.ContractAddress == "" {
		t.Skip("No contract address configured. Skipping live Besu integration test.")
	}

	t.Logf("Connecting to live Besu at %s with contract %s...", rpcURL, cfg.ContractAddress)
	client, err := besu.NewRealBesuClient(rpcURL, cfg.ContractAddress, cfg.PrivateKeyHex, cfg.ChainID)
	if err != nil {
		t.Skipf("Live Besu node not reachable or client init failed (%v). Skipping live test.", err)
	}

	ctx, cancel := context.WithTimeout(context.Background(), 20*time.Second)
	defer cancel()

	if !client.IsConnected(ctx) {
		t.Skipf("Besu RPC node is not reachable at %s. Skipping live test.", rpcURL)
	}

	// 1. Generate test IDs and digests
	var auditRecordID [32]byte
	_, _ = rand.Read(auditRecordID[:])

	recordDigest := sha256.Sum256([]byte("AUDIT-V1|canonical-test-payload-sample"))

	// Test A: Anchor new record
	txHash, blockNum, anchoredAt, err := client.AnchorRecord(ctx, auditRecordID, recordDigest)
	if err != nil {
		t.Fatalf("AnchorRecord failed: %v", err)
	}
	t.Logf("Anchored successfully! Tx: %s, Block: %d, At: %s", txHash, blockNum, anchoredAt)
	if txHash == "" {
		t.Errorf("Expected non-empty transaction hash")
	}
	if blockNum == 0 {
		t.Errorf("Expected non-zero block number")
	}

	// Test B: Retrieve anchored record
	retrievedHash, recAnchoredAt, submitter, exists, err := client.GetRecord(ctx, auditRecordID)
	if err != nil {
		t.Fatalf("GetRecord failed: %v", err)
	}
	if !exists {
		t.Fatalf("Expected record to exist on-chain")
	}
	if retrievedHash != recordDigest {
		t.Fatalf("Expected retrieved hash %x, got %x", recordDigest, retrievedHash)
	}
	t.Logf("Retrieved record: Hash: %x, Submitter: %s, At: %s", retrievedHash, submitter, recAnchoredAt)

	// Test C: Verify identical record returns Match
	isMatch, _, _, err := client.VerifyRecord(ctx, auditRecordID, recordDigest)
	if err != nil {
		t.Fatalf("VerifyRecord failed: %v", err)
	}
	if !isMatch {
		t.Errorf("Expected isMatch=true for identical hash")
	}
	t.Logf("Verification with identical hash: Match = %v", isMatch)

	// Test D: Verify differing record returns Mismatch
	alteredDigest := sha256.Sum256([]byte("AUDIT-V1|tampered-modified-payload"))
	isMatchAltered, _, _, err := client.VerifyRecord(ctx, auditRecordID, alteredDigest)
	if err != nil {
		t.Fatalf("VerifyRecord with altered hash failed: %v", err)
	}
	if isMatchAltered {
		t.Errorf("Expected isMatch=false for altered hash")
	}
	t.Logf("Verification with altered hash: Match = %v (Mismatch detected neutral)", isMatchAltered)

	// Test E: Resubmit same ID + same hash (idempotent duplicate)
	idempotentTx, _, _, err := client.AnchorRecord(ctx, auditRecordID, recordDigest)
	if err != nil {
		t.Fatalf("Idempotent re-anchor failed: %v", err)
	}
	t.Logf("Idempotent anchor succeeded without conflict! Ref: %s", idempotentTx)

	// Test F: Resubmit same ID + different hash (rejection)
	_, _, _, errDiff := client.AnchorRecord(ctx, auditRecordID, alteredDigest)
	if errDiff == nil {
		t.Fatalf("Expected error when re-anchoring existing ID with differing hash, but got nil")
	}
	t.Logf("Conflicting anchor rejected as expected: %v", errDiff)
}

func TestExplicitMockModeSafety(t *testing.T) {
	// Normal mode with invalid URL must NOT silently fall back to mock
	dummyContractAddr := "0x0000000000000000000000000000000000000001"
	_, err := besu.NewRealBesuClient("http://127.0.0.1:9999", dummyContractAddr, "8f2a55949038a9610f50fb23b5883af3b4ecb3c3bb792cbcefbd1542c692be63", 1337)
	if err == nil {
		t.Errorf("Expected error connecting to non-existent Besu node in normal mode")
	}

	unavailableClient := besu.NewUnavailableLedgerClient("connection refused")
	var dummyID [32]byte
	_, _, _, errAnchor := unavailableClient.AnchorRecord(context.Background(), dummyID, dummyID)
	if errAnchor != besu.ErrLedgerUnavailable {
		t.Errorf("Expected ErrLedgerUnavailable, got %v", errAnchor)
	}
}
