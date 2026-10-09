// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/**
 * @title GovernanceAuditRegistry
 * @dev Tamper-evident, immutable audit registry for State Land Governance (Component 4).
 * Stores only minimal integrity digests (SHA-256 hashes) and timestamps.
 * 
 * Strict Privacy Boundary:
 * Absolutely NO applicant PII, NIC numbers, parcel geometry, case notes, or confidential
 * documents are transmitted to or stored in this contract.
 *
 * Immutability & Idempotency Rules:
 * 1. New ID + new hash -> Anchors successfully.
 * 2. Existing ID + identical hash -> Idempotently accepted as already anchored.
 * 3. Existing ID + differing hash -> Reverted. Off-chain tamper/conflict protection.
 */
contract GovernanceAuditRegistry {
    struct AuditAnchor {
        bytes32 recordHash;
        uint64 anchoredAt;
        address submitter;
        bool exists;
    }

    // Role-based authorization
    address public owner;
    mapping(address => bool) public authorizedSubmitters;

    // auditRecordId (bytes32) => AuditAnchor
    mapping(bytes32 => AuditAnchor) private _anchors;

    // Events
    event AuditRecordAnchored(
        bytes32 indexed auditRecordId,
        bytes32 indexed recordHash,
        uint64 anchoredAt,
        address indexed submitter
    );

    event SubmitterAuthorized(address indexed submitter);
    event SubmitterRevoked(address indexed submitter);
    event OwnershipTransferred(address indexed previousOwner, address indexed newOwner);

    modifier onlyOwner() {
        require(msg.sender == owner, "Caller is not the contract owner");
        _;
    }

    modifier onlyAuthorized() {
        require(msg.sender == owner || authorizedSubmitters[msg.sender], "Caller is not authorized to anchor audit records");
        _;
    }

    constructor() {
        owner = msg.sender;
        authorizedSubmitters[msg.sender] = true;
        emit SubmitterAuthorized(msg.sender);
    }

    function transferOwnership(address newOwner) external onlyOwner {
        require(newOwner != address(0), "New owner cannot be zero address");
        emit OwnershipTransferred(owner, newOwner);
        owner = newOwner;
    }

    function authorizeSubmitter(address submitter) external onlyOwner {
        require(submitter != address(0), "Submitter cannot be zero address");
        authorizedSubmitters[submitter] = true;
        emit SubmitterAuthorized(submitter);
    }

    function revokeSubmitter(address submitter) external onlyOwner {
        require(submitter != address(0), "Submitter cannot be zero address");
        authorizedSubmitters[submitter] = false;
        emit SubmitterRevoked(submitter);
    }

    /**
     * @notice Anchors a governance audit digest.
     * @param auditRecordId UUID of the audit record formatted as bytes32.
     * @param recordHash SHA-256 digest of the canonical audit record as bytes32.
     * @return success true if newly anchored or already idempotently anchored with identical hash.
     */
    function anchorRecord(bytes32 auditRecordId, bytes32 recordHash) external onlyAuthorized returns (bool success) {
        require(auditRecordId != bytes32(0), "AuditRecordId cannot be empty");
        require(recordHash != bytes32(0), "RecordHash cannot be empty");

        AuditAnchor storage existing = _anchors[auditRecordId];

        if (existing.exists) {
            // Idempotency check: If the same record is anchored with the exact same hash, succeed idempotently.
            if (existing.recordHash == recordHash) {
                return true;
            }

            // Tamper/Conflict prevention: Reject modifying an existing anchored record with a different hash.
            revert("Audit record already anchored with differing hash");
        }

        uint64 timestamp = uint64(block.timestamp);
        _anchors[auditRecordId] = AuditAnchor({
            recordHash: recordHash,
            anchoredAt: timestamp,
            submitter: msg.sender,
            exists: true
        });

        emit AuditRecordAnchored(auditRecordId, recordHash, timestamp, msg.sender);
        return true;
    }

    /**
     * @notice Retrieves the anchor data for a given audit record ID.
     */
    function getRecord(bytes32 auditRecordId) external view returns (
        bytes32 recordHash,
        uint64 anchoredAt,
        address submitter,
        bool exists
    ) {
        AuditAnchor storage anchor = _anchors[auditRecordId];
        return (anchor.recordHash, anchor.anchoredAt, anchor.submitter, anchor.exists);
    }

    /**
     * @notice Verifies whether a given expected hash matches the anchored record hash.
     * @return isMatch true if anchored and matches; false if mismatch or not anchored.
     * @return anchoredAt timestamp of on-chain anchoring.
     * @return submitter account that submitted the anchor.
     */
    function verifyRecord(bytes32 auditRecordId, bytes32 expectedHash) external view returns (
        bool isMatch,
        uint64 anchoredAt,
        address submitter
    ) {
        AuditAnchor storage anchor = _anchors[auditRecordId];
        if (!anchor.exists) {
            return (false, 0, address(0));
        }

        bool matchResult = (anchor.recordHash == expectedHash);
        return (matchResult, anchor.anchoredAt, anchor.submitter);
    }
}
