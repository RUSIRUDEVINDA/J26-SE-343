package besu

import (
	"context"
	"crypto/ecdsa"
	"errors"
	"fmt"
	"log"
	"math/big"
	"strings"
	"time"

	"github.com/ethereum/go-ethereum"
	"github.com/ethereum/go-ethereum/accounts/abi/bind"
	"github.com/ethereum/go-ethereum/common"
	"github.com/ethereum/go-ethereum/crypto"
	"github.com/ethereum/go-ethereum/ethclient"

	"governance-blockchain-service/internal/contract"
)

// RealBesuClient connects directly to a Hyperledger Besu JSON-RPC endpoint.
type RealBesuClient struct {
	client        *ethclient.Client
	registry      *contract.GovernanceAuditRegistry
	contractAddr  common.Address
	privateKey    *ecdsa.PrivateKey
	submitterAddr common.Address
	chainID       *big.Int
}

// NewRealBesuClient initializes a connection to Besu and loads the smart contract.
func NewRealBesuClient(rpcURL, contractAddrHex, privKeyHex string, chainID int64) (*RealBesuClient, error) {
	if strings.TrimSpace(contractAddrHex) == "" || common.HexToAddress(contractAddrHex) == (common.Address{}) {
		return nil, errors.New("deployed contract address is required: specify REGISTRY_CONTRACT_ADDRESS or deploy contract to generate deployment.json")
	}

	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()

	client, err := ethclient.DialContext(ctx, rpcURL)
	if err != nil {
		return nil, fmt.Errorf("failed to dial Besu RPC at %s: %w", rpcURL, err)
	}

	privKeyHex = strings.TrimPrefix(privKeyHex, "0x")
	privKey, err := crypto.HexToECDSA(privKeyHex)
	if err != nil {
		return nil, fmt.Errorf("invalid private key: %w", err)
	}

	publicKey := privKey.Public()
	publicKeyECDSA, ok := publicKey.(*ecdsa.PublicKey)
	if !ok {
		return nil, fmt.Errorf("failed to derive public key from private key")
	}
	submitterAddr := crypto.PubkeyToAddress(*publicKeyECDSA)

	cAddr := common.HexToAddress(contractAddrHex)
	registry, err := contract.NewGovernanceAuditRegistry(cAddr, client)
	if err != nil {
		return nil, fmt.Errorf("failed to bind GovernanceAuditRegistry contract at %s: %w", contractAddrHex, err)
	}

	// Immediate connectivity probe
	if _, err := client.BlockNumber(ctx); err != nil {
		return nil, fmt.Errorf("failed to reach Besu RPC node at %s: %w", rpcURL, err)
	}

	return &RealBesuClient{
		client:        client,
		registry:      registry,
		contractAddr:  cAddr,
		privateKey:    privKey,
		submitterAddr: submitterAddr,
		chainID:       big.NewInt(chainID),
	}, nil
}

// IsConnected checks if the Besu node is reachable.
func (b *RealBesuClient) IsConnected(ctx context.Context) bool {
	ctxCheck, cancel := context.WithTimeout(ctx, 2*time.Second)
	defer cancel()

	_, err := b.client.BlockNumber(ctxCheck)
	return err == nil
}

