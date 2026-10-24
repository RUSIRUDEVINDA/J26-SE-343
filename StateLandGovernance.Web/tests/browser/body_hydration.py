"""Browser regression: extension-owned body attributes must not mask child errors.
Run while Next Development is serving: python tests/browser/body_hydration.py
Requires Playwright with Microsoft Edge. No sign-in or workflow mutations occur.
"""
import json
import os
from playwright.sync_api import sync_playwright

URL = os.environ.get("HYDRATION_TEST_BASE_URL", "http://127.0.0.1:3000") + "/sign-in"

def run():
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, channel="msedge")
        for scenario in ["clean", "extension_attributes", "child_mismatch"]:
            print("Checking " + scenario, flush=True)
            context = browser.new_context()
            page = context.new_page()
            page.set_default_timeout(10000)
            page.set_default_navigation_timeout(15000)
            # Isolate this hydration test from API availability; no authentication occurs.
            page.route("**/api/development-auth/status", lambda route: route.fulfill(
                status=200, content_type="application/json", body='{"enabled":true}'))
            errors = []
            def collect(message):
                value = message.text if hasattr(message, "text") else str(message)
                if "hydrat" in value.lower():
                    errors.append(value)
            page.on("console", collect)
            page.on("pageerror", collect)
            if scenario != "clean":
                # Simulate an extension's document_start observer without rewriting
                # Next's streamed HTML or its transport/security response headers.
                attribute = "data-gr-ext-installed" if scenario == "extension_attributes" else "data-hydration-test-child"
                selector = "body" if scenario == "extension_attributes" else 'input[name="username"]'
                page.add_init_script("""(() => {
                    const selector = __SELECTOR__;
                    const attribute = __ATTRIBUTE__;
                    const mutate = () => {
                        const element = document.querySelector(selector);
                        if (!element) return false;
                        element.setAttribute(attribute, '');
                        if (selector === 'body') element.setAttribute('data-new-gr-c-s-check-loaded', '14.1338.0');
                        return true;
                    };
                    if (!mutate()) {
                        const observer = new MutationObserver(() => { if (mutate()) observer.disconnect(); });
                        observer.observe(document, {childList:true, subtree:true});
                    }
                })();""".replace("__SELECTOR__", json.dumps(selector)).replace("__ATTRIBUTE__", json.dumps(attribute)))
            page.goto(URL)
            page.get_by_label("Test username").wait_for()
            page.get_by_label("Test username").fill("hydration-test-no-submit")
            page.wait_for_function("() => { const b = document.querySelector('form button'); return b && !b.disabled; }")
            page.wait_for_timeout(500)
            if scenario == "child_mismatch":
                assert errors, "Child mismatch must still be detected"
            else:
                assert not errors, errors
            if scenario == "extension_attributes":
                assert page.locator("body").get_attribute("data-gr-ext-installed") == ""
            print(scenario + ": passed")
            context.close()
        browser.close()

if __name__ == "__main__":
    run()
