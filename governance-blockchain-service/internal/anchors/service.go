package anchors

import (
	"context"
	"encoding/hex"
	"errors"
	"fmt"
	"strings"
	"time"

	"governance-blockchain-service/internal/besu"
)

var (
	ErrInvalidRecordID = errors.New("auditRecordId must be a valid UUID")
	ErrInvalidHash     = errors.New("recordHash must be a 64-character hex string (SHA-256)")
)

// AnchorRequest represents payload received from C#.
type AnchorRequest struct {
	AuditRecordID string `json:"auditRecordId"`
	RecordHash    string `json:"recordHash"`
	EngineType    string `json:"engineType"`
	RecordVersion string `json:"recordVersion"`
}

// AnchorResponse represents the receipt payload returned to C#.
type AnchorResponse struct {
	AuditRecordID        string     `json:"auditRecordId"`
	RecordHash           string     `json:"recordHash"`
	AnchorStatus         string     `json:"anchorStatus"` // Anchored, Failed, Unavailable
	TransactionReference string     `json:"transactionReference,omitempty"`
	BlockNumber          *int64     `json:"blockNumber,omitempty"`
	AnchoredAtUtc        *time.Time `json:"anchoredAtUtc,omitempty"`
	ErrorMessage         string     `json:"errorMessage,omitempty"`
}

// VerifyRequest represents verification query payload.
type VerifyRequest struct {
	ExpectedHash string `json:"expectedHash"`
}

// VerifyResponse represents verification result returned to C#.
type VerifyResponse struct {
	AuditRecordID        string     `json:"auditRecordId"`
	CurrentHash          string     `json:"currentHash"`
	AnchoredHash         string     `json:"anchoredHash,omitempty"`
	VerificationStatus   string     `json:"verificationStatus"` // Match, Mismatch, NotAnchored, Unavailable
	TransactionReference string     `json:"transactionReference,omitempty"`
	AnchoredAtUtc        *time.Time `json:"anchoredAtUtc,omitempty"`
	VerifiedAtUtc        time.Time  `json:"verifiedAtUtc"`
	Explanation          string     `json:"explanation"`
}

// Service orchestrates ledger anchoring and verification calls.
type Service struct {
	ledger besu.LedgerClient
}

// NewService instantiates a new Service.
func NewService(ledger besu.LedgerClient) *Service {
	return &Service{ledger: ledger}
}

// AnchorRecord processes an anchor request and submits to the ledger.
func (s *Service) AnchorRecord(ctx context.Context, req AnchorRequest) (*AnchorResponse, error) {
	recordIDBytes, err := parseRecordID(req.AuditRecordID)
	if err != nil {
		return nil, ErrInvalidRecordID
	}

	hashBytes, err := parseHash(req.RecordHash)
	if err != nil {
		return nil, ErrInvalidHash
	}

	txHash, blockNum, anchoredAt, err := s.ledger.AnchorRecord(ctx, recordIDBytes, hashBytes)
	if err != nil {
		if errors.Is(err, besu.ErrLedgerUnavailable) {
			return &AnchorResponse{
				AuditRecordID: req.AuditRecordID,
				RecordHash:    req.RecordHash,
				AnchorStatus:  "Unavailable",
				ErrorMessage:  "Blockchain ledger is temporarily unreachable.",
			}, nil
		}
		if errors.Is(err, besu.ErrRecordAlreadyAnchoredWithDifferentHash) {
			return &AnchorResponse{
				AuditRecordID: req.AuditRecordID,
				RecordHash:    req.RecordHash,
				AnchorStatus:  "Failed",
				ErrorMessage:  "Tamper protection: audit record ID was previously anchored with a different hash.",
			}, nil
		}
		return &AnchorResponse{
			AuditRecordID: req.AuditRecordID,
			RecordHash:    req.RecordHash,
			AnchorStatus:  "Failed",
			ErrorMessage:  fmt.Sprintf("Ledger transaction failed: %v", err),
		}, nil
	}

	bNum := int64(blockNum)
	return &AnchorResponse{
		AuditRecordID:        req.AuditRecordID,
		RecordHash:           req.RecordHash,
		AnchorStatus:         "Anchored",
		TransactionReference: txHash,
		BlockNumber:          &bNum,
		AnchoredAtUtc:        &anchoredAt,
	}, nil
}

