package besu

import (
	"context"
	"crypto/sha256"
	"encoding/hex"
	"errors"
	"fmt"
	"sync"
	"time"
)

var (
	ErrRecordAlreadyAnchoredWithDifferentHash = errors.New("audit record already anchored with differing hash")
	ErrLedgerUnavailable                      = errors.New("blockchain ledger is unavailable")
	ErrRecordNotFound                         = errors.New("audit record anchor not found on ledger")
)

// LedgerClient abstracts ledger interactions (either live Besu node or resilient in-memory mock).
type LedgerClient interface {
	AnchorRecord(ctx context.Context, auditRecordID [32]byte, recordHash [32]byte) (txHash string, blockNumber uint64, anchoredAt time.Time, err error)
	GetRecord(ctx context.Context, auditRecordID [32]byte) (recordHash [32]byte, anchoredAt time.Time, submitter string, exists bool, err error)
	VerifyRecord(ctx context.Context, auditRecordID [32]byte, expectedHash [32]byte) (isMatch bool, anchoredAt time.Time, submitter string, err error)
	IsConnected(ctx context.Context) bool
}

// MockRecord stores on-chain integrity facts.
type MockRecord struct {
	RecordHash [32]byte
	AnchoredAt time.Time
	Submitter  string
	TxHash     string
	BlockNum   uint64
}

// MockLedgerClient provides deterministic, contract-identical behavior for testing and local development.
type MockLedgerClient struct {
	mu           sync.RWMutex
	records      map[[32]byte]MockRecord
	currentBlock uint64
	submitter    string
	offline      bool
}

// NewMockLedgerClient initializes an in-memory ledger client adhering to GovernanceAuditRegistry rules.
func NewMockLedgerClient(submitter string, offline bool) *MockLedgerClient {
	if submitter == "" {
		submitter = "0x8f2a55949038a9610f50fb23b5883af3b4ecb3c3"
	}
	return &MockLedgerClient{
		records:      make(map[[32]byte]MockRecord),
		currentBlock: 100,
		submitter:    submitter,
		offline:      offline,
	}
}

func (m *MockLedgerClient) SetOffline(offline bool) {
	m.mu.Lock()
	defer m.mu.Unlock()
	m.offline = offline
}

func (m *MockLedgerClient) IsConnected(ctx context.Context) bool {
	m.mu.RLock()
	defer m.mu.RUnlock()
	return !m.offline
}

func (m *MockLedgerClient) AnchorRecord(ctx context.Context, auditRecordID [32]byte, recordHash [32]byte) (string, uint64, time.Time, error) {
	m.mu.Lock()
	defer m.mu.Unlock()

	if m.offline {
		return "", 0, time.Time{}, ErrLedgerUnavailable
	}

	existing, found := m.records[auditRecordID]
	if found {
		// Idempotency: identical hash -> succeed idempotently
		if existing.RecordHash == recordHash {
			return existing.TxHash, existing.BlockNum, existing.AnchoredAt, nil
		}
		// Tamper prevention: differing hash -> reject
		return "", 0, time.Time{}, ErrRecordAlreadyAnchoredWithDifferentHash
	}

	m.currentBlock++
	now := time.Now().UTC().Truncate(time.Second)

	// Deterministic mock tx hash
	txBytes := sha256.Sum256([]byte(fmt.Sprintf("%x-%x-%d", auditRecordID, recordHash, m.currentBlock)))
	txHash := "0x" + hex.EncodeToString(txBytes[:])

	record := MockRecord{
		RecordHash: recordHash,
		AnchoredAt: now,
		Submitter:  m.submitter,
		TxHash:     txHash,
		BlockNum:   m.currentBlock,
	}

	m.records[auditRecordID] = record
	return txHash, m.currentBlock, now, nil
}

func (m *MockLedgerClient) GetRecord(ctx context.Context, auditRecordID [32]byte) ([32]byte, time.Time, string, bool, error) {
	m.mu.RLock()
	defer m.mu.RUnlock()

	if m.offline {
		return [32]byte{}, time.Time{}, "", false, ErrLedgerUnavailable
	}

	record, found := m.records[auditRecordID]
	if !found {
		return [32]byte{}, time.Time{}, "", false, nil
	}

	return record.RecordHash, record.AnchoredAt, record.Submitter, true, nil
}

func (m *MockLedgerClient) VerifyRecord(ctx context.Context, auditRecordID [32]byte, expectedHash [32]byte) (bool, time.Time, string, error) {
	m.mu.RLock()
	defer m.mu.RUnlock()

	if m.offline {
		return false, time.Time{}, "", ErrLedgerUnavailable
	}

	record, found := m.records[auditRecordID]
	if !found {
		return false, time.Time{}, "", nil
	}

	isMatch := (record.RecordHash == expectedHash)
	return isMatch, record.AnchoredAt, record.Submitter, nil
}
