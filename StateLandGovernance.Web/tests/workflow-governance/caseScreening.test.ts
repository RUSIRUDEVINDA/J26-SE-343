import assert from "node:assert/strict";
import test, { afterEach } from "node:test";
import { workflowDevelopmentHeaders, workflowAuthRole } from "../../src/modules/workflow-governance/services/workflowDevelopmentHeaders";

const env = process.env as Record<string, string | undefined>;
const priorEnvironment = { node: env.NODE_ENV, optIn: env.NEXT_PUBLIC_WORKFLOW_GOVERNANCE_DEV_AUTH };
const priorWindow = globalThis.window;
afterEach(() => {
  if (priorEnvironment.node === undefined) delete env.NODE_ENV; else env.NODE_ENV = priorEnvironment.node;
  if (priorEnvironment.optIn === undefined) delete env.NEXT_PUBLIC_WORKFLOW_GOVERNANCE_DEV_AUTH; else env.NEXT_PUBLIC_WORKFLOW_GOVERNANCE_DEV_AUTH = priorEnvironment.optIn;
  Object.defineProperty(globalThis, "window", { value: priorWindow, writable: true, configurable: true });
});

function setup(node = "development", hostname = "localhost", optIn = "true") {
  env.NODE_ENV = node; env.NEXT_PUBLIC_WORKFLOW_GOVERNANCE_DEV_AUTH = optIn;
  Object.defineProperty(globalThis, "window", { configurable: true, writable: true, value: {
    location: { hostname }, localStorage: { getItem: () => "33333333-3333-3333-3333-333333333333" },
  } });
}

test("C3 local officer headers require explicit Development opt-in", () => {
  setup();
  assert.equal((workflowDevelopmentHeaders("officer")?.headers as Record<string, string>)["X-Actor-Role"], "Officer");
  assert.equal(workflowAuthRole("officer"), "none");
  setup("development", "localhost", "false"); assert.equal(workflowDevelopmentHeaders("officer"), undefined);
});
test("Production never emits C3 development authority headers", () => {
  setup("production"); assert.equal(workflowDevelopmentHeaders("officer"), undefined);
  assert.equal(workflowAuthRole("officer"), "officer");
});
test("A remote hostname cannot use the local Development header scheme", () => {
  setup("development", "example.org"); assert.equal(workflowDevelopmentHeaders("officer"), undefined);
});
test("Applicant headers retain owner identity without officer authority", () => {
  setup();
  const headers = workflowDevelopmentHeaders("applicant")?.headers as Record<string, string>;
  assert.equal(headers["X-Actor-Role"], "Applicant");
  assert.equal(headers["X-Actor-Id"], "33333333-3333-3333-3333-333333333333");
  assert.equal(workflowDevelopmentHeaders("none"), undefined);
});