// GetReceipt retrieves an existing anchor record from the ledger.
func (s *Service) GetReceipt(ctx context.Context, recordIDStr string) (*AnchorResponse, error) {
	recordIDBytes, err := parseRecordID(recordIDStr)
	if err != nil {
		return nil, ErrInvalidRecordID
	}

	recordHash, anchoredAt, _, exists, err := s.ledger.GetRecord(ctx, recordIDBytes)
	if err != nil {
		if errors.Is(err, besu.ErrLedgerUnavailable) {
			return &AnchorResponse{
				AuditRecordID: recordIDStr,
				AnchorStatus:  "Unavailable",
				ErrorMessage:  "Blockchain ledger is temporarily unreachable.",
			}, nil
		}
		return nil, err
	}

	if !exists {
		return &AnchorResponse{
			AuditRecordID: recordIDStr,
			AnchorStatus:  "NotAnchored",
			ErrorMessage:  "No anchor found on blockchain ledger for this record ID.",
		}, nil
	}

	hashHex := hex.EncodeToString(recordHash[:])
	return &AnchorResponse{
		AuditRecordID: recordIDStr,
		RecordHash:    hashHex,
		AnchorStatus:  "Anchored",
		AnchoredAtUtc: &anchoredAt,
	}, nil
}

// VerifyRecord verifies an expected hash against the ledger.
func (s *Service) VerifyRecord(ctx context.Context, recordIDStr string, expectedHashStr string) (*VerifyResponse, error) {
	recordIDBytes, err := parseRecordID(recordIDStr)
	if err != nil {
		return nil, ErrInvalidRecordID
	}

	expectedHashBytes, err := parseHash(expectedHashStr)
	if err != nil {
		return nil, ErrInvalidHash
	}

	now := time.Now().UTC()

	isMatch, anchoredAt, _, err := s.ledger.VerifyRecord(ctx, recordIDBytes, expectedHashBytes)
	if err != nil {
		if errors.Is(err, besu.ErrLedgerUnavailable) {
			return &VerifyResponse{
				AuditRecordID:      recordIDStr,
				CurrentHash:        expectedHashStr,
				VerificationStatus: "Unavailable",
				VerifiedAtUtc:      now,
				Explanation:        "Ledger verification service or network is temporarily unreachable.",
			}, nil
		}
		return nil, err
	}

	if anchoredAt.IsZero() {
		return &VerifyResponse{
			AuditRecordID:      recordIDStr,
			CurrentHash:        expectedHashStr,
			VerificationStatus: "NotAnchored",
			VerifiedAtUtc:      now,
			Explanation:        "This governance record has not been anchored onto the blockchain ledger.",
		}, nil
	}

	// Fetch anchored hash for display
	anchoredHashBytes, _, _, _, _ := s.ledger.GetRecord(ctx, recordIDBytes)
	anchoredHashHex := hex.EncodeToString(anchoredHashBytes[:])

	if isMatch {
		return &VerifyResponse{
			AuditRecordID:      recordIDStr,
			CurrentHash:        expectedHashStr,
			AnchoredHash:       anchoredHashHex,
			VerificationStatus: "Match",
			AnchoredAtUtc:      &anchoredAt,
			VerifiedAtUtc:      now,
			Explanation:        "The current off-chain record digest matches the anchored blockchain ledger record.",
		}, nil
	}

	return &VerifyResponse{
		AuditRecordID:      recordIDStr,
		CurrentHash:        expectedHashStr,
		AnchoredHash:       anchoredHashHex,
		VerificationStatus: "Mismatch",
		AnchoredAtUtc:      &anchoredAt,
		VerifiedAtUtc:      now,
		Explanation:        "The current off-chain record does not match the anchored record version.",
	}, nil
}

// parseRecordID converts a UUID string into [32]byte.
func parseRecordID(idStr string) ([32]byte, error) {
	clean := strings.ReplaceAll(strings.ToLower(strings.TrimSpace(idStr)), "-", "")
	if len(clean) != 32 {
		return [32]byte{}, fmt.Errorf("invalid UUID length: %s", idStr)
	}

	var res [32]byte
	decoded, err := hex.DecodeString(clean)
	if err != nil {
		return [32]byte{}, err
	}
	copy(res[16:], decoded) // store 16-byte UUID in right-aligned bytes32
	return res, nil
}

// parseHash converts a 64-character SHA-256 hex string into [32]byte.
func parseHash(hashStr string) ([32]byte, error) {
	clean := strings.TrimPrefix(strings.ToLower(strings.TrimSpace(hashStr)), "0x")
	if len(clean) != 64 {
		return [32]byte{}, fmt.Errorf("invalid hash length: %d (expected 64)", len(clean))
	}

	var res [32]byte
	decoded, err := hex.DecodeString(clean)
	if err != nil {
		return [32]byte{}, err
	}
	copy(res[:], decoded)
	return res, nil
}