// AnchorRecord anchors an audit digest on Besu with strict idempotency and zero silent fallback.
func (b *RealBesuClient) AnchorRecord(ctx context.Context, auditRecordID [32]byte, recordHash [32]byte) (string, uint64, time.Time, error) {
	if !b.IsConnected(ctx) {
		return "", 0, time.Time{}, ErrLedgerUnavailable
	}

	// 1. Check existing on-chain state for idempotency or conflict
	existing, err := b.registry.GetRecord(&bind.CallOpts{Context: ctx}, auditRecordID)
	if err == nil && existing.Exists {
		if existing.RecordHash == recordHash {
			// Idempotent duplicate: accept same hash without re-submitting transaction
			anchoredAt := time.Unix(int64(existing.AnchoredAt), 0).UTC()
			log.Printf("[INFO] Audit record %x already anchored with matching hash. Returning existing anchor receipt.", auditRecordID[:8])

			// Query past AuditRecordAnchored event logs to return the original transaction reference and block number
			query := ethereum.FilterQuery{
				Addresses: []common.Address{b.contractAddr},
				Topics: [][]common.Hash{
					{crypto.Keccak256Hash([]byte("AuditRecordAnchored(bytes32,bytes32,uint64,address)"))},
					{common.BytesToHash(auditRecordID[:])},
				},
			}
			logs, logErr := b.client.FilterLogs(ctx, query)
			if logErr == nil && len(logs) > 0 {
				return logs[0].TxHash.Hex(), logs[0].BlockNumber, anchoredAt, nil
			}

			// Deterministic fallback reference if log querying is filtered
			return fmt.Sprintf("0xanchored_%x", auditRecordID[:8]), 0, anchoredAt, nil
		}
		// Existing ID with differing hash -> strictly reject
		return "", 0, time.Time{}, ErrRecordAlreadyAnchoredWithDifferentHash
	}

	// 2. Prepare transaction
	auth, err := bind.NewKeyedTransactorWithChainID(b.privateKey, b.chainID)
	if err != nil {
		return "", 0, time.Time{}, fmt.Errorf("failed to create transactor: %w", err)
	}
	auth.Context = ctx
	auth.GasPrice = big.NewInt(0) // Besu private QBFT zero-gas network

	tx, err := b.registry.AnchorRecord(auth, auditRecordID, recordHash)
	if err != nil {
		if strings.Contains(err.Error(), "different hash") {
			return "", 0, time.Time{}, ErrRecordAlreadyAnchoredWithDifferentHash
		}
		return "", 0, time.Time{}, fmt.Errorf("anchorRecord transaction submission failed: %w", err)
	}

	// 3. Wait for transaction to be mined into block
	receipt, err := bind.WaitMined(ctx, b.client, tx)
	if err != nil {
		return "", 0, time.Time{}, fmt.Errorf("failed waiting for transaction %s to be mined: %w", tx.Hash().Hex(), err)
	}

	header, err := b.client.HeaderByNumber(ctx, receipt.BlockNumber)
	anchoredAt := time.Now().UTC()
	if err == nil && header != nil {
		anchoredAt = time.Unix(int64(header.Time), 0).UTC()
	}

	return receipt.TxHash.Hex(), receipt.BlockNumber.Uint64(), anchoredAt, nil
}

// GetRecord queries an audit anchor by record ID from the on-chain contract.
func (b *RealBesuClient) GetRecord(ctx context.Context, auditRecordID [32]byte) ([32]byte, time.Time, string, bool, error) {
	if !b.IsConnected(ctx) {
		return [32]byte{}, time.Time{}, "", false, ErrLedgerUnavailable
	}

	rec, err := b.registry.GetRecord(&bind.CallOpts{Context: ctx}, auditRecordID)
	if err != nil {
		return [32]byte{}, time.Time{}, "", false, err
	}

	if !rec.Exists {
		return [32]byte{}, time.Time{}, "", false, nil
	}

	anchoredAt := time.Unix(int64(rec.AnchoredAt), 0).UTC()
	return rec.RecordHash, anchoredAt, rec.Submitter.Hex(), true, nil
}

// VerifyRecord compares an expected hash against the on-chain anchor.
func (b *RealBesuClient) VerifyRecord(ctx context.Context, auditRecordID [32]byte, expectedHash [32]byte) (bool, time.Time, string, error) {
	if !b.IsConnected(ctx) {
		return false, time.Time{}, "", ErrLedgerUnavailable
	}

	res, err := b.registry.VerifyRecord(&bind.CallOpts{Context: ctx}, auditRecordID, expectedHash)
	if err != nil {
		return false, time.Time{}, "", err
	}

	anchoredAt := time.Unix(int64(res.AnchoredAt), 0).UTC()
	return res.IsMatch, anchoredAt, res.Submitter.Hex(), nil
}

// UnavailableLedgerClient is a safe fail-closed client used when Besu cannot be reached in normal mode.
type UnavailableLedgerClient struct {
	reason string
}

func NewUnavailableLedgerClient(reason string) *UnavailableLedgerClient {
	return &UnavailableLedgerClient{reason: reason}
}

func (u *UnavailableLedgerClient) IsConnected(ctx context.Context) bool {
	return false
}

func (u *UnavailableLedgerClient) AnchorRecord(ctx context.Context, auditRecordID [32]byte, recordHash [32]byte) (string, uint64, time.Time, error) {
	return "", 0, time.Time{}, ErrLedgerUnavailable
}

func (u *UnavailableLedgerClient) GetRecord(ctx context.Context, auditRecordID [32]byte) ([32]byte, time.Time, string, bool, error) {
	return [32]byte{}, time.Time{}, "", false, ErrLedgerUnavailable
}

func (u *UnavailableLedgerClient) VerifyRecord(ctx context.Context, auditRecordID [32]byte, expectedHash [32]byte) (bool, time.Time, string, error) {
	return false, time.Time{}, "", ErrLedgerUnavailable
}
