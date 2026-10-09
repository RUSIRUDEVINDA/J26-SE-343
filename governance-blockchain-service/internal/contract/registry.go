package contract

import (
	"strings"

	"github.com/ethereum/go-ethereum/accounts/abi"
	"github.com/ethereum/go-ethereum/accounts/abi/bind"
	"github.com/ethereum/go-ethereum/common"
	"github.com/ethereum/go-ethereum/core/types"
)

// GovernanceAuditRegistryMetaData contains the compiled ABI from Hardhat.
const GovernanceAuditRegistryABI = `[
	{"inputs":[],"stateMutability":"nonpayable","type":"constructor"},
	{"anonymous":false,"inputs":[{"indexed":true,"internalType":"bytes32","name":"auditRecordId","type":"bytes32"},{"indexed":true,"internalType":"bytes32","name":"recordHash","type":"bytes32"},{"indexed":false,"internalType":"uint64","name":"anchoredAt","type":"uint64"},{"indexed":true,"internalType":"address","name":"submitter","type":"address"}],"name":"AuditRecordAnchored","type":"event"},
	{"inputs":[{"internalType":"bytes32","name":"auditRecordId","type":"bytes32"},{"internalType":"bytes32","name":"recordHash","type":"bytes32"}],"name":"anchorRecord","outputs":[{"internalType":"bool","name":"success","type":"bool"}],"stateMutability":"nonpayable","type":"function"},
	{"inputs":[{"internalType":"address","name":"submitter","type":"address"}],"name":"authorizeSubmitter","outputs":[],"stateMutability":"nonpayable","type":"function"},
	{"inputs":[{"internalType":"bytes32","name":"auditRecordId","type":"bytes32"}],"name":"getRecord","outputs":[{"internalType":"bytes32","name":"recordHash","type":"bytes32"},{"internalType":"uint64","name":"anchoredAt","type":"uint64"},{"internalType":"address","name":"submitter","type":"address"},{"internalType":"bool","name":"exists","type":"bool"}],"stateMutability":"view","type":"function"},
	{"inputs":[],"name":"owner","outputs":[{"internalType":"address","name":"","type":"address"}],"stateMutability":"view","type":"function"},
	{"inputs":[{"internalType":"address","name":"submitter","type":"address"}],"name":"revokeSubmitter","outputs":[],"stateMutability":"nonpayable","type":"function"},
	{"inputs":[{"internalType":"address","name":"newOwner","type":"address"}],"name":"transferOwnership","outputs":[],"stateMutability":"nonpayable","type":"function"},
	{"inputs":[{"internalType":"bytes32","name":"auditRecordId","type":"bytes32"},{"internalType":"bytes32","name":"expectedHash","type":"bytes32"}],"name":"verifyRecord","outputs":[{"internalType":"bool","name":"isMatch","type":"bool"},{"internalType":"uint64","name":"anchoredAt","type":"uint64"},{"internalType":"address","name":"submitter","type":"address"}],"stateMutability":"view","type":"function"}
]`

// GovernanceAuditRegistry is an auto-generated Go binding around an Ethereum contract.
type GovernanceAuditRegistry struct {
	GovernanceAuditRegistryCaller     // Read-only binding to access the contract state
	GovernanceAuditRegistryTransactor // Write-only binding to send transactions to the contract
	GovernanceAuditRegistryFilterer   // Log filterer for contract events
}

type GovernanceAuditRegistryCaller struct {
	contract *bind.BoundContract
}

type GovernanceAuditRegistryTransactor struct {
	contract *bind.BoundContract
}

type GovernanceAuditRegistryFilterer struct {
	contract *bind.BoundContract
}

func ParsedABI() (abi.ABI, error) {
	return abi.JSON(strings.NewReader(GovernanceAuditRegistryABI))
}

func NewGovernanceAuditRegistry(address common.Address, backend bind.ContractBackend) (*GovernanceAuditRegistry, error) {
	parsed, err := ParsedABI()
	if err != nil {
		return nil, err
	}
	bound := bind.NewBoundContract(address, parsed, backend, backend, backend)
	return &GovernanceAuditRegistry{
		GovernanceAuditRegistryCaller:     GovernanceAuditRegistryCaller{contract: bound},
		GovernanceAuditRegistryTransactor: GovernanceAuditRegistryTransactor{contract: bound},
		GovernanceAuditRegistryFilterer:   GovernanceAuditRegistryFilterer{contract: bound},
	}, nil
}

func (c *GovernanceAuditRegistryCaller) GetRecord(opts *bind.CallOpts, auditRecordId [32]byte) (struct {
	RecordHash [32]byte
	AnchoredAt uint64
	Submitter  common.Address
	Exists     bool
}, error) {
	var out struct {
		RecordHash [32]byte
		AnchoredAt uint64
		Submitter  common.Address
		Exists     bool
	}
	var rawOut []interface{}
	err := c.contract.Call(opts, &rawOut, "getRecord", auditRecordId)
	if err != nil {
		return out, err
	}
	out.RecordHash = *abi.ConvertType(rawOut[0], new([32]byte)).(*[32]byte)
	out.AnchoredAt = *abi.ConvertType(rawOut[1], new(uint64)).(*uint64)
	out.Submitter = *abi.ConvertType(rawOut[2], new(common.Address)).(*common.Address)
	out.Exists = *abi.ConvertType(rawOut[3], new(bool)).(*bool)
	return out, nil
}

func (c *GovernanceAuditRegistryCaller) VerifyRecord(opts *bind.CallOpts, auditRecordId [32]byte, expectedHash [32]byte) (struct {
	IsMatch    bool
	AnchoredAt uint64
	Submitter  common.Address
}, error) {
	var out struct {
		IsMatch    bool
		AnchoredAt uint64
		Submitter  common.Address
	}
	var rawOut []interface{}
	err := c.contract.Call(opts, &rawOut, "verifyRecord", auditRecordId, expectedHash)
	if err != nil {
		return out, err
	}
	out.IsMatch = *abi.ConvertType(rawOut[0], new(bool)).(*bool)
	out.AnchoredAt = *abi.ConvertType(rawOut[1], new(uint64)).(*uint64)
	out.Submitter = *abi.ConvertType(rawOut[2], new(common.Address)).(*common.Address)
	return out, nil
}

func (t *GovernanceAuditRegistryTransactor) AnchorRecord(opts *bind.TransactOpts, auditRecordId [32]byte, recordHash [32]byte) (*types.Transaction, error) {
	return t.contract.Transact(opts, "anchorRecord", auditRecordId, recordHash)
}
