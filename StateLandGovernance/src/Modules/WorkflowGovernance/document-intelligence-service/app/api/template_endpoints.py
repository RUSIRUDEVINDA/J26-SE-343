from fastapi import APIRouter, HTTPException
from app.semantic.template_mapping import TemplateMappingRequest, map_template
from app.semantic.gemini_client import GeminiError

router = APIRouter()


@router.post("/proposal-template/map")
def map_proposal_template(request: TemplateMappingRequest):
    try:
        return map_template(request)
    except GeminiError:
        # Never expose upstream response bodies, document content or credentials.
        raise HTTPException(status_code=503, detail="Template mapping unavailable; officer review required.")
