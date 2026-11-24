import assert from "node:assert/strict";
import test from "node:test";

const {
  validateMockCredentials,
  isLandIntelligencePathAllowed,
  landIntelligenceNavItemsForRole,
  mockLandIntelligenceHome,
  readMockLandIntelligenceSession,
} = await import(
  "../src/modules/component-01-land-intelligence/utils/mockLandIntelligenceAuth.ts"
);

const parcelId = "380bd64e-699c-61fd-227e-1a7611f70c36";

test("validateMockCredentials accepts demo users case-insensitively", () => {
  const customer = validateMockCredentials("Ravindu@gmail.com", "123456");
  assert.equal(customer?.role, "customer");
  assert.equal(customer?.email, "ravindu@gmail.com");

  const officer = validateMockCredentials("officer@gmail.com", "123456");
  assert.equal(officer?.role, "officer");

  assert.equal(validateMockCredentials("ravindu@gmail.com", "wrong"), null);
  assert.equal(validateMockCredentials("unknown@test.com", "123456"), null);
});

test("mockLandIntelligenceHome paths", () => {
  assert.equal(mockLandIntelligenceHome("customer"), "/land-intelligence");
  assert.equal(
    mockLandIntelligenceHome("officer"),
    "/land-intelligence/parcels",
  );
});

test("customer path allow-list", () => {
  assert.equal(isLandIntelligencePathAllowed("/land-intelligence", "customer"), true);
  assert.equal(
    isLandIntelligencePathAllowed("/land-intelligence/recommendations", "customer"),
    true,
  );
  assert.equal(
    isLandIntelligencePathAllowed(
      `/land-intelligence/parcels/${parcelId}/lease-interest`,
      "customer",
    ),
    true,
  );
  assert.equal(
    isLandIntelligencePathAllowed("/land-intelligence/parcels", "customer"),
    false,
  );
  assert.equal(
    isLandIntelligencePathAllowed(
      "/land-intelligence/officer/lease-interest-queue",
      "customer",
    ),
    false,
  );
});

test("officer path allow-list", () => {
  assert.equal(
    isLandIntelligencePathAllowed("/land-intelligence/parcels", "officer"),
    true,
  );
  assert.equal(
    isLandIntelligencePathAllowed(
      "/land-intelligence/officer/lease-interest-queue",
      "officer",
    ),
    true,
  );
  assert.equal(
    isLandIntelligencePathAllowed("/land-intelligence", "officer"),
    false,
  );
  assert.equal(
    isLandIntelligencePathAllowed("/land-intelligence/my-requests", "officer"),
    false,
  );
});

test("role-based nav item counts", () => {
  assert.equal(landIntelligenceNavItemsForRole("customer").length, 4);
  assert.equal(landIntelligenceNavItemsForRole("officer").length, 3);
});

test("readMockLandIntelligenceSession caches the snapshot object", () => {
  const store = new Map();
  globalThis.window = {
    sessionStorage: {
      getItem(key) {
        return store.has(key) ? store.get(key) : null;
      },
      setItem(key, value) {
        store.set(key, String(value));
      },
      removeItem(key) {
        store.delete(key);
      },
    },
    dispatchEvent() {
      return true;
    },
  };

  const key = "component01.mockLandIntelligenceAuth";
  store.set(
    key,
    JSON.stringify({
      role: "customer",
      email: "ravindu@gmail.com",
      signedInAt: "2026-10-25T00:00:00.000Z",
    }),
  );

  const first = readMockLandIntelligenceSession();
  const second = readMockLandIntelligenceSession();
  assert.equal(first, second);
  assert.equal(first?.role, "customer");

  store.set(
    key,
    JSON.stringify({
      role: "officer",
      email: "officer@gmail.com",
      signedInAt: "2026-10-25T01:00:00.000Z",
    }),
  );
  const updated = readMockLandIntelligenceSession();
  const updatedAgain = readMockLandIntelligenceSession();
  assert.notEqual(updated, first);
  assert.equal(updated, updatedAgain);
  assert.equal(updated?.role, "officer");

  store.set(key, "{not-json");
  assert.equal(readMockLandIntelligenceSession(), null);
  assert.equal(store.has(key), false);
  assert.equal(readMockLandIntelligenceSession(), null);
});
