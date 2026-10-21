const { expect } = require("chai");
const { ethers } = require("hardhat");

describe("GovernanceAuditRegistry Contract Tests", function () {
  let registry;
  let owner;
  let authorizedSubmitter;
  let unauthorizedAccount;

  const testRecordId = ethers.encodeBytes32String("test-record-001");
  const testHashA = ethers.keccak256(ethers.toUtf8Bytes("canonical-audit-payload-v1-a"));
  const testHashB = ethers.keccak256(ethers.toUtf8Bytes("canonical-audit-payload-v1-b"));

  beforeEach(async function () {
    [owner, authorizedSubmitter, unauthorizedAccount] = await ethers.getSigners();

    const RegistryFactory = await ethers.getContractFactory("GovernanceAuditRegistry");
    registry = await RegistryFactory.deploy();
    await registry.waitForDeployment();

    // Authorize submitter
    await registry.connect(owner).authorizeSubmitter(authorizedSubmitter.address);
  });

  describe("Access Control", function () {
    it("should allow owner to anchor records", async function () {
      const tx = await registry.connect(owner).anchorRecord(testRecordId, testHashA);
      await tx.wait();

      const [recordHash, anchoredAt, submitter, exists] = await registry.getRecord(testRecordId);
      expect(exists).to.be.true;
      expect(recordHash).to.equal(testHashA);
      expect(submitter).to.equal(owner.address);
      expect(anchoredAt).to.be.gt(0);
    });

    it("should allow authorized submitter to anchor records", async function () {
      const recordId2 = ethers.encodeBytes32String("test-record-002");
      const tx = await registry.connect(authorizedSubmitter).anchorRecord(recordId2, testHashA);
      await tx.wait();

      const [recordHash, , submitter, exists] = await registry.getRecord(recordId2);
      expect(exists).to.be.true;
      expect(recordHash).to.equal(testHashA);
      expect(submitter).to.equal(authorizedSubmitter.address);
    });

    it("should reject anchoring from unauthorized account", async function () {
      await expect(
        registry.connect(unauthorizedAccount).anchorRecord(testRecordId, testHashA)
      ).to.be.revertedWith("Caller is not authorized to anchor audit records");
    });

    it("should allow revoking a submitter", async function () {
      await registry.connect(owner).revokeSubmitter(authorizedSubmitter.address);

      await expect(
        registry.connect(authorizedSubmitter).anchorRecord(testRecordId, testHashA)
      ).to.be.revertedWith("Caller is not authorized to anchor audit records");
    });
  });

  describe("Idempotency & Tamper Resistance", function () {
    it("should idempotently accept the same ID with identical hash", async function () {
      // First anchor
      await registry.connect(owner).anchorRecord(testRecordId, testHashA);

      // Second anchor with identical hash
      const tx2 = await registry.connect(owner).anchorRecord(testRecordId, testHashA);
      const receipt = await tx2.wait();
      expect(receipt.status).to.equal(1);

      const [recordHash, , , exists] = await registry.getRecord(testRecordId);
      expect(exists).to.be.true;
      expect(recordHash).to.equal(testHashA);
    });

    it("should revert if attempting to re-anchor existing ID with a different hash", async function () {
      // First anchor with hash A
      await registry.connect(owner).anchorRecord(testRecordId, testHashA);

      // Attempt to re-anchor same ID with hash B
      await expect(
        registry.connect(owner).anchorRecord(testRecordId, testHashB)
      ).to.be.revertedWith("Audit record already anchored with differing hash");
    });
  });

  describe("Integrity Verification", function () {
    it("should return isMatch = true when verifying with identical hash", async function () {
      await registry.connect(owner).anchorRecord(testRecordId, testHashA);

      const [isMatch, anchoredAt, submitter] = await registry.verifyRecord(testRecordId, testHashA);
      expect(isMatch).to.be.true;
      expect(anchoredAt).to.be.gt(0);
      expect(submitter).to.equal(owner.address);
    });

    it("should return isMatch = false when verifying with differing hash", async function () {
      await registry.connect(owner).anchorRecord(testRecordId, testHashA);

      const [isMatch, anchoredAt, submitter] = await registry.verifyRecord(testRecordId, testHashB);
      expect(isMatch).to.be.false;
      expect(anchoredAt).to.be.gt(0);
      expect(submitter).to.equal(owner.address);
    });

    it("should return isMatch = false for unanchored record ID", async function () {
      const nonExistentId = ethers.encodeBytes32String("non-existent");
      const [isMatch, anchoredAt, submitter] = await registry.verifyRecord(nonExistentId, testHashA);
      expect(isMatch).to.be.false;
      expect(anchoredAt).to.equal(0);
      expect(submitter).to.equal(ethers.ZeroAddress);
    });
  });
});
