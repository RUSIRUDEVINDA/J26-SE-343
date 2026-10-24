from uuid import UUID
import pytest
from app.semantic.template_mapping import TemplateMappingRequest, map_template
from app.semantic.gemini_client import GeminiResponseError


class Client:
    model_name = "EXPLICIT TEST DOUBLE"
    def __init__(self, observations):
        self.observations = observations
    def generate_content(self, *args, **kwargs):
        return {"observations": self.observations}


def request(text="Actual source text", conditional=False):
    return TemplateMappingRequest(documentId=UUID(int=1), documentVersionId=UUID(int=2), analysisRunId=UUID(int=3),
        templateId="TEST_ONLY", templateVersion="1", definitionDigest="TEST_DIGEST",
        pages=[{"pageNumber": 1, "text": text}], requirements=[{"requirementId": "extra_template_section",
        "displayName": "A section beyond the fact catalogue", "sourceReference": "TEST_ONLY",
        "applicabilityContext": "Conditional test" if conditional else None}])


@pytest.mark.parametrize("quote,state", [("Actual source text", "Present"), ("Invented quote", "Uncertain")])
def test_exact_page_grounding(quote, state):
    result = map_template(request(), Client([{"requirementId": "extra_template_section", "state": "Present",
        "pageNumber": 1, "textSpan": quote, "explanation": "Test observation"}]))
    assert result["observations"][0]["state"] == state
    assert result["isMachineGenerated"] is True
    assert result["analysisRunId"] == str(UUID(int=3))


def test_conditional_requires_officer_resolution():
    result = map_template(request(conditional=True), Client([{"requirementId": "extra_template_section", "state": "Present",
        "pageNumber": 1, "textSpan": "Actual source text", "explanation": "Test observation"}]))
    assert result["observations"][0]["state"] == "Uncertain"


def test_empty_ocr_page_does_not_establish_missing():
    result = map_template(request(text=""), Client([{"requirementId": "extra_template_section", "state": "Missing", "explanation": "Test absence"}]))
    assert result["observations"][0]["state"] == "Uncertain"


def test_absent_observation_is_uncertain():
    assert map_template(request(), Client([]))["observations"][0]["state"] == "Uncertain"


@pytest.mark.parametrize("items", [[{"requirementId": "unknown", "state": "Missing", "explanation": "Test"}],
    [{"requirementId": "extra_template_section", "state": "Missing", "explanation": "Test"}] * 2])
def test_unknown_or_duplicate_requirements_are_rejected(items):
    with pytest.raises(GeminiResponseError):
        map_template(request(), Client(items))
